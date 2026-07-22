using System;
using Roll4d4.Klep.Behaviors;

namespace Roll4d4.Klep.Unity
{
    /// <summary>
    /// Immutable evidence that one trusted external source added reserve
    /// rounds before the weapon observation for an explicit world Tick.
    /// </summary>
    public sealed class KLEPWeaponReserveSupplyReceipt
    {
        internal KLEPWeaponReserveSupplyReceipt(
            long observedWorldTick,
            string supplySourceId,
            string weaponId,
            string receiverEntityId,
            string receiverTeamId,
            int magazineCapacity,
            int magazineRounds,
            int reserveBefore,
            int reserveAfter)
        {
            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(observedWorldTick));
            }

            SupplySourceId = RequireId(
                supplySourceId,
                nameof(supplySourceId));
            WeaponId = RequireId(weaponId, nameof(weaponId));
            ReceiverEntityId = RequireId(
                receiverEntityId,
                nameof(receiverEntityId));
            ReceiverTeamId = RequireId(
                receiverTeamId,
                nameof(receiverTeamId));
            if (magazineCapacity <= 0 ||
                magazineRounds < 0 ||
                magazineRounds >= magazineCapacity ||
                reserveBefore != 0 ||
                reserveAfter <= reserveBefore ||
                reserveAfter - reserveBefore !=
                    magazineCapacity - magazineRounds)
            {
                throw new ArgumentException(
                    "External weapon supply must fill exactly one current " +
                    "magazine deficit into an empty reserve.",
                    nameof(reserveAfter));
            }

            ObservedWorldTick = observedWorldTick;
            MagazineCapacity = magazineCapacity;
            MagazineRounds = magazineRounds;
            ReserveBefore = reserveBefore;
            ReserveAfter = reserveAfter;
        }

        public long ObservedWorldTick { get; }
        public string SupplySourceId { get; }
        public string WeaponId { get; }
        public string ReceiverEntityId { get; }
        public string ReceiverTeamId { get; }
        public int MagazineCapacity { get; }
        public int MagazineRounds { get; }
        public int ReserveBefore { get; }
        public int ReserveAfter { get; }
        public int SuppliedRounds => ReserveAfter - ReserveBefore;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A reserve-supply receipt requires stable identities.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Immutable world evidence that one exact Fire intent consumed ammunition
    /// and was traced by the trusted weapon adapter.
    /// </summary>
    public sealed class KLEPWeaponAppliedShotReceipt
    {
        internal KLEPWeaponAppliedShotReceipt(
            long agentCycle,
            string actionStableId,
            long actionRunIndex,
            string weaponId,
            string shooterEntityId,
            string shooterTeamId,
            long observedWorldTick,
            int magazineBefore,
            int magazineAfter,
            KLEPWeaponLineHitKind hitKind,
            string hitEntityId,
            string hitTeamId,
            float hitDistance,
            float healthBefore,
            float healthAfter,
            bool targetKilled,
            bool friendlyFire)
        {
            if (agentCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentCycle));
            }

            ActionStableId = RequireId(
                actionStableId,
                nameof(actionStableId),
                "action");
            if (actionRunIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionRunIndex));
            }

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

            if (magazineBefore <= 0 ||
                magazineAfter != magazineBefore - 1)
            {
                throw new ArgumentException(
                    "An applied shot must consume exactly one magazine round.",
                    nameof(magazineAfter));
            }

            if (!Enum.IsDefined(typeof(KLEPWeaponLineHitKind), hitKind))
            {
                throw new ArgumentOutOfRangeException(nameof(hitKind));
            }

            RequireFiniteNonnegative(hitDistance, nameof(hitDistance));
            RequireFiniteNonnegative(healthBefore, nameof(healthBefore));
            RequireFiniteNonnegative(healthAfter, nameof(healthAfter));
            if (healthAfter > healthBefore)
            {
                throw new ArgumentException(
                    "A shot receipt cannot increase target health.",
                    nameof(healthAfter));
            }

            string normalizedHitEntityId = hitEntityId ?? string.Empty;
            string normalizedHitTeamId = hitTeamId ?? string.Empty;
            bool didHitEntity = hitKind == KLEPWeaponLineHitKind.Entity;
            if (didHitEntity)
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
                     normalizedHitTeamId.Length != 0 ||
                     healthBefore != 0f ||
                     healthAfter != 0f ||
                     targetKilled ||
                     friendlyFire)
            {
                throw new ArgumentException(
                    "Only an entity hit may carry target consequence evidence.",
                    nameof(hitEntityId));
            }

            bool calculatedFriendly = didHitEntity &&
                StringComparer.Ordinal.Equals(
                    shooterTeamId,
                    normalizedHitTeamId);
            if (friendlyFire != calculatedFriendly)
            {
                throw new ArgumentException(
                    "Friendly-fire evidence must match the actual shooter and hit teams.",
                    nameof(friendlyFire));
            }

            if (targetKilled && healthAfter != 0f)
            {
                throw new ArgumentException(
                    "A killed target must have zero health after the shot.",
                    nameof(targetKilled));
            }

            AgentCycle = agentCycle;
            ActionRunIndex = actionRunIndex;
            ObservedWorldTick = observedWorldTick;
            MagazineBefore = magazineBefore;
            MagazineAfter = magazineAfter;
            HitKind = hitKind;
            HitEntityId = normalizedHitEntityId;
            HitTeamId = normalizedHitTeamId;
            HitDistance = hitDistance;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
            TargetKilled = targetKilled;
            FriendlyFire = friendlyFire;
        }

        public long AgentCycle { get; }
        public string ActionStableId { get; }
        public long ActionRunIndex { get; }
        public string WeaponId { get; }
        public string ShooterEntityId { get; }
        public string ShooterTeamId { get; }
        public long ObservedWorldTick { get; }
        public int MagazineBefore { get; }
        public int MagazineAfter { get; }
        public KLEPWeaponLineHitKind HitKind { get; }
        public bool DidHitEntity => HitKind == KLEPWeaponLineHitKind.Entity;
        public string HitEntityId { get; }
        public string HitTeamId { get; }
        public float HitDistance { get; }
        public float HealthBefore { get; }
        public float HealthAfter { get; }
        public float AppliedDamage => HealthBefore - HealthAfter;
        public bool TargetKilled { get; }
        public bool FriendlyFire { get; }

        private static string RequireId(
            string value,
            string parameterName,
            string role)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"An applied shot requires a non-empty {role} ID.",
                    parameterName);
            }

            return value;
        }

        private static void RequireFiniteNonnegative(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// Immutable world evidence that one exact Reload intent transferred
    /// ammunition while conserving the weapon's total round count.
    /// </summary>
    public sealed class KLEPWeaponAppliedReloadReceipt
    {
        internal KLEPWeaponAppliedReloadReceipt(
            long agentCycle,
            string actionStableId,
            long actionRunIndex,
            string weaponId,
            string shooterEntityId,
            string shooterTeamId,
            long observedWorldTick,
            int magazineCapacity,
            int magazineBefore,
            int magazineAfter,
            int reserveBefore,
            int reserveAfter)
        {
            if (agentCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(agentCycle));
            }

            ActionStableId = RequireId(actionStableId, nameof(actionStableId));
            if (actionRunIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionRunIndex));
            }

            WeaponId = RequireId(weaponId, nameof(weaponId));
            ShooterEntityId = RequireId(
                shooterEntityId,
                nameof(shooterEntityId));
            ShooterTeamId = RequireId(shooterTeamId, nameof(shooterTeamId));
            if (observedWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(observedWorldTick));
            }

            if (magazineCapacity <= 0 ||
                magazineBefore < 0 ||
                magazineAfter <= magazineBefore ||
                magazineAfter > magazineCapacity ||
                reserveBefore <= 0 ||
                reserveAfter < 0 ||
                reserveAfter >= reserveBefore ||
                magazineAfter - magazineBefore !=
                    reserveBefore - reserveAfter)
            {
                throw new ArgumentException(
                    "A reload receipt requires one positive, conserved transfer " +
                    "from reserve into the magazine.");
            }

            AgentCycle = agentCycle;
            ActionRunIndex = actionRunIndex;
            ObservedWorldTick = observedWorldTick;
            MagazineCapacity = magazineCapacity;
            MagazineBefore = magazineBefore;
            MagazineAfter = magazineAfter;
            ReserveBefore = reserveBefore;
            ReserveAfter = reserveAfter;
        }

        public long AgentCycle { get; }
        public string ActionStableId { get; }
        public long ActionRunIndex { get; }
        public string WeaponId { get; }
        public string ShooterEntityId { get; }
        public string ShooterTeamId { get; }
        public long ObservedWorldTick { get; }
        public int MagazineCapacity { get; }
        public int MagazineBefore { get; }
        public int MagazineAfter { get; }
        public int ReserveBefore { get; }
        public int ReserveAfter { get; }
        public int TransferredRounds => MagazineAfter - MagazineBefore;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A reload receipt requires non-empty stable identities.",
                    parameterName);
            }

            return value;
        }
    }
}
