using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Structural classification of the first trusted world hit along a
    /// weapon's sampled aim ray.
    /// </summary>
    public enum KLEPWeaponLineHitKind
    {
        None = 0,
        Obstacle = 1,
        Entity = 2
    }

    /// <summary>
    /// One immutable, Unity-free sample of a weapon, its ammunition, and the
    /// first thing on its aim line. Freshness is the copied ObservedWorldTick;
    /// Key-store clocks are not world evidence.
    /// </summary>
    public sealed class KLEPWeaponObservation
    {
        public const string ObservationSchema =
            "klep.weapon.observation.v1";
        internal const string FireIntentSchema =
            "klep.weapon.fire-intent.v1";

        public const string SchemaField = "schema";
        public const string WeaponIdField = "weaponId";
        public const string ShooterEntityIdField = "shooterEntityId";
        public const string ShooterTeamIdField = "shooterTeamId";
        public const string ObservedWorldTickField = "observedWorldTick";
        public const string MagazineCapacityField = "magazineCapacity";
        public const string MagazineRoundsField = "magazineRounds";
        public const string ReserveRoundsField = "reserveRounds";
        public const string OriginXField = "originX";
        public const string OriginYField = "originY";
        public const string OriginZField = "originZ";
        public const string DirectionXField = "directionX";
        public const string DirectionYField = "directionY";
        public const string DirectionZField = "directionZ";
        public const string MaximumDistanceField = "maximumDistance";
        public const string HitKindField = "hitKind";
        public const string HitEntityIdField = "hitEntityId";
        public const string HitTeamIdField = "hitTeamId";
        public const string HitDistanceField = "hitDistance";

        public const int PayloadFieldCount = 19;

        public KLEPWeaponObservation(
            string weaponId,
            string shooterEntityId,
            string shooterTeamId,
            long observedWorldTick,
            int magazineCapacity,
            int magazineRounds,
            int reserveRounds,
            double originX,
            double originY,
            double originZ,
            double directionX,
            double directionY,
            double directionZ,
            double maximumDistance,
            KLEPWeaponLineHitKind hitKind,
            string hitEntityId,
            string hitTeamId,
            double hitDistance)
        {
            WeaponId = RequireId(weaponId, nameof(weaponId), "weapon");
            ShooterEntityId = RequireId(
                shooterEntityId,
                nameof(shooterEntityId),
                "shooter entity");
            ShooterTeamId = RequireId(
                shooterTeamId,
                nameof(shooterTeamId),
                "shooter team");
            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedWorldTick),
                    "A weapon observation world Tick cannot be negative.");
            }

            if (magazineCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magazineCapacity),
                    "Magazine capacity must be positive.");
            }

            if (magazineRounds < 0 || magazineRounds > magazineCapacity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magazineRounds),
                    "Magazine rounds must be within the magazine capacity.");
            }

            if (reserveRounds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reserveRounds),
                    "Reserve rounds cannot be negative.");
            }

            RequireFinite(originX, nameof(originX));
            RequireFinite(originY, nameof(originY));
            RequireFinite(originZ, nameof(originZ));
            RequireFinite(directionX, nameof(directionX));
            RequireFinite(directionY, nameof(directionY));
            RequireFinite(directionZ, nameof(directionZ));
            double directionMagnitudeSquared =
                directionX * directionX +
                directionY * directionY +
                directionZ * directionZ;
            if (Math.Abs(directionMagnitudeSquared - 1d) > 0.000001d)
            {
                throw new ArgumentException(
                    "A weapon observation direction must be normalized.",
                    nameof(directionX));
            }

            RequireFinite(maximumDistance, nameof(maximumDistance));
            if (maximumDistance <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDistance),
                    "Weapon maximum distance must be positive.");
            }

            if (!Enum.IsDefined(typeof(KLEPWeaponLineHitKind), hitKind))
            {
                throw new ArgumentOutOfRangeException(nameof(hitKind));
            }

            RequireFinite(hitDistance, nameof(hitDistance));
            if (hitDistance < 0d || hitDistance > maximumDistance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hitDistance),
                    "Hit distance must lie on the sampled aim segment.");
            }

            string normalizedHitEntityId = hitEntityId ?? string.Empty;
            string normalizedHitTeamId = hitTeamId ?? string.Empty;
            if (hitKind == KLEPWeaponLineHitKind.Entity)
            {
                RequireId(
                    normalizedHitEntityId,
                    nameof(hitEntityId),
                    "hit entity");
                RequireId(
                    normalizedHitTeamId,
                    nameof(hitTeamId),
                    "hit team");
            }
            else if (normalizedHitEntityId.Length != 0 ||
                     normalizedHitTeamId.Length != 0)
            {
                throw new ArgumentException(
                    "Only an Entity line hit may carry hit entity or team IDs.",
                    nameof(hitEntityId));
            }

            if (hitKind == KLEPWeaponLineHitKind.None &&
                hitDistance != maximumDistance)
            {
                throw new ArgumentException(
                    "A no-hit observation uses maximum distance as its sampled endpoint.",
                    nameof(hitDistance));
            }

            ObservedWorldTick = observedWorldTick;
            MagazineCapacity = magazineCapacity;
            MagazineRounds = magazineRounds;
            ReserveRounds = reserveRounds;
            OriginX = originX;
            OriginY = originY;
            OriginZ = originZ;
            DirectionX = directionX;
            DirectionY = directionY;
            DirectionZ = directionZ;
            MaximumDistance = maximumDistance;
            HitKind = hitKind;
            HitEntityId = normalizedHitEntityId;
            HitTeamId = normalizedHitTeamId;
            HitDistance = hitDistance;
        }

        public string WeaponId { get; }
        public string ShooterEntityId { get; }
        public string ShooterTeamId { get; }
        public long ObservedWorldTick { get; }
        public int MagazineCapacity { get; }
        public int MagazineRounds { get; }
        public int ReserveRounds { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double OriginZ { get; }
        public double DirectionX { get; }
        public double DirectionY { get; }
        public double DirectionZ { get; }
        public double MaximumDistance { get; }
        public KLEPWeaponLineHitKind HitKind { get; }
        public string HitEntityId { get; }
        public string HitTeamId { get; }
        public double HitDistance { get; }
        public bool IsLoaded => MagazineRounds > 0;
        public bool HasEntityHit => HitKind == KLEPWeaponLineHitKind.Entity;
        public bool HasFriendlyEntityHit => HasEntityHit &&
            StringComparer.Ordinal.Equals(ShooterTeamId, HitTeamId);
        public bool HasSafeLine => HasEntityHit &&
            !StringComparer.Ordinal.Equals(ShooterTeamId, HitTeamId);
        public bool CanReload => MagazineRounds < MagazineCapacity &&
            ReserveRounds > 0;

        public KLEPKeyPayload ToPayload()
        {
            return ToPayload(ObservationSchema);
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPWeaponObservation observation)
        {
            return TryRead(payload, ObservationSchema, out observation);
        }

        public static KLEPWeaponObservation Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPWeaponObservation observation))
            {
                throw new InvalidOperationException(
                    "A WeaponObservation payload must exactly match the closed " +
                    $"'{ObservationSchema}' schema with finite normalized aim, " +
                    "valid ammunition counts, and coherent first-hit evidence.");
            }

            return observation;
        }

        internal KLEPKeyPayload ToPayload(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                throw new ArgumentException(
                    "A non-empty weapon payload schema is required.",
                    nameof(schema));
            }

            return new KLEPKeyPayload(new[]
            {
                Field(SchemaField, schema),
                Field(WeaponIdField, WeaponId),
                Field(ShooterEntityIdField, ShooterEntityId),
                Field(ShooterTeamIdField, ShooterTeamId),
                Field(ObservedWorldTickField, ObservedWorldTick),
                Field(MagazineCapacityField, MagazineCapacity),
                Field(MagazineRoundsField, MagazineRounds),
                Field(ReserveRoundsField, ReserveRounds),
                Field(OriginXField, OriginX),
                Field(OriginYField, OriginY),
                Field(OriginZField, OriginZ),
                Field(DirectionXField, DirectionX),
                Field(DirectionYField, DirectionY),
                Field(DirectionZField, DirectionZ),
                Field(MaximumDistanceField, MaximumDistance),
                Field(HitKindField, (int)HitKind),
                Field(HitEntityIdField, HitEntityId),
                Field(HitTeamIdField, HitTeamId),
                Field(HitDistanceField, HitDistance)
            });
        }

        internal static bool TryRead(
            KLEPKeyPayload payload,
            string schema,
            out KLEPWeaponObservation observation)
        {
            observation = null;
            if (payload == null ||
                payload.Count != PayloadFieldCount ||
                !payload.TryGetText(SchemaField, out string payloadSchema) ||
                !StringComparer.Ordinal.Equals(payloadSchema, schema) ||
                !payload.TryGetText(WeaponIdField, out string weaponId) ||
                !payload.TryGetText(
                    ShooterEntityIdField,
                    out string shooterEntityId) ||
                !payload.TryGetText(
                    ShooterTeamIdField,
                    out string shooterTeamId) ||
                !payload.TryGetInteger(
                    ObservedWorldTickField,
                    out long observedWorldTick) ||
                !payload.TryGetInteger(
                    MagazineCapacityField,
                    out long magazineCapacity) ||
                !payload.TryGetInteger(
                    MagazineRoundsField,
                    out long magazineRounds) ||
                !payload.TryGetInteger(
                    ReserveRoundsField,
                    out long reserveRounds) ||
                !payload.TryGetNumber(OriginXField, out double originX) ||
                !payload.TryGetNumber(OriginYField, out double originY) ||
                !payload.TryGetNumber(OriginZField, out double originZ) ||
                !payload.TryGetNumber(DirectionXField, out double directionX) ||
                !payload.TryGetNumber(DirectionYField, out double directionY) ||
                !payload.TryGetNumber(DirectionZField, out double directionZ) ||
                !payload.TryGetNumber(
                    MaximumDistanceField,
                    out double maximumDistance) ||
                !payload.TryGetInteger(HitKindField, out long hitKind) ||
                !payload.TryGetText(HitEntityIdField, out string hitEntityId) ||
                !payload.TryGetText(HitTeamIdField, out string hitTeamId) ||
                !payload.TryGetNumber(HitDistanceField, out double hitDistance) ||
                magazineCapacity > int.MaxValue ||
                magazineCapacity < int.MinValue ||
                magazineRounds > int.MaxValue ||
                magazineRounds < int.MinValue ||
                reserveRounds > int.MaxValue ||
                reserveRounds < int.MinValue ||
                hitKind > int.MaxValue ||
                hitKind < int.MinValue)
            {
                return false;
            }

            try
            {
                observation = new KLEPWeaponObservation(
                    weaponId,
                    shooterEntityId,
                    shooterTeamId,
                    observedWorldTick,
                    (int)magazineCapacity,
                    (int)magazineRounds,
                    (int)reserveRounds,
                    originX,
                    originY,
                    originZ,
                    directionX,
                    directionY,
                    directionZ,
                    maximumDistance,
                    (KLEPWeaponLineHitKind)(int)hitKind,
                    hitEntityId,
                    hitTeamId,
                    hitDistance);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static KeyValuePair<string, KLEPKeyValue> Field(
            string name,
            KLEPKeyValue value)
        {
            return new KeyValuePair<string, KLEPKeyValue>(name, value);
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

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Weapon observation numbers must be finite.");
            }
        }
    }
}
