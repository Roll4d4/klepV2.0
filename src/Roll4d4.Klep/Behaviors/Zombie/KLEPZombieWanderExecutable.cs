using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// A continuous Solo wander state. The pure behavior exposes the current
    /// immutable local-space direction for a Unity effect adapter.
    /// </summary>
    public sealed class KLEPZombieWanderExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition wanderDirectionDefinition;

        public KLEPZombieWanderExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition wanderDirectionDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A zombie wander behavior");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                wanderDirectionDefinition,
                nameof(wanderDirectionDefinition),
                "WanderDirection");
            this.wanderDirectionDefinition = wanderDirectionDefinition;
        }

        public KLEPKeyDefinition WanderDirectionDefinition =>
            wanderDirectionDefinition;
        public double LastDirectionX { get; private set; }
        public double LastDirectionZ { get; private set; }
        public int LastHeadingIndex { get; private set; } = -1;
        public long IntentCycleIndex { get; private set; } = -1;
        public long IntentRunIndex { get; private set; } = -1;

        public bool TryGetIntent(
            long cycleIndex,
            out double directionX,
            out double directionZ,
            out int headingIndex)
        {
            if (IntentCycleIndex == cycleIndex)
            {
                directionX = LastDirectionX;
                directionZ = LastDirectionZ;
                headingIndex = LastHeadingIndex;
                return true;
            }

            directionX = 0d;
            directionZ = 0d;
            headingIndex = -1;
            return false;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(wanderDirectionDefinition.Id);
            if (facts.Count == 0)
            {
                ClearIntent();
                return KLEPExecutableTickStatus.Failed;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    "Zombie wander expected exactly one '" +
                    $"{wanderDirectionDefinition.Id}' occurrence, but found " +
                    $"{facts.Count}.");
            }

            KLEPDeterministicWanderRouterExecutable.ReadPayload(
                facts[0].Payload,
                out double directionX,
                out double directionZ,
                out int headingIndex);
            LastDirectionX = directionX;
            LastDirectionZ = directionZ;
            LastHeadingIndex = headingIndex;
            IntentCycleIndex = context.CycleIndex;
            IntentRunIndex = context.RunIndex;
            return KLEPExecutableTickStatus.Running;
        }

        private void ClearIntent()
        {
            LastDirectionX = 0d;
            LastDirectionZ = 0d;
            LastHeadingIndex = -1;
            IntentCycleIndex = -1;
            IntentRunIndex = -1;
        }
    }
}
