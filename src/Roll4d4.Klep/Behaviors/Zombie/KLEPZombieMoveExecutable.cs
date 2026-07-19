using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// A continuous Solo movement state. The pure behavior captures the current
    /// immutable MoveTarget and remains Running; a Unity adapter applies motion
    /// only for an intent produced during the completed Agent Tick.
    /// </summary>
    public sealed class KLEPZombieMoveExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition moveTargetDefinition;

        public KLEPZombieMoveExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition moveTargetDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A zombie move behavior");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                moveTargetDefinition,
                nameof(moveTargetDefinition),
                "MoveTarget");
            this.moveTargetDefinition = moveTargetDefinition;
        }

        public KLEPKeyDefinition MoveTargetDefinition => moveTargetDefinition;
        public KLEPEnemyObservation LastTarget { get; private set; }
        public long IntentCycleIndex { get; private set; } = -1;
        public long IntentRunIndex { get; private set; } = -1;

        public bool TryGetIntent(
            long cycleIndex,
            out KLEPEnemyObservation target)
        {
            target = IntentCycleIndex == cycleIndex ? LastTarget : null;
            return target != null;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!KLEPZombieBehaviorValidation.TryReadSingleTarget(
                    context.Keys,
                    moveTargetDefinition,
                    out KLEPEnemyObservation target))
            {
                LastTarget = null;
                IntentCycleIndex = -1;
                IntentRunIndex = -1;
                return KLEPExecutableTickStatus.Failed;
            }

            LastTarget = target;
            IntentCycleIndex = context.CycleIndex;
            IntentRunIndex = context.RunIndex;
            return KLEPExecutableTickStatus.Running;
        }
    }
}
