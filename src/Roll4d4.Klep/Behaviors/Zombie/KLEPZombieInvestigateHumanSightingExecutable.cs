using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Continuous Solo intent that moves toward the immutable position carried
    /// by the current BestHumanSighting. A Unity sink may apply this intent
    /// only for the exact completed Agent cycle.
    /// </summary>
    public sealed class KLEPZombieInvestigateHumanSightingExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition bestHumanSightingDefinition;

        public KLEPZombieInvestigateHumanSightingExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition bestHumanSightingDefinition)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Action,
                KLEPExecutionMode.Solo,
                "An Investigate Human Sighting behavior");
            KLEPHumanSightingBehaviorValidation.RequireLocalOneCycle(
                bestHumanSightingDefinition,
                nameof(bestHumanSightingDefinition),
                "BestHumanSighting");
            this.bestHumanSightingDefinition = bestHumanSightingDefinition;
        }

        public KLEPKeyDefinition BestHumanSightingDefinition =>
            bestHumanSightingDefinition;
        public KLEPHumanSighting LastTarget { get; private set; }
        public long IntentCycleIndex { get; private set; } = -1;
        public long IntentRunIndex { get; private set; } = -1;

        public bool TryGetIntent(
            long cycleIndex,
            out KLEPHumanSighting target)
        {
            target = IntentCycleIndex == cycleIndex ? LastTarget : null;
            return target != null;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(bestHumanSightingDefinition.Id);
            if (facts.Count == 0)
            {
                ClearIntent();
                return KLEPExecutableTickStatus.Failed;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    "Zombie investigation expected exactly one '" +
                    $"{bestHumanSightingDefinition.Id}' occurrence, but found " +
                    $"{facts.Count}.");
            }

            LastTarget = KLEPHumanSighting.Read(facts[0].Payload);
            IntentCycleIndex = context.CycleIndex;
            IntentRunIndex = context.RunIndex;
            return KLEPExecutableTickStatus.Running;
        }

        private void ClearIntent()
        {
            LastTarget = null;
            IntentCycleIndex = -1;
            IntentRunIndex = -1;
        }
    }
}
