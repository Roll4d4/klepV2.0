using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Reduces direct and copied HumanSighting facts to one deterministic,
    /// transient BestHumanSighting. Freshness is derived exclusively from the
    /// payload's observedWorldTick and an explicit host-supplied world Tick.
    /// </summary>
    public sealed class KLEPBestHumanSightingRouterExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition humanSightingDefinition;
        private readonly KLEPKeyDefinition bestHumanSightingDefinition;
        private long currentWorldTick;
        private bool hasWorldTick;

        public KLEPBestHumanSightingRouterExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition humanSightingDefinition,
            KLEPKeyDefinition bestHumanSightingDefinition,
            string receiverNeuronId,
            long maximumSightingAgeTicks = long.MaxValue)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Router,
                KLEPExecutionMode.Tandem,
                "A BestHumanSighting router");
            KLEPHumanSightingBehaviorValidation.RequireLocalPersistent(
                humanSightingDefinition,
                nameof(humanSightingDefinition),
                "HumanSighting");
            KLEPHumanSightingBehaviorValidation.RequireLocalOneCycle(
                bestHumanSightingDefinition,
                nameof(bestHumanSightingDefinition),
                "BestHumanSighting");
            KLEPHumanSightingBehaviorValidation.RequireSingleExactDeclaredOutput(
                definition,
                bestHumanSightingDefinition,
                "A BestHumanSighting router");
            if (humanSightingDefinition.Id == bestHumanSightingDefinition.Id)
            {
                throw new ArgumentException(
                    "HumanSighting and BestHumanSighting require different Key IDs.",
                    nameof(bestHumanSightingDefinition));
            }

            if (string.IsNullOrWhiteSpace(receiverNeuronId))
            {
                throw new ArgumentException(
                    "A non-empty receiver Neuron ID is required.",
                    nameof(receiverNeuronId));
            }

            if (maximumSightingAgeTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSightingAgeTicks),
                    "Maximum sighting age cannot be negative.");
            }

            this.humanSightingDefinition = humanSightingDefinition;
            this.bestHumanSightingDefinition = bestHumanSightingDefinition;
            ReceiverNeuronId = receiverNeuronId;
            MaximumSightingAgeTicks = maximumSightingAgeTicks;
        }

        public string ReceiverNeuronId { get; }
        public long MaximumSightingAgeTicks { get; }
        public bool HasWorldTick => hasWorldTick;
        public long CurrentWorldTick => currentWorldTick;
        public KLEPKeyDefinition HumanSightingDefinition =>
            humanSightingDefinition;
        public KLEPKeyDefinition BestHumanSightingDefinition =>
            bestHumanSightingDefinition;
        public KLEPHumanSighting LastSelectedSighting { get; private set; }
        public KLEPKeyOccurrenceId? LastSelectedOccurrenceId { get; private set; }

        public void SetWorldTick(long worldTick)
        {
            if (worldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "World Tick cannot be negative.");
            }

            currentWorldTick = worldTick;
            hasWorldTick = true;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (!hasWorldTick)
            {
                throw new InvalidOperationException(
                    $"BestHumanSighting router '{StableId}' requires an explicit " +
                    "world Tick before every Agent Tick.");
            }

            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(humanSightingDefinition.Id);
            Candidate selected = null;
            foreach (KLEPKeyFact fact in facts)
            {
                KLEPHumanSighting sighting =
                    KLEPHumanSighting.Read(fact.Payload);
                if (sighting.ObservedWorldTick > currentWorldTick)
                {
                    throw new InvalidOperationException(
                        $"HumanSighting occurrence '{fact.OccurrenceId}' claims " +
                        $"future world Tick {sighting.ObservedWorldTick} while " +
                        $"the selector is at {currentWorldTick}.");
                }

                long age = currentWorldTick - sighting.ObservedWorldTick;
                if (age > MaximumSightingAgeTicks)
                {
                    continue;
                }

                var candidate = new Candidate(fact, sighting);
                if (selected == null || Compare(candidate, selected) < 0)
                {
                    selected = candidate;
                }
            }

            if (selected == null)
            {
                LastSelectedSighting = null;
                LastSelectedOccurrenceId = null;
                return KLEPExecutableTickStatus.Failed;
            }

            LastSelectedSighting = selected.Sighting;
            LastSelectedOccurrenceId = selected.Fact.OccurrenceId;
            // Preserve the selected occurrence's complete opaque payload. The
            // parser above validates the required fields but extensions remain.
            context.Add(
                bestHumanSightingDefinition,
                selected.Fact.Payload);
            return KLEPExecutableTickStatus.Succeeded;
        }

        private int Compare(Candidate left, Candidate right)
        {
            int tickComparison = right.Sighting.ObservedWorldTick.CompareTo(
                left.Sighting.ObservedWorldTick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            bool leftIsOwn = StringComparer.Ordinal.Equals(
                left.Sighting.ObserverNeuronId,
                ReceiverNeuronId);
            bool rightIsOwn = StringComparer.Ordinal.Equals(
                right.Sighting.ObserverNeuronId,
                ReceiverNeuronId);
            if (leftIsOwn != rightIsOwn)
            {
                return leftIsOwn ? -1 : 1;
            }

            int observerComparison = StringComparer.Ordinal.Compare(
                left.Sighting.ObserverNeuronId,
                right.Sighting.ObserverNeuronId);
            if (observerComparison != 0)
            {
                return observerComparison;
            }

            int entityComparison = StringComparer.Ordinal.Compare(
                left.Sighting.EntityId,
                right.Sighting.EntityId);
            return entityComparison != 0
                ? entityComparison
                : left.Fact.OccurrenceId.CompareTo(right.Fact.OccurrenceId);
        }

        private sealed class Candidate
        {
            internal Candidate(KLEPKeyFact fact, KLEPHumanSighting sighting)
            {
                Fact = fact;
                Sighting = sighting;
            }

            internal KLEPKeyFact Fact { get; }
            internal KLEPHumanSighting Sighting { get; }
        }
    }
}
