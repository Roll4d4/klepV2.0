using System;
using System.Globalization;
using Roll4d4.Klep.Desire;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// The factual demo phases at which civilian safety is sampled. These are
    /// project observations, not extra Agent phases.
    /// </summary>
    public enum KLEPCivilianSafetyDesirePhase
    {
        Baseline,
        WorldObservation
    }

    /// <summary>
    /// Immutable project state supplied to the civilian's two safety-related
    /// Desire evaluators. A distance at or beyond ThreatRadius means no
    /// relevant nearby threat; the live Unity world is never retained.
    /// </summary>
    public sealed class KLEPCivilianSafetyDesireContext
    {
        public KLEPCivilianSafetyDesireContext(
            bool isAlive,
            float nearestHostileDistance,
            float threatRadius,
            bool hasWeapon,
            bool weaponLoaded,
            long worldTick,
            KLEPCivilianSafetyDesirePhase phase)
        {
            IsAlive = isAlive;
            NearestHostileDistance =
                KLEPCivilianSafetyDesireBody.RequireNonnegativeFinite(
                    nearestHostileDistance,
                    nameof(nearestHostileDistance));
            ThreatRadius = KLEPCivilianSafetyDesireBody.RequirePositiveFinite(
                threatRadius,
                nameof(threatRadius));
            if (weaponLoaded && !hasWeapon)
            {
                throw new ArgumentException(
                    "A civilian cannot report a loaded weapon it does not possess.",
                    nameof(weaponLoaded));
            }

            if (worldTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldTick));
            }

            if (!Enum.IsDefined(
                    typeof(KLEPCivilianSafetyDesirePhase),
                    phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            HasWeapon = hasWeapon;
            WeaponLoaded = weaponLoaded;
            WorldTick = worldTick;
            Phase = phase;
            Threat01 = Clamp01(
                1f - (NearestHostileDistance / ThreatRadius));
        }

        public bool IsAlive { get; }
        public float NearestHostileDistance { get; }
        public float ThreatRadius { get; }
        public float Threat01 { get; }
        public bool HasWeapon { get; }
        public bool WeaponLoaded { get; }
        public long WorldTick { get; }
        public KLEPCivilianSafetyDesirePhase Phase { get; }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }

    /// <summary>
    /// One passive civilian Desire observation and its explicitly unknown
    /// causal effect vector.
    /// </summary>
    public sealed class KLEPCivilianSafetyDesireStep
    {
        internal KLEPCivilianSafetyDesireStep(
            KLEPDesireSnapshot snapshot,
            KLEPDesireEffectVector effects)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(
                nameof(snapshot));
            Effects = effects ?? throw new ArgumentNullException(
                nameof(effects));
        }

        public KLEPDesireSnapshot Snapshot { get; }
        public KLEPDesireEffectVector Effects { get; }
    }

    /// <summary>
    /// Project-owned passive Desire body for one demo civilian. Actual safety
    /// and defensive readiness remain separate so possessing a loaded weapon
    /// is not silently declared equivalent to being safe. This type has no
    /// selection, execution, Emotion, or Ethics authority.
    /// </summary>
    public sealed class KLEPCivilianSafetyDesireBody
    {
        public const string SafetyDesireStableId = "desire.civilian.safe";
        public const string SafetyDesireVersion = "1";
        public const string ReadyToDefendDesireStableId =
            "desire.civilian.ready-to-defend";
        public const string ReadyToDefendDesireVersion = "1";
        public const string ContextSchemaId = "context.civilian.safety";
        public const string ContextSchemaVersion = "1";
        public const float DefaultSafetyWeight = 1f;
        public const float DefaultReadyToDefendWeight = 1f;

        private readonly string ownerId;
        private long nextDesireTick;
        private long lastWorldTick;

        public KLEPCivilianSafetyDesireBody(
            string ownerId,
            float threatRadius,
            bool initiallyAlive,
            float initialNearestHostileDistance,
            bool initiallyHasWeapon,
            bool initiallyWeaponLoaded,
            float safetyWeight = DefaultSafetyWeight,
            float readyToDefendWeight = DefaultReadyToDefendWeight,
            long initialWorldTick = 0)
        {
            this.ownerId = RequireId(ownerId, nameof(ownerId));
            ThreatRadius = RequirePositiveFinite(
                threatRadius,
                nameof(threatRadius));
            float acceptedSafetyWeight = RequireNonnegativeFinite(
                safetyWeight,
                nameof(safetyWeight));
            float acceptedReadyWeight = RequireNonnegativeFinite(
                readyToDefendWeight,
                nameof(readyToDefendWeight));
            if (initialWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialWorldTick));
            }

            DesireSystem =
                new KLEPDesireSystem<KLEPCivilianSafetyDesireContext>(
                    this.ownerId,
                    new[]
                    {
                        new KLEPDesireDefinition<
                            KLEPCivilianSafetyDesireContext>(
                                SafetyDesireStableId,
                                SafetyDesireVersion,
                                acceptedSafetyWeight,
                                new SafetyEvaluator()),
                        new KLEPDesireDefinition<
                            KLEPCivilianSafetyDesireContext>(
                                ReadyToDefendDesireStableId,
                                ReadyToDefendDesireVersion,
                                acceptedReadyWeight,
                                new ReadyToDefendEvaluator())
                    });

            nextDesireTick = 0;
            lastWorldTick = initialWorldTick;
            BaselineSnapshot = Observe(
                initialWorldTick,
                initiallyAlive,
                initialNearestHostileDistance,
                initiallyHasWeapon,
                initiallyWeaponLoaded,
                KLEPCivilianSafetyDesirePhase.Baseline,
                "baseline");
        }

        public string OwnerId => ownerId;
        public float ThreatRadius { get; }
        public long LastWorldTick => lastWorldTick;
        public KLEPDesireSystem<KLEPCivilianSafetyDesireContext>
            DesireSystem { get; }
        public KLEPDesireSnapshot BaselineSnapshot { get; }
        public KLEPDesireEffectVector LastEffects { get; private set; }

        /// <summary>
        /// Records the exact next world observation. Causality is deliberately
        /// Unknown: a periodic sample may combine movement, attack, equipment,
        /// and other world changes, so it cannot truthfully train the
        /// ActionOwned critic.
        /// </summary>
        public KLEPCivilianSafetyDesireStep ObserveWorld(
            long worldTick,
            bool isAlive,
            float nearestHostileDistance,
            bool hasWeapon,
            bool weaponLoaded)
        {
            if (lastWorldTick == long.MaxValue ||
                worldTick != lastWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "Civilian safety observation requires the exact next " +
                    "world Tick so no state change is skipped or replayed.");
            }

            KLEPDesireSnapshot prior = DesireSystem.CurrentSnapshot;
            KLEPDesireSnapshot consequence = Observe(
                worldTick,
                isAlive,
                nearestHostileDistance,
                hasWeapon,
                weaponLoaded,
                KLEPCivilianSafetyDesirePhase.WorldObservation,
                "world-observation");
            var attribution = new KLEPDesireAttributionEvidence(
                KLEPDesireEffectAttribution.Unknown,
                "source.civilian.safety-sampler",
                evidenceIds: new[]
                {
                    "state.civilian.alive",
                    "state.civilian.nearest-hostile-distance",
                    "state.civilian.weapon-possessed",
                    "state.civilian.weapon-loaded",
                    "world.tick." + worldTick.ToString(
                        CultureInfo.InvariantCulture)
                });
            KLEPDesireEffectVector effects = DesireSystem.EvaluateTransition(
                new KLEPDesireTransitionRequest(
                    TransitionId(worldTick, consequence.DesireTick),
                    prior,
                    consequence,
                    attribution));

            lastWorldTick = worldTick;
            LastEffects = effects;
            return new KLEPCivilianSafetyDesireStep(consequence, effects);
        }

        private KLEPDesireSnapshot Observe(
            long worldTick,
            bool isAlive,
            float nearestHostileDistance,
            bool hasWeapon,
            bool weaponLoaded,
            KLEPCivilianSafetyDesirePhase phase,
            string phaseId)
        {
            if (nextDesireTick == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The civilian safety Desire Tick counter is exhausted.");
            }

            long desireTick = nextDesireTick;
            var context = new KLEPCivilianSafetyDesireContext(
                isAlive,
                nearestHostileDistance,
                ThreatRadius,
                hasWeapon,
                weaponLoaded,
                worldTick,
                phase);
            string desireText = desireTick.ToString(
                CultureInfo.InvariantCulture);
            string worldText = worldTick.ToString(
                CultureInfo.InvariantCulture);
            KLEPDesireSnapshot snapshot = DesireSystem.Observe(
                new KLEPDesireObservationRequest<
                    KLEPCivilianSafetyDesireContext>(
                        ownerId + ".desire.snapshot." + desireText,
                        desireTick,
                        ownerId + ".world." + worldText + "." + phaseId,
                        new KLEPDesireContextIdentity(
                            ownerId + ".desire.context.safety",
                            ContextSchemaId,
                            ContextSchemaVersion),
                        context));
            nextDesireTick++;
            return snapshot;
        }

        private string TransitionId(
            long worldTick,
            long consequenceDesireTick)
        {
            return ownerId + ".desire.transition.safety-sample.world-" +
                   worldTick.ToString(CultureInfo.InvariantCulture) +
                   ".desire-" + consequenceDesireTick.ToString(
                       CultureInfo.InvariantCulture);
        }

        internal static float RequireNonnegativeFinite(
            float value,
            string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static float RequirePositiveFinite(
            float value,
            string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Civilian safety Desire values must be finite.");
            }
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Civilian safety Desire evidence requires a non-empty identity.",
                    parameterName);
            }

            return value;
        }

        private sealed class SafetyEvaluator :
            IKLEPDesireEvaluator<KLEPCivilianSafetyDesireContext>
        {
            public string EvaluatorId => "evaluator.civilian.safe";
            public string EvaluatorVersion => "1";

            public KLEPDesireAssessment Evaluate(
                KLEPCivilianSafetyDesireContext context,
                long desireTick)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                float satisfaction = context.IsAlive
                    ? 1f - context.Threat01
                    : 0f;
                float pressure = context.IsAlive ? context.Threat01 : 0f;
                return new KLEPDesireAssessment(
                    satisfaction,
                    pressure,
                    "Actual safety is " + satisfaction.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) +
                    " from alive=" + context.IsAlive +
                    " and nearest-hostile distance " +
                    context.NearestHostileDistance.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) +
                    " within authored threat radius " +
                    context.ThreatRadius.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) +
                    " after " + context.Phase + ".",
                    new[]
                    {
                        "state.civilian.alive",
                        "state.civilian.nearest-hostile-distance",
                        "configuration.civilian.threat-radius",
                        "world.tick." + context.WorldTick.ToString(
                            CultureInfo.InvariantCulture)
                    });
            }
        }

        private sealed class ReadyToDefendEvaluator :
            IKLEPDesireEvaluator<KLEPCivilianSafetyDesireContext>
        {
            public string EvaluatorId =>
                "evaluator.civilian.ready-to-defend";
            public string EvaluatorVersion => "1";

            public KLEPDesireAssessment Evaluate(
                KLEPCivilianSafetyDesireContext context,
                long desireTick)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                float satisfaction = context.IsAlive &&
                                     context.HasWeapon &&
                                     context.WeaponLoaded
                    ? 1f
                    : 0f;
                float pressure = context.IsAlive ? context.Threat01 : 0f;
                return new KLEPDesireAssessment(
                    satisfaction,
                    pressure,
                    "Defensive readiness requires a living civilian to " +
                    "possess a loaded weapon; possessed=" +
                    context.HasWeapon + ", loaded=" + context.WeaponLoaded +
                    ". Its urgency follows nearby threat " +
                    context.Threat01.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) + ".",
                    new[]
                    {
                        "state.civilian.alive",
                        "state.civilian.weapon-possessed",
                        "state.civilian.weapon-loaded",
                        "state.civilian.nearest-hostile-distance",
                        "world.tick." + context.WorldTick.ToString(
                            CultureInfo.InvariantCulture)
                    });
            }
        }
    }
}
