using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One W/A/S/D branch of a keyboard observation. Four stable-ID-distinct
    /// Tandem roots may share the same immutable sample; each root declares and
    /// may emit only its own Local OneCycle direction Key.
    /// </summary>
    public sealed class KLEPKeyboardMovementSensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition wDefinition;
        private readonly KLEPKeyDefinition aDefinition;
        private readonly KLEPKeyDefinition sDefinition;
        private readonly KLEPKeyDefinition dDefinition;
        private readonly KLEPKeyDefinition outputDefinition;
        private readonly KeyboardBranch branch;
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

            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    "A keyboard movement sensor branch requires exactly one " +
                    "declared output.",
                    nameof(definition));
            }

            KLEPKeyDefinition declared = definition.DeclaredOutputs[0];
            if (ReferenceEquals(declared, wDefinition))
            {
                branch = KeyboardBranch.W;
            }
            else if (ReferenceEquals(declared, aDefinition))
            {
                branch = KeyboardBranch.A;
            }
            else if (ReferenceEquals(declared, sDefinition))
            {
                branch = KeyboardBranch.S;
            }
            else if (ReferenceEquals(declared, dDefinition))
            {
                branch = KeyboardBranch.D;
            }
            else
            {
                throw new ArgumentException(
                    "A keyboard movement sensor branch must declare exactly " +
                    "one of the supplied W, A, S, or D Key definition objects.",
                    nameof(definition));
            }

            this.wDefinition = wDefinition;
            this.aDefinition = aDefinition;
            this.sDefinition = sDefinition;
            this.dDefinition = dDefinition;
            outputDefinition = declared;
        }

        public KLEPKeyboardMovementInput Observation => observation;
        public KLEPKeyDefinition WDefinition => wDefinition;
        public KLEPKeyDefinition ADefinition => aDefinition;
        public KLEPKeyDefinition SDefinition => sDefinition;
        public KLEPKeyDefinition DDefinition => dDefinition;
        public KLEPKeyDefinition OutputDefinition => outputDefinition;

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
            bool isPressed;
            switch (branch)
            {
                case KeyboardBranch.W:
                    isPressed = observation.W;
                    break;
                case KeyboardBranch.A:
                    isPressed = observation.A;
                    break;
                case KeyboardBranch.S:
                    isPressed = observation.S;
                    break;
                case KeyboardBranch.D:
                    isPressed = observation.D;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Keyboard movement sensor branch is invalid.");
            }

            if (!isPressed)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(outputDefinition);
            return KLEPExecutableTickStatus.Succeeded;
        }

        private enum KeyboardBranch
        {
            W,
            A,
            S,
            D
        }
    }
}
