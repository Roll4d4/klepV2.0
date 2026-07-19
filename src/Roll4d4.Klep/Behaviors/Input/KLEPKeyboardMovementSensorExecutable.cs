using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes one Local OneCycle presence Key for each pressed W/A/S/D
    /// direction in the immutable sample supplied before the Tick.
    /// </summary>
    public sealed class KLEPKeyboardMovementSensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition wDefinition;
        private readonly KLEPKeyDefinition aDefinition;
        private readonly KLEPKeyDefinition sDefinition;
        private readonly KLEPKeyDefinition dDefinition;
        private KLEPKeyboardMovementInput observation =
            KLEPKeyboardMovementInput.None;

        public KLEPKeyboardMovementSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition wDefinition,
            KLEPKeyDefinition aDefinition,
            KLEPKeyDefinition sDefinition,
            KLEPKeyDefinition dDefinition)
            : base(definition)
        {
            KLEPPlayerInputBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "A keyboard movement sensor");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                wDefinition, nameof(wDefinition), "W");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                aDefinition, nameof(aDefinition), "A");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                sDefinition, nameof(sDefinition), "S");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                dDefinition, nameof(dDefinition), "D");
            KLEPPlayerInputBehaviorValidation.RequireDistinctDefinitions(
                nameof(definition),
                wDefinition,
                aDefinition,
                sDefinition,
                dDefinition);

            if (definition.DeclaredOutputs.Count != 4)
            {
                throw new ArgumentException(
                    "A keyboard movement sensor requires exactly four " +
                    "declared outputs.",
                    nameof(definition));
            }

            KLEPPlayerInputBehaviorValidation.RequireExactDeclaredOutput(
                definition, wDefinition, "W");
            KLEPPlayerInputBehaviorValidation.RequireExactDeclaredOutput(
                definition, aDefinition, "A");
            KLEPPlayerInputBehaviorValidation.RequireExactDeclaredOutput(
                definition, sDefinition, "S");
            KLEPPlayerInputBehaviorValidation.RequireExactDeclaredOutput(
                definition, dDefinition, "D");

            this.wDefinition = wDefinition;
            this.aDefinition = aDefinition;
            this.sDefinition = sDefinition;
            this.dDefinition = dDefinition;
        }

        public KLEPKeyboardMovementInput Observation => observation;
        public KLEPKeyDefinition WDefinition => wDefinition;
        public KLEPKeyDefinition ADefinition => aDefinition;
        public KLEPKeyDefinition SDefinition => sDefinition;
        public KLEPKeyDefinition DDefinition => dDefinition;

        public void SetObservation(KLEPKeyboardMovementInput observation)
        {
            this.observation = observation ??
                throw new ArgumentNullException(nameof(observation));
        }

        public void SetObservation(bool w, bool a, bool s, bool d)
        {
            SetObservation(new KLEPKeyboardMovementInput(w, a, s, d));
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observation.W)
            {
                context.Add(wDefinition);
            }

            if (observation.A)
            {
                context.Add(aDefinition);
            }

            if (observation.S)
            {
                context.Add(sDefinition);
            }

            if (observation.D)
            {
                context.Add(dDefinition);
            }

            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
