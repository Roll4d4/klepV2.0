using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// One immutable, Unity-free enemy sample. Entity identity remains a stable
    /// text handle; a Unity adapter may resolve that handle after the Agent Tick.
    /// </summary>
    public sealed class KLEPEnemyObservation
    {
        public const string EntityIdField = "entityId";
        public const string TeamIdField = "teamId";
        public const string DistanceField = "distance";
        public const string PositionXField = "positionX";
        public const string PositionYField = "positionY";
        public const string PositionZField = "positionZ";

        public KLEPEnemyObservation(
            string entityId,
            string teamId,
            double distance,
            double positionX,
            double positionY,
            double positionZ)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                throw new ArgumentException(
                    "A non-empty stable enemy entity ID is required.",
                    nameof(entityId));
            }

            RequireFiniteNonNegative(distance, nameof(distance));
            RequireFinite(positionX, nameof(positionX));
            RequireFinite(positionY, nameof(positionY));
            RequireFinite(positionZ, nameof(positionZ));

            EntityId = entityId;
            TeamId = teamId ?? string.Empty;
            Distance = distance;
            PositionX = positionX;
            PositionY = positionY;
            PositionZ = positionZ;
        }

        public string EntityId { get; }
        public string TeamId { get; }
        public double Distance { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public double PositionZ { get; }

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(EntityIdField, EntityId),
                new KeyValuePair<string, KLEPKeyValue>(TeamIdField, TeamId),
                new KeyValuePair<string, KLEPKeyValue>(DistanceField, Distance),
                new KeyValuePair<string, KLEPKeyValue>(PositionXField, PositionX),
                new KeyValuePair<string, KLEPKeyValue>(PositionYField, PositionY),
                new KeyValuePair<string, KLEPKeyValue>(PositionZField, PositionZ)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPEnemyObservation observation)
        {
            observation = null;
            if (payload == null ||
                !payload.TryGetText(EntityIdField, out string entityId) ||
                string.IsNullOrWhiteSpace(entityId) ||
                !payload.TryGetText(TeamIdField, out string teamId) ||
                !payload.TryGetNumber(DistanceField, out double distance) ||
                !payload.TryGetNumber(PositionXField, out double positionX) ||
                !payload.TryGetNumber(PositionYField, out double positionY) ||
                !payload.TryGetNumber(PositionZField, out double positionZ))
            {
                return false;
            }

            try
            {
                observation = new KLEPEnemyObservation(
                    entityId,
                    teamId,
                    distance,
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

        public static KLEPEnemyObservation Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPEnemyObservation observation))
            {
                throw new InvalidOperationException(
                    "An enemy observation payload requires a non-empty text " +
                    $"'{EntityIdField}', a text '{TeamIdField}', and finite " +
                    $"numeric '{DistanceField}', '{PositionXField}', " +
                    $"'{PositionYField}', and '{PositionZField}' fields. " +
                    $"'{DistanceField}' cannot be negative.");
            }

            return observation;
        }

        internal static int CompareByEntityId(
            KLEPEnemyObservation left,
            KLEPEnemyObservation right)
        {
            return StringComparer.Ordinal.Compare(left.EntityId, right.EntityId);
        }

        internal static int CompareForTargetSelection(
            KLEPEnemyObservation left,
            KLEPEnemyObservation right)
        {
            int distanceComparison = left.Distance.CompareTo(right.Distance);
            return distanceComparison != 0
                ? distanceComparison
                : StringComparer.Ordinal.Compare(left.EntityId, right.EntityId);
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Enemy observation numbers must be finite.");
            }
        }

        private static void RequireFiniteNonNegative(
            double value,
            string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Enemy observation distance cannot be negative.");
            }
        }
    }

    internal static class KLEPZombieBehaviorValidation
    {
        internal static void RequireExecutableShape(
            KLEPExecutableDefinition definition,
            KLEPExecutableKind kind,
            KLEPExecutionMode mode,
            string behaviorName)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind != kind)
            {
                throw new ArgumentException(
                    $"{behaviorName} requires Executable Kind {kind}.",
                    nameof(definition));
            }

            if (definition.ExecutionMode != mode)
            {
                throw new ArgumentException(
                    $"{behaviorName} requires {mode} execution mode.",
                    nameof(definition));
            }
        }

        internal static void RequireLocalOneCycle(
            KLEPKeyDefinition definition,
            string parameterName,
            string role)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (definition.Scope != KLEPKeyScope.Local ||
                definition.DefaultLifetime != KLEPKeyLifetime.OneCycle)
            {
                throw new ArgumentException(
                    $"{role} Key '{definition.Id}' must be Local and OneCycle.",
                    parameterName);
            }
        }

        internal static void RequireExactDeclaredOutput(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition output,
            string role)
        {
            if (!definition.TryGetDeclaredOutput(
                    output.Id,
                    out KLEPKeyDefinition declared) ||
                !ReferenceEquals(declared, output))
            {
                throw new ArgumentException(
                    $"{role} Key '{output.Id}' must be the exact declared output " +
                    $"of Executable '{definition.StableId}'.",
                    nameof(definition));
            }
        }

        internal static bool TryReadSingleTarget(
            KLEPKeySnapshot keys,
            KLEPKeyDefinition targetDefinition,
            out KLEPEnemyObservation target)
        {
            IReadOnlyList<KLEPKeyFact> facts = keys.FindAll(targetDefinition.Id);
            if (facts.Count == 0)
            {
                target = null;
                return false;
            }

            if (facts.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Zombie action expected exactly one '{targetDefinition.Id}' " +
                    $"target occurrence, but found {facts.Count}.");
            }

            target = KLEPEnemyObservation.Read(facts[0].Payload);
            return true;
        }
    }
}
