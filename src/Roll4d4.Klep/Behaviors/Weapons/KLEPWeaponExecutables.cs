using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes one externally supplied, immutable WeaponObservation during a
    /// Tandem wave. Absence is a failed, no-output observation.
    /// </summary>
    public sealed class KLEPWeaponObservationSensorExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition observationDefinition;

        public KLEPWeaponObservationSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition observationDefinition)
            : base(definition)
        {
            KLEPWeaponBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "A weapon observation sensor");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                observationDefinition,
                nameof(observationDefinition),
                "WeaponObservation");
            KLEPWeaponBehaviorValidation.RequireSingleExactDeclaredOutput(
                definition,
                observationDefinition,
                "WeaponObservation");
            this.observationDefinition = observationDefinition;
        }

        public KLEPKeyDefinition ObservationDefinition =>
            observationDefinition;
        public KLEPWeaponObservation Observation { get; private set; }

        public void SetObservation(KLEPWeaponObservation observation)
        {
            Observation = observation;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (Observation == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(observationDefinition, Observation.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }

    public enum KLEPWeaponObservationRoute
    {
        Loaded = 0,
        SafeLine = 1,
        FriendlyLine = 2
    }

    /// <summary>
    /// Routes one WeaponObservation into an explicit loaded, hostile-entity
    /// line, or friendly-entity line fact. Safe and Friendly are deliberately
    /// evidence, not firing authority.
    /// </summary>
    public sealed class KLEPWeaponObservationRouterExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition observationDefinition;
        private readonly KLEPKeyDefinition outputDefinition;

        public KLEPWeaponObservationRouterExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition observationDefinition,
            KLEPKeyDefinition loadedDefinition,
            KLEPKeyDefinition safeLineDefinition,
            KLEPKeyDefinition friendlyLineDefinition)
            : base(definition)
        {
            KLEPWeaponBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Router,
                KLEPExecutionMode.Tandem,
                "A weapon observation router");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                observationDefinition,
                nameof(observationDefinition),
                "WeaponObservation");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                loadedDefinition,
                nameof(loadedDefinition),
                "WeaponLoaded");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                safeLineDefinition,
                nameof(safeLineDefinition),
                "WeaponSafeLine");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                friendlyLineDefinition,
                nameof(friendlyLineDefinition),
                "WeaponFriendlyLine");
            KLEPWeaponBehaviorValidation.RequireDistinctDefinitions(
                nameof(observationDefinition),
                observationDefinition,
                loadedDefinition,
                safeLineDefinition,
                friendlyLineDefinition);

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "A weapon observation router requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPKeyDefinition declared = definition.DeclaredOutputs[0];
            if (ReferenceEquals(declared, loadedDefinition))
            {
                Route = KLEPWeaponObservationRoute.Loaded;
                outputDefinition = loadedDefinition;
            }
            else if (ReferenceEquals(declared, safeLineDefinition))
            {
                Route = KLEPWeaponObservationRoute.SafeLine;
                outputDefinition = safeLineDefinition;
            }
            else if (ReferenceEquals(declared, friendlyLineDefinition))
            {
                Route = KLEPWeaponObservationRoute.FriendlyLine;
                outputDefinition = friendlyLineDefinition;
            }
            else
            {
                throw new ArgumentException(
                    "A weapon router's declared output must be the exact " +
                    "WeaponLoaded, WeaponSafeLine, or WeaponFriendlyLine definition.",
                    nameof(definition));
            }

            this.observationDefinition = observationDefinition;
        }

        public KLEPWeaponObservationRoute Route { get; }
        public KLEPKeyDefinition ObservationDefinition =>
            observationDefinition;
        public KLEPKeyDefinition OutputDefinition => outputDefinition;
        public KLEPWeaponObservation LastObservation { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!KLEPWeaponBehaviorValidation.TryReadSingleObservation(
                    context.Keys,
                    observationDefinition,
                    "Weapon observation router",
                    out KLEPWeaponObservation observation))
            {
                LastObservation = null;
                return KLEPExecutableTickStatus.Failed;
            }

            LastObservation = observation;
            bool matches;
            switch (Route)
            {
                case KLEPWeaponObservationRoute.Loaded:
                    matches = observation.IsLoaded;
                    break;
                case KLEPWeaponObservationRoute.SafeLine:
                    matches = observation.HasSafeLine;
                    break;
                case KLEPWeaponObservationRoute.FriendlyLine:
                    matches = observation.HasFriendlyEntityHit;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Weapon route '{Route}' is unsupported.");
            }

            if (!matches)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(outputDefinition, observation.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }

    /// <summary>
    /// One instantaneous Solo decision to request a shot. Successful completion
    /// guarantees one frozen WeaponFireIntent Key; it does not claim a world hit.
    /// </summary>
    public sealed class KLEPWeaponFireExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition observationDefinition;
        private readonly KLEPKeyDefinition fireIntentDefinition;

        public KLEPWeaponFireExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition observationDefinition,
            KLEPKeyDefinition fireIntentDefinition)
            : base(definition)
        {
            KLEPWeaponBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A weapon Fire action");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                observationDefinition,
                nameof(observationDefinition),
                "WeaponObservation");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                fireIntentDefinition,
                nameof(fireIntentDefinition),
                "WeaponFireIntent");
            KLEPWeaponBehaviorValidation.RequireDistinctDefinitions(
                nameof(observationDefinition),
                observationDefinition,
                fireIntentDefinition);
            KLEPWeaponBehaviorValidation.RequireSingleExactDeclaredOutput(
                definition,
                fireIntentDefinition,
                "WeaponFireIntent");
            this.observationDefinition = observationDefinition;
            this.fireIntentDefinition = fireIntentDefinition;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!KLEPWeaponBehaviorValidation.TryReadSingleObservation(
                    context.Keys,
                    observationDefinition,
                    "Weapon Fire action",
                    out KLEPWeaponObservation observation) ||
                !observation.IsLoaded)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(
                fireIntentDefinition,
                new KLEPWeaponFireIntent(observation).ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }

    /// <summary>
    /// One instantaneous Solo decision to request an ammunition transfer.
    /// Successful completion guarantees one frozen WeaponReloadIntent Key.
    /// </summary>
    public sealed class KLEPWeaponReloadExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition observationDefinition;
        private readonly KLEPKeyDefinition reloadIntentDefinition;

        public KLEPWeaponReloadExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition observationDefinition,
            KLEPKeyDefinition reloadIntentDefinition)
            : base(definition)
        {
            KLEPWeaponBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A weapon Reload action");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                observationDefinition,
                nameof(observationDefinition),
                "WeaponObservation");
            KLEPWeaponBehaviorValidation.RequireClosedLocalOneCycle(
                reloadIntentDefinition,
                nameof(reloadIntentDefinition),
                "WeaponReloadIntent");
            KLEPWeaponBehaviorValidation.RequireDistinctDefinitions(
                nameof(observationDefinition),
                observationDefinition,
                reloadIntentDefinition);
            KLEPWeaponBehaviorValidation.RequireSingleExactDeclaredOutput(
                definition,
                reloadIntentDefinition,
                "WeaponReloadIntent");
            this.observationDefinition = observationDefinition;
            this.reloadIntentDefinition = reloadIntentDefinition;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!KLEPWeaponBehaviorValidation.TryReadSingleObservation(
                    context.Keys,
                    observationDefinition,
                    "Weapon Reload action",
                    out KLEPWeaponObservation observation) ||
                !observation.CanReload)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(
                reloadIntentDefinition,
                new KLEPWeaponReloadIntent(observation).ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
