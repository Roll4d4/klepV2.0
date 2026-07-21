using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Immutable communicable knowledge that one Neuron observed one human at
    /// an explicit world Tick. Observation freshness lives only in the copied
    /// payload; Key-store IssuedTick and ActivatedTick are not world evidence.
    /// </summary>
    public sealed class KLEPHumanSighting
    {
        public const string ObservedWorldTickField = "observedWorldTick";
        public const string ObserverNeuronIdField = "observerNeuronId";
        public const string EntityIdField = "entityId";
        public const string TeamIdField = "teamId";
        public const string PositionXField = "positionX";
        public const string PositionYField = "positionY";
        public const string PositionZField = "positionZ";

        public KLEPHumanSighting(
            long observedWorldTick,
            string observerNeuronId,
            string entityId,
            string teamId,
            double positionX,
            double positionY,
            double positionZ)
        {
            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedWorldTick),
                    "A HumanSighting world Tick cannot be negative.");
            }

            ObservedWorldTick = observedWorldTick;
            ObserverNeuronId = RequireId(
                observerNeuronId,
                nameof(observerNeuronId),
                "observer Neuron");
            EntityId = RequireId(entityId, nameof(entityId), "human entity");
            TeamId = RequireId(teamId, nameof(teamId), "human team");
            PositionX = RequireFinite(positionX, nameof(positionX));
            PositionY = RequireFinite(positionY, nameof(positionY));
            PositionZ = RequireFinite(positionZ, nameof(positionZ));
        }

        public long ObservedWorldTick { get; }
        public string ObserverNeuronId { get; }
        public string EntityId { get; }
        public string TeamId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public double PositionZ { get; }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(
                    ObservedWorldTickField,
                    ObservedWorldTick),
                new KeyValuePair<string, KLEPKeyValue>(
                    ObserverNeuronIdField,
                    ObserverNeuronId),
                new KeyValuePair<string, KLEPKeyValue>(EntityIdField, EntityId),
                new KeyValuePair<string, KLEPKeyValue>(TeamIdField, TeamId),
                new KeyValuePair<string, KLEPKeyValue>(PositionXField, PositionX),
                new KeyValuePair<string, KLEPKeyValue>(PositionYField, PositionY),
                new KeyValuePair<string, KLEPKeyValue>(PositionZField, PositionZ)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPHumanSighting sighting)
        {
            sighting = null;
            if (payload == null ||
                !payload.TryGetInteger(
                    ObservedWorldTickField,
                    out long observedWorldTick) ||
                !payload.TryGetText(
                    ObserverNeuronIdField,
                    out string observerNeuronId) ||
                !payload.TryGetText(EntityIdField, out string entityId) ||
                !payload.TryGetText(TeamIdField, out string teamId) ||
                !payload.TryGetNumber(PositionXField, out double positionX) ||
                !payload.TryGetNumber(PositionYField, out double positionY) ||
                !payload.TryGetNumber(PositionZField, out double positionZ))
            {
                return false;
            }

            try
            {
                sighting = new KLEPHumanSighting(
                    observedWorldTick,
                    observerNeuronId,
                    entityId,
                    teamId,
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

        public static KLEPHumanSighting Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPHumanSighting sighting))
            {
                throw new InvalidOperationException(
                    "A HumanSighting payload requires a nonnegative Int64 " +
                    $"'{ObservedWorldTickField}', non-empty text " +
                    $"'{ObserverNeuronIdField}', '{EntityIdField}', and " +
                    $"'{TeamIdField}', plus finite numeric '{PositionXField}', " +
                    $"'{PositionYField}', and '{PositionZField}' fields.");
            }

            return sighting;
        }

        internal bool HasSameObservation(KLEPHumanSighting other)
        {
            return other != null &&
                ObservedWorldTick == other.ObservedWorldTick &&
                StringComparer.Ordinal.Equals(
                    ObserverNeuronId,
                    other.ObserverNeuronId) &&
                StringComparer.Ordinal.Equals(EntityId, other.EntityId) &&
                StringComparer.Ordinal.Equals(TeamId, other.TeamId) &&
                PositionX == other.PositionX &&
                PositionY == other.PositionY &&
                PositionZ == other.PositionZ;
        }

        private static string RequireId(
            string value,
            string parameterName,
            string role)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"A non-empty stable {role} ID is required.",
                    parameterName);
            }

            return value;
        }

        private static double RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "HumanSighting positions must be finite.");
            }

            return value;
        }
    }

    internal static class KLEPHumanSightingBehaviorValidation
    {
        internal static void RequireLocalPersistent(
            KLEPKeyDefinition definition,
            string parameterName,
            string role)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (definition.Scope != KLEPKeyScope.Local ||
                definition.DefaultLifetime != KLEPKeyLifetime.Persistent)
            {
                throw new ArgumentException(
                    $"{role} Key '{definition.Id}' must be Local and Persistent.",
                    parameterName);
            }
        }

        internal static void RequireLocalOneCycle(
            KLEPKeyDefinition definition,
            string parameterName,
            string role)
        {
            KLEPZombieBehaviorValidation.RequireLocalOneCycle(
                definition,
                parameterName,
                role);
        }

        internal static void RequireSingleExactDeclaredOutput(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition output,
            string role)
        {
            if (definition.DeclaredOutputs.Count != 1)
            {
                throw new ArgumentException(
                    $"{role} requires exactly one declared output.",
                    nameof(definition));
            }

            KLEPZombieBehaviorValidation.RequireExactDeclaredOutput(
                definition,
                output,
                role);
        }
    }
}
