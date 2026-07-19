using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One instantaneous Solo attack decision. A Unity adapter resolves the
    /// stable entity handle and applies the hit after the Agent Tick succeeds.
    /// </summary>
    public sealed class KLEPZombieAttackExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition attackTargetDefinition;

        public KLEPZombieAttackExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition attackTargetDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A zombie attack behavior");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                attackTargetDefinition,
                nameof(attackTargetDefinition),
                "AttackTarget");
            this.attackTargetDefinition = attackTargetDefinition;
        }

        public KLEPKeyDefinition AttackTargetDefinition =>
            attackTargetDefinition;
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
                    attackTargetDefinition,
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
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
