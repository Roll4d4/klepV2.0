using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// A continuous Solo edge-recovery state. The pure behavior exposes the
    /// sampled normalized avoidance direction and probe mask for a Unity effect
    /// adapter; it does not access Physics or a Transform.
    /// </summary>
    public sealed class KLEPZombieAvoidEdgeExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition edgeDangerDefinition;

        public KLEPZombieAvoidEdgeExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition edgeDangerDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "A zombie edge-avoidance behavior");
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                edgeDangerDefinition,
                nameof(edgeDangerDefinition),
                "EdgeDanger");
            this.edgeDangerDefinition = edgeDangerDefinition;
        }

        public KLEPKeyDefinition EdgeDangerDefinition => edgeDangerDefinition;
        public KLEPEdgeObservation LastObservation { get; private set; }
        public double LastAvoidanceX => LastObservation == null
            ? 0d
            : LastObservation.AvoidanceX;
        public double LastAvoidanceZ => LastObservation == null
            ? 0d
            : LastObservation.AvoidanceZ;
        public int LastSupportedProbeMask => LastObservation == null
            ? 0
            : LastObservation.SupportedProbeMask;
        public int LastMissingProbeMask => LastObservation == null
            ? 0
            : LastObservation.MissingProbeMask;
        public long IntentCycleIndex { get; private set; } = -1;
        public long IntentRunIndex { get; private set; } = -1;

        public bool TryGetIntent(
            long cycleIndex,
            out KLEPEdgeObservation observation)
        {
            observation = IntentCycleIndex == cycleIndex
                ? LastObservation
                : null;
            return observation != null;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(edgeDangerDefinition.Id);
            if (facts.Count == 0)
            {
                ClearIntent();
                return KLEPExecutableTickStatus.Failed;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    "Zombie edge avoidance expected exactly one '" +
                    $"{edgeDangerDefinition.Id}' occurrence, but found " +
                    $"{facts.Count}.");
            }

            LastObservation = KLEPEdgeObservation.Read(facts[0].Payload);
            IntentCycleIndex = context.CycleIndex;
            IntentRunIndex = context.RunIndex;
            return KLEPExecutableTickStatus.Running;
        }

        private void ClearIntent()
        {
            LastObservation = null;
            IntentCycleIndex = -1;
            IntentRunIndex = -1;
        }
    }
}
