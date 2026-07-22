using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// One immutable, communicable observation of one persistent ammunition
    /// node. World freshness lives only in ObservedWorldTick.
    /// </summary>
    public sealed class KLEPAmmoNodeSighting
    {
        public const int PayloadFieldCount = 7;
        public const string ObservedWorldTickField = "observedWorldTick";
        public const string ObserverNeuronIdField = "observerNeuronId";
        public const string AmmoNodeIdField = "ammoNodeId";
        public const string PositionXField = "positionX";
        public const string PositionYField = "positionY";
        public const string PositionZField = "positionZ";
        public const string SchemaField = "schema";
        public const string Schema = "klep.zombie-test.ammo-node-sighting.v1";

        public KLEPAmmoNodeSighting(
            long observedWorldTick,
            string observerNeuronId,
            string ammoNodeId,
            double positionX,
            double positionY,
            double positionZ)
        {
            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedWorldTick));
            }

            ObservedWorldTick = observedWorldTick;
            ObserverNeuronId = RequireId(
                observerNeuronId,
                nameof(observerNeuronId));
            AmmoNodeId = RequireId(ammoNodeId, nameof(ammoNodeId));
            PositionX = RequireFinite(positionX, nameof(positionX));
            PositionY = RequireFinite(positionY, nameof(positionY));
            PositionZ = RequireFinite(positionZ, nameof(positionZ));
        }

        public long ObservedWorldTick { get; }
        public string ObserverNeuronId { get; }
        public string AmmoNodeId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public double PositionZ { get; }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                Pair(SchemaField, Schema),
                Pair(ObservedWorldTickField, ObservedWorldTick),
                Pair(ObserverNeuronIdField, ObserverNeuronId),
                Pair(AmmoNodeIdField, AmmoNodeId),
                Pair(PositionXField, PositionX),
                Pair(PositionYField, PositionY),
                Pair(PositionZField, PositionZ)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPAmmoNodeSighting sighting)
        {
            sighting = null;
            if (payload == null ||
                payload.Count != PayloadFieldCount ||
                !payload.TryGetText(SchemaField, out string schema) ||
                !StringComparer.Ordinal.Equals(schema, Schema) ||
                !payload.TryGetInteger(
                    ObservedWorldTickField,
                    out long observedWorldTick) ||
                !payload.TryGetText(
                    ObserverNeuronIdField,
                    out string observerNeuronId) ||
                !payload.TryGetText(AmmoNodeIdField, out string ammoNodeId) ||
                !payload.TryGetNumber(PositionXField, out double positionX) ||
                !payload.TryGetNumber(PositionYField, out double positionY) ||
                !payload.TryGetNumber(PositionZField, out double positionZ))
            {
                return false;
            }

            try
            {
                sighting = new KLEPAmmoNodeSighting(
                    observedWorldTick,
                    observerNeuronId,
                    ammoNodeId,
                    positionX,
                    positionY,
                    positionZ);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static KLEPAmmoNodeSighting Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPAmmoNodeSighting sighting))
            {
                throw new InvalidOperationException(
                    "AmmoNodeSighting requires its exact V1 schema, a " +
                    "nonnegative observedWorldTick, stable observer/node " +
                    "identities, and finite XYZ coordinates.");
            }

            return sighting;
        }

        public bool HasSameObservation(KLEPAmmoNodeSighting other)
        {
            return other != null &&
                ObservedWorldTick == other.ObservedWorldTick &&
                StringComparer.Ordinal.Equals(
                    ObserverNeuronId,
                    other.ObserverNeuronId) &&
                StringComparer.Ordinal.Equals(AmmoNodeId, other.AmmoNodeId) &&
                PositionX == other.PositionX &&
                PositionY == other.PositionY &&
                PositionZ == other.PositionZ;
        }

        private static KeyValuePair<string, KLEPKeyValue> Pair(
            string key,
            KLEPKeyValue value)
        {
            return new KeyValuePair<string, KLEPKeyValue>(key, value);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Ammo-node knowledge requires non-empty stable identities.",
                    parameterName);
            }

            return value;
        }

        private static double RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Publishes all directly observed nodes while retaining old direct facts
    /// for nodes outside the current view and every copied foreign fact.
    /// </summary>
    public sealed class KLEPAmmoNodeSightingSensorExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition output;
        private readonly List<KLEPAmmoNodeSighting> observations =
            new List<KLEPAmmoNodeSighting>();

        public KLEPAmmoNodeSightingSensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition outputDefinition,
            string observerNeuronId)
            : base(definition)
        {
            RequireSensorShape(
                definition,
                outputDefinition,
                KLEPKeyLifetime.Persistent,
                "AmmoNodeSighting");
            if (string.IsNullOrWhiteSpace(observerNeuronId))
            {
                throw new ArgumentException(
                    "AmmoNodeSighting requires an observer Neuron ID.",
                    nameof(observerNeuronId));
            }

            output = outputDefinition;
            ObserverNeuronId = observerNeuronId;
        }

        public string ObserverNeuronId { get; }
        public IReadOnlyList<KLEPAmmoNodeSighting> Observations => observations;

        public void SetObservations(
            IReadOnlyList<KLEPAmmoNodeSighting> nextObservations)
        {
            if (nextObservations == null)
            {
                throw new ArgumentNullException(nameof(nextObservations));
            }

            observations.Clear();
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < nextObservations.Count; index++)
            {
                KLEPAmmoNodeSighting observation = nextObservations[index] ??
                    throw new ArgumentException(
                        "AmmoNodeSighting observations cannot contain null.",
                        nameof(nextObservations));
                if (!StringComparer.Ordinal.Equals(
                        observation.ObserverNeuronId,
                        ObserverNeuronId) ||
                    !nodeIds.Add(observation.AmmoNodeId))
                {
                    throw new ArgumentException(
                        "One source requires its own observer ID and at most " +
                        "one observation for each node.",
                        nameof(nextObservations));
                }

                observations.Add(observation);
            }

            observations.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.AmmoNodeId,
                right.AmmoNodeId));
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observations.Count == 0)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            IReadOnlyList<KLEPKeyFact> facts = context.Keys.FindAll(output.Id);
            for (int observationIndex = 0;
                 observationIndex < observations.Count;
                 observationIndex++)
            {
                KLEPAmmoNodeSighting observation =
                    observations[observationIndex];
                var matching = new List<KLEPKeyFact>();
                long newestPriorTick = -1;
                for (int factIndex = 0; factIndex < facts.Count; factIndex++)
                {
                    KLEPKeyFact fact = facts[factIndex];
                    KLEPAmmoNodeSighting existing =
                        KLEPAmmoNodeSighting.Read(fact.Payload);
                    if (!StringComparer.Ordinal.Equals(
                            existing.ObserverNeuronId,
                            ObserverNeuronId) ||
                        !StringComparer.Ordinal.Equals(
                            existing.AmmoNodeId,
                            observation.AmmoNodeId))
                    {
                        continue;
                    }

                    matching.Add(fact);
                    if (existing.ObservedWorldTick ==
                            observation.ObservedWorldTick &&
                        !existing.HasSameObservation(observation))
                    {
                        throw new InvalidOperationException(
                            $"AmmoNodeSighting sensor '{StableId}' received " +
                            $"a conflicting world Tick " +
                            $"{observation.ObservedWorldTick} observation " +
                            $"for node '{observation.AmmoNodeId}'.");
                    }

                    newestPriorTick = Math.Max(
                        newestPriorTick,
                        existing.ObservedWorldTick);
                }

                if (observation.ObservedWorldTick < newestPriorTick)
                {
                    throw new InvalidOperationException(
                        $"AmmoNodeSighting sensor '{StableId}' cannot replace " +
                        $"node '{observation.AmmoNodeId}' world Tick " +
                        $"{newestPriorTick} with older Tick " +
                        $"{observation.ObservedWorldTick}.");
                }

                matching.Sort((left, right) =>
                    left.OccurrenceId.CompareTo(right.OccurrenceId));
                if (matching.Count == 0)
                {
                    context.Add(output, observation.ToPayload());
                }
                else
                {
                    context.Replace(matching[0], observation.ToPayload());
                    for (int index = 1; index < matching.Count; index++)
                    {
                        context.Remove(matching[index]);
                    }
                }
            }

            return KLEPExecutableTickStatus.Succeeded;
        }

        internal static void RequireSensorShape(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition outputDefinition,
            KLEPKeyLifetime lifetime,
            string role)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (outputDefinition == null)
            {
                throw new ArgumentNullException(nameof(outputDefinition));
            }

            if (definition.Kind != KLEPExecutableKind.Sensor ||
                definition.ExecutionMode != KLEPExecutionMode.Tandem ||
                definition.DeclaredOutputs.Count != 1 ||
                definition.DeclaredOutputs[0].Id != outputDefinition.Id ||
                outputDefinition.Scope != KLEPKeyScope.Local ||
                outputDefinition.DefaultLifetime != lifetime ||
                outputDefinition.DefaultPayload.Count != 0)
            {
                throw new ArgumentException(
                    $"{role} requires one empty-default Local {lifetime} " +
                    "output from a Tandem Sensor.",
                    nameof(definition));
            }
        }
    }

    /// <summary>
    /// Publishes one same-cycle factual proximity observation.
    /// </summary>
    public sealed class KLEPAmmoNodeNearbySensorExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition output;
        private KLEPAmmoNodeSighting observation;

        public KLEPAmmoNodeNearbySensorExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition outputDefinition)
            : base(definition)
        {
            KLEPAmmoNodeSightingSensorExecutable.RequireSensorShape(
                definition,
                outputDefinition,
                KLEPKeyLifetime.OneCycle,
                "AmmoNodeNearby");
            output = outputDefinition;
        }

        public void SetObservation(KLEPAmmoNodeSighting nextObservation)
        {
            observation = nextObservation;
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            if (observation == null)
            {
                return KLEPExecutableTickStatus.Failed;
            }

            context.Add(output, observation.ToPayload());
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
