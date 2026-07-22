using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Behaviors
{
    /// <summary>
    /// Compile-safe command emitted by a successful Fire Action. It freezes the
    /// complete observation used by the decision; only a trusted world adapter
    /// may interpret the command and publish what actually happened.
    /// </summary>
    public sealed class KLEPWeaponFireIntent
    {
        public KLEPWeaponFireIntent(KLEPWeaponObservation observation)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));
        }

        public KLEPWeaponObservation Observation { get; }
        public string WeaponId => Observation.WeaponId;
        public string ShooterEntityId => Observation.ShooterEntityId;
        public string ShooterTeamId => Observation.ShooterTeamId;
        public long ObservedWorldTick => Observation.ObservedWorldTick;

        public KLEPKeyPayload ToPayload()
        {
            return Observation.ToPayload(KLEPWeaponObservation.FireIntentSchema);
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPWeaponFireIntent intent)
        {
            intent = null;
            if (!KLEPWeaponObservation.TryRead(
                    payload,
                    KLEPWeaponObservation.FireIntentSchema,
                    out KLEPWeaponObservation observation))
            {
                return false;
            }

            intent = new KLEPWeaponFireIntent(observation);
            return true;
        }

        public static KLEPWeaponFireIntent Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPWeaponFireIntent intent))
            {
                throw new InvalidOperationException(
                    "A WeaponFireIntent payload must exactly match the closed " +
                    $"'{KLEPWeaponObservation.FireIntentSchema}' schema.");
            }

            return intent;
        }
    }

    /// <summary>
    /// Compile-safe command emitted by a successful Reload Action. Capacity and
    /// ammunition are copied from the deciding observation; the trusted Unity
    /// adapter remains authoritative over the actual ammunition state.
    /// </summary>
    public sealed class KLEPWeaponReloadIntent
    {
        public const string ReloadIntentSchema =
            "klep.weapon.reload-intent.v1";
        public const int PayloadFieldCount = 8;

        public KLEPWeaponReloadIntent(
            string weaponId,
            string shooterEntityId,
            string shooterTeamId,
            long observedWorldTick,
            int magazineCapacity,
            int magazineRounds,
            int reserveRounds)
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
                throw new ArgumentOutOfRangeException(nameof(observedWorldTick));
            }

            if (magazineCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineCapacity));
            }

            if (magazineRounds < 0 || magazineRounds > magazineCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(magazineRounds));
            }

            if (reserveRounds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserveRounds));
            }

            ObservedWorldTick = observedWorldTick;
            MagazineCapacity = magazineCapacity;
            MagazineRounds = magazineRounds;
            ReserveRounds = reserveRounds;
        }

        public KLEPWeaponReloadIntent(KLEPWeaponObservation observation)
            : this(
                (observation ?? throw new ArgumentNullException(
                    nameof(observation))).WeaponId,
                observation.ShooterEntityId,
                observation.ShooterTeamId,
                observation.ObservedWorldTick,
                observation.MagazineCapacity,
                observation.MagazineRounds,
                observation.ReserveRounds)
        {
        }

        public string WeaponId { get; }
        public string ShooterEntityId { get; }
        public string ShooterTeamId { get; }
        public long ObservedWorldTick { get; }
        public int MagazineCapacity { get; }
        public int MagazineRounds { get; }
        public int ReserveRounds { get; }
        public bool CanReload => MagazineRounds < MagazineCapacity &&
            ReserveRounds > 0;

        public KLEPKeyPayload ToPayload()
        {
            return new KLEPKeyPayload(new[]
            {
                Field(KLEPWeaponObservation.SchemaField, ReloadIntentSchema),
                Field(KLEPWeaponObservation.WeaponIdField, WeaponId),
                Field(
                    KLEPWeaponObservation.ShooterEntityIdField,
                    ShooterEntityId),
                Field(
                    KLEPWeaponObservation.ShooterTeamIdField,
                    ShooterTeamId),
                Field(
                    KLEPWeaponObservation.ObservedWorldTickField,
                    ObservedWorldTick),
                Field(
                    KLEPWeaponObservation.MagazineCapacityField,
                    MagazineCapacity),
                Field(
                    KLEPWeaponObservation.MagazineRoundsField,
                    MagazineRounds),
                Field(
                    KLEPWeaponObservation.ReserveRoundsField,
                    ReserveRounds)
            });
        }

        public static bool TryRead(
            KLEPKeyPayload payload,
            out KLEPWeaponReloadIntent intent)
        {
            intent = null;
            if (payload == null ||
                payload.Count != PayloadFieldCount ||
                !payload.TryGetText(
                    KLEPWeaponObservation.SchemaField,
                    out string schema) ||
                !StringComparer.Ordinal.Equals(schema, ReloadIntentSchema) ||
                !payload.TryGetText(
                    KLEPWeaponObservation.WeaponIdField,
                    out string weaponId) ||
                !payload.TryGetText(
                    KLEPWeaponObservation.ShooterEntityIdField,
                    out string shooterEntityId) ||
                !payload.TryGetText(
                    KLEPWeaponObservation.ShooterTeamIdField,
                    out string shooterTeamId) ||
                !payload.TryGetInteger(
                    KLEPWeaponObservation.ObservedWorldTickField,
                    out long observedWorldTick) ||
                !payload.TryGetInteger(
                    KLEPWeaponObservation.MagazineCapacityField,
                    out long magazineCapacity) ||
                !payload.TryGetInteger(
                    KLEPWeaponObservation.MagazineRoundsField,
                    out long magazineRounds) ||
                !payload.TryGetInteger(
                    KLEPWeaponObservation.ReserveRoundsField,
                    out long reserveRounds) ||
                magazineCapacity > int.MaxValue ||
                magazineCapacity < int.MinValue ||
                magazineRounds > int.MaxValue ||
                magazineRounds < int.MinValue ||
                reserveRounds > int.MaxValue ||
                reserveRounds < int.MinValue)
            {
                return false;
            }

            try
            {
                intent = new KLEPWeaponReloadIntent(
                    weaponId,
                    shooterEntityId,
                    shooterTeamId,
                    observedWorldTick,
                    (int)magazineCapacity,
                    (int)magazineRounds,
                    (int)reserveRounds);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static KLEPWeaponReloadIntent Read(KLEPKeyPayload payload)
        {
            if (!TryRead(payload, out KLEPWeaponReloadIntent intent))
            {
                throw new InvalidOperationException(
                    "A WeaponReloadIntent payload must exactly match the closed " +
                    $"'{ReloadIntentSchema}' schema.");
            }

            return intent;
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
    }
}
