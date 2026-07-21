using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Publishes one directly observed HumanSighting as Local Persistent
    /// knowledge. A new direct sample replaces every prior occurrence whose
    /// payload names this Sensor's Neuron; foreign copied sightings remain.
    /// Losing direct sight does not erase the last factual observation.
    /// </summary>
    public sealed class KLEPHumanSightingSensorExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition humanSightingDefinition;
        private KLEPHumanSighting observation;

        public KLEPHumanSightingSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition humanSightingDefinition,
            string observerNeuronId)
            : base(definition)
        {
            KLEPZombieBehaviorValidation.RequireExecutableShape(
                definition,
                KLEPExecutableKind.Sensor,
                KLEPExecutionMode.Tandem,
                "A HumanSighting sensor");
            KLEPHumanSightingBehaviorValidation.RequireLocalPersistent(
                humanSightingDefinition,
                nameof(humanSightingDefinition),
                "HumanSighting");
            KLEPHumanSightingBehaviorValidation.RequireSingleExactDeclaredOutput(
                definition,
                humanSightingDefinition,
                "A HumanSighting sensor");
            if (string.IsNullOrWhiteSpace(observerNeuronId))
            {
                throw new ArgumentException(
                    "A non-empty observer Neuron ID is required.",
                    nameof(observerNeuronId));
            }

            this.humanSightingDefinition = humanSightingDefinition;
            ObserverNeuronId = observerNeuronId;
        }

        public string ObserverNeuronId { get; }
        public KLEPKeyDefinition HumanSightingDefinition =>
            humanSightingDefinition;
        public KLEPHumanSighting Observation => observation;

        public void SetObservation(KLEPHumanSighting nextObservation)
        {
            if (nextObservation != null &&
                !StringComparer.Ordinal.Equals(
                    nextObservation.ObserverNeuronId,
                    ObserverNeuronId))
            {
                throw new ArgumentException(
                    $"HumanSighting sensor '{StableId}' belongs to Neuron " +
                    $"'{ObserverNeuronId}', not observer " +
                    $"'{nextObservation.ObserverNeuronId}'.",
                    nameof(nextObservation));
            }

            observation = nextObservation;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observation == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            IReadOnlyList<KLEPKeyFact> facts =
                context.Keys.FindAll(humanSightingDefinition.Id);
            var directFacts = new List<KLEPKeyFact>();
            long newestPriorDirectTick = -1;
            foreach (KLEPKeyFact fact in facts)
            {
                KLEPHumanSighting existing =
                    KLEPHumanSighting.Read(fact.Payload);
                if (StringComparer.Ordinal.Equals(
                        existing.ObserverNeuronId,
                        ObserverNeuronId))
                {
                    directFacts.Add(fact);
                    if (existing.ObservedWorldTick > newestPriorDirectTick)
                    {
                        newestPriorDirectTick = existing.ObservedWorldTick;
                    }
                }
            }

            if (observation.ObservedWorldTick < newestPriorDirectTick)
            {
                throw new InvalidOperationException(
                    $"HumanSighting sensor '{StableId}' cannot replace its " +
                    $"world Tick {newestPriorDirectTick} direct observation " +
                    $"with older Tick {observation.ObservedWorldTick}.");
            }

            directFacts.Sort((left, right) =>
                left.OccurrenceId.CompareTo(right.OccurrenceId));
            if (directFacts.Count == 0)
            {
                context.Add(
                    humanSightingDefinition,
                    observation.ToPayload());
            }
            else
            {
                context.Replace(directFacts[0], observation.ToPayload());
                for (int index = 1; index < directFacts.Count; index++)
                {
                    context.Remove(directFacts[index]);
                }
            }

            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
