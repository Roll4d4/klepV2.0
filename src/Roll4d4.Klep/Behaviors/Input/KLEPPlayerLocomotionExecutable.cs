using System;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One continuous Solo intent that combines mouse-facing and actor-local
    /// keyboard movement. The authored Lock supplies Ground + MouseAim
    /// eligibility; W/A/S/D are optional presence facts read from the same
    /// immutable final snapshot.
    /// </summary>
    public sealed class KLEPPlayerLocomotionExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition wDefinition;
        private readonly KLEPKeyDefinition aDefinition;
        private readonly KLEPKeyDefinition sDefinition;
        private readonly KLEPKeyDefinition dDefinition;
        private readonly KLEPKeyDefinition mouseAimDefinition;

        public KLEPPlayerLocomotionExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition wDefinition,
            KLEPKeyDefinition aDefinition,
            KLEPKeyDefinition sDefinition,
            KLEPKeyDefinition dDefinition,
            KLEPKeyDefinition mouseAimDefinition)
            : base(definition)
        {
            KLEPPlayerInputBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "Player locomotion");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                wDefinition, nameof(wDefinition), "W");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                aDefinition, nameof(aDefinition), "A");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                sDefinition, nameof(sDefinition), "S");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                dDefinition, nameof(dDefinition), "D");
            KLEPPlayerInputBehaviorValidation.RequireLocalOneCycle(
                mouseAimDefinition,
                nameof(mouseAimDefinition),
                "MouseAim");
            KLEPPlayerInputBehaviorValidation.RequireDistinctDefinitions(
                nameof(definition),
                wDefinition,
                aDefinition,
                sDefinition,
                dDefinition,
                mouseAimDefinition);

            this.wDefinition = wDefinition;
            this.aDefinition = aDefinition;
            this.sDefinition = sDefinition;
            this.dDefinition = dDefinition;
            this.mouseAimDefinition = mouseAimDefinition;
        }

        public KLEPKeyboardMovementInput LastInput { get; private set; }
        public KLEPMouseAimObservation LastAim { get; private set; }
        public long IntentCycleIndex { get; private set; } = -1;
        public long IntentRunIndex { get; private set; } = -1;

        public bool TryGetIntent(
            long cycleIndex,
            out KLEPKeyboardMovementInput input,
            out KLEPMouseAimObservation aim)
        {
            if (IntentCycleIndex == cycleIndex && LastAim != null)
            {
                input = LastInput;
                aim = LastAim;
                return true;
            }

            input = null;
            aim = null;
            return false;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!KLEPPlayerInputBehaviorValidation.TryReadSingleAim(
                    context.Keys,
                    mouseAimDefinition,
                    out KLEPMouseAimObservation aim))
            {
                ClearIntent();
                return KLEPExecutableTickStatus.Failed;
            }

            LastInput = new KLEPKeyboardMovementInput(
                context.Keys.Contains(wDefinition.Id),
                context.Keys.Contains(aDefinition.Id),
                context.Keys.Contains(sDefinition.Id),
                context.Keys.Contains(dDefinition.Id));
            LastAim = aim;
            IntentCycleIndex = context.CycleIndex;
            IntentRunIndex = context.RunIndex;
            return KLEPExecutableTickStatus.Running;
        }

        private void ClearIntent()
        {
            LastInput = null;
            LastAim = null;
            IntentCycleIndex = -1;
            IntentRunIndex = -1;
        }
    }
}
