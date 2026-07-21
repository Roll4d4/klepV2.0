using System;
using System.Collections.Generic;
using System.Globalization;
using Roll4d4.Klep.Desire;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// The three factual phases in which the demo samples zombie hunger.
    /// They are observations, not extra KLEP Agent phases.
    /// </summary>
    public enum KLEPZombieDesirePhase
    {
        Baseline,
        AfterMetabolism,
        AfterSuccessfulBite
    }

    /// <summary>
    /// Immutable project state supplied to the fed-Desire evaluator. The live
    /// Unity object is never captured by the portable Desire system.
    /// </summary>
    public sealed class KLEPZombieDesireContext
    {
        public KLEPZombieDesireContext(
            float hunger01,
            long worldTick,
            KLEPZombieDesirePhase phase)
        {
            Hunger01 = KLEPZombieDesireBody.RequireUnitInterval(
                hunger01,
                nameof(hunger01));
            if (worldTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldTick));
            }

            if (!Enum.IsDefined(typeof(KLEPZombieDesirePhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }

            WorldTick = worldTick;
            Phase = phase;
        }

        public float Hunger01 { get; }
        public long WorldTick { get; }
        public KLEPZombieDesirePhase Phase { get; }
    }

    /// <summary>
    /// Immutable pair produced by one factual project transition. The caller
    /// may archive the effect vector in Memory or publish both values outward.
    /// </summary>
    public sealed class KLEPZombieDesireStep
    {
        internal KLEPZombieDesireStep(
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
    /// Project-owned factual body for the demo zombie's fed Desire. It keeps
    /// hunger and Desire observation clocks, but has no authority over Agent
    /// eligibility or selection.
    /// </summary>
    public sealed class KLEPZombieDesireBody
    {
        public const float DefaultInitialHunger01 = 0.60f;
        public const float DefaultMetabolismPerWorldTick = 0.01f;
        public const float DefaultSuccessfulBiteRelief = 0.35f;
        public const float DefaultFedDesireWeight = 1f;
        public const float MinimumNonzeroHungerDelta = 0.000001f;

        public const string FedDesireStableId = "desire.zombie.fed";
        public const string FedDesireVersion = "1";
        public const string ContextSchemaId = "context.zombie.hunger";
        public const string ContextSchemaVersion = "1";

        private readonly string ownerId;
        private readonly string attackExecutableStableId;
        private readonly float metabolismPerWorldTick;
        private readonly float successfulBiteRelief;
        private long nextDesireTick;
        private long lastWorldTick;
        private long lastSuccessfulBiteRunIndex;
        private bool biteRecordedForCurrentWorldTick;

        public KLEPZombieDesireBody(
            string ownerId,
            string attackExecutableStableId,
            float initialHunger01 = DefaultInitialHunger01,
            float metabolismPerWorldTick = DefaultMetabolismPerWorldTick,
            float successfulBiteRelief = DefaultSuccessfulBiteRelief,
            float fedDesireWeight = DefaultFedDesireWeight,
            long initialWorldTick = 0)
        {
            this.ownerId = RequireId(ownerId, nameof(ownerId));
            this.attackExecutableStableId = RequireId(
                attackExecutableStableId,
                nameof(attackExecutableStableId));
            Hunger01 = RequireUnitInterval(
                initialHunger01,
                nameof(initialHunger01));
            this.metabolismPerWorldTick = RequireHungerDelta(
                metabolismPerWorldTick,
                nameof(metabolismPerWorldTick));
            this.successfulBiteRelief = RequireHungerDelta(
                successfulBiteRelief,
                nameof(successfulBiteRelief));
            float weight = RequireNonnegativeFinite(
                fedDesireWeight,
                nameof(fedDesireWeight));

            DesireSystem = new KLEPDesireSystem<KLEPZombieDesireContext>(
                this.ownerId,
                new[]
                {
                    new KLEPDesireDefinition<KLEPZombieDesireContext>(
                        FedDesireStableId,
                        FedDesireVersion,
                        weight,
                        new FedDesireEvaluator())
                });

            if (initialWorldTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialWorldTick));
            }

            nextDesireTick = 0;
            lastWorldTick = initialWorldTick;
            lastSuccessfulBiteRunIndex = 0;
            BaselineSnapshot = Observe(
                initialWorldTick,
                KLEPZombieDesirePhase.Baseline,
                "baseline");
        }

        public string OwnerId => ownerId;
        public string AttackExecutableStableId => attackExecutableStableId;
        public float Hunger01 { get; private set; }
        public float MetabolismPerWorldTick => metabolismPerWorldTick;
        public float SuccessfulBiteRelief => successfulBiteRelief;
        public long LastWorldTick => lastWorldTick;
        public KLEPDesireSystem<KLEPZombieDesireContext> DesireSystem { get; }
        public KLEPDesireSnapshot BaselineSnapshot { get; }
        public KLEPDesireEffectVector LastExternalEffects { get; private set; }
        public KLEPDesireEffectVector LastSuccessfulBiteEffects { get; private set; }

        /// <summary>
        /// Applies only passive hunger growth and records it as External. Call
        /// this once before the Agent Tick for each strictly increasing world
        /// Tick. Even a configured zero rate remains explicit zero-effect
        /// evidence rather than being silently merged with a later bite.
        /// </summary>
        public KLEPZombieDesireStep AdvanceMetabolism(long worldTick)
        {
            if (lastWorldTick == long.MaxValue ||
                worldTick != lastWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "Zombie metabolism requires the exact next world Tick so " +
                    "no passive change is silently skipped or double-counted.");
            }

            KLEPDesireSnapshot prior = DesireSystem.CurrentSnapshot;
            Hunger01 = Clamp01(Hunger01 + metabolismPerWorldTick);
            KLEPDesireSnapshot consequence = Observe(
                worldTick,
                KLEPZombieDesirePhase.AfterMetabolism,
                "metabolism");
            var attribution = new KLEPDesireAttributionEvidence(
                KLEPDesireEffectAttribution.External,
                "source.zombie.metabolism",
                evidenceIds: new[]
                {
                    "state.zombie.hunger",
                    "world.tick." + worldTick.ToString(CultureInfo.InvariantCulture)
                });
            KLEPDesireEffectVector effects = DesireSystem.EvaluateTransition(
                new KLEPDesireTransitionRequest(
                    TransitionId("metabolism", worldTick, consequence.DesireTick),
                    prior,
                    consequence,
                    attribution));

            lastWorldTick = worldTick;
            biteRecordedForCurrentWorldTick = false;
            LastExternalEffects = effects;
            return new KLEPZombieDesireStep(consequence, effects);
        }

        /// <summary>
        /// Records hunger relief only after the Unity adapter has factually
        /// applied the exact successful attack run. The stable ID and run index
        /// are mandatory; display text such as LastAction is never consulted.
        /// </summary>
        internal KLEPZombieDesireStep RecordSuccessfulBite(
            long worldTick,
            string actionStableId,
            long actionRunIndex,
            string targetEntityId)
        {
            if (worldTick != lastWorldTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "A successful bite must follow metabolism in the same world Tick.");
            }

            if (biteRecordedForCurrentWorldTick)
            {
                throw new InvalidOperationException(
                    "Only one successful bite can be attributed in one demo world Tick.");
            }

            string exactActionId = RequireId(
                actionStableId,
                nameof(actionStableId));
            if (!StringComparer.Ordinal.Equals(
                    exactActionId,
                    attackExecutableStableId))
            {
                throw new ArgumentException(
                    "Bite Desire evidence must identify the configured attack Executable.",
                    nameof(actionStableId));
            }

            if (actionRunIndex <= lastSuccessfulBiteRunIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionRunIndex),
                    "A successful bite requires a new positive exact action run index.");
            }

            string exactTargetId = RequireId(
                targetEntityId,
                nameof(targetEntityId));
            KLEPDesireSnapshot prior = DesireSystem.CurrentSnapshot;
            Hunger01 = Clamp01(Hunger01 - successfulBiteRelief);
            KLEPDesireSnapshot consequence = Observe(
                worldTick,
                KLEPZombieDesirePhase.AfterSuccessfulBite,
                "bite-run-" + actionRunIndex.ToString(CultureInfo.InvariantCulture));
            var attribution = new KLEPDesireAttributionEvidence(
                KLEPDesireEffectAttribution.ActionOwned,
                "unity.zombie.attack-applied",
                exactActionId,
                actionRunIndex,
                new[]
                {
                    "state.zombie.hunger",
                    "target." + exactTargetId,
                    "world.tick." + worldTick.ToString(CultureInfo.InvariantCulture)
                });
            KLEPDesireEffectVector effects = DesireSystem.EvaluateTransition(
                new KLEPDesireTransitionRequest(
                    TransitionId("bite", worldTick, consequence.DesireTick),
                    prior,
                    consequence,
                    attribution));

            biteRecordedForCurrentWorldTick = true;
            lastSuccessfulBiteRunIndex = actionRunIndex;
            LastSuccessfulBiteEffects = effects;
            return new KLEPZombieDesireStep(consequence, effects);
        }

        private KLEPDesireSnapshot Observe(
            long worldTick,
            KLEPZombieDesirePhase phase,
            string phaseId)
        {
            if (nextDesireTick == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The zombie Desire Tick counter is exhausted.");
            }

            long desireTick = nextDesireTick;
            nextDesireTick++;
            string tickText = desireTick.ToString(CultureInfo.InvariantCulture);
            string worldText = worldTick.ToString(CultureInfo.InvariantCulture);
            var context = new KLEPZombieDesireContext(
                Hunger01,
                worldTick,
                phase);
            return DesireSystem.Observe(
                new KLEPDesireObservationRequest<KLEPZombieDesireContext>(
                    ownerId + ".desire.snapshot." + tickText,
                    desireTick,
                    ownerId + ".world." + worldText + "." + phaseId,
                    new KLEPDesireContextIdentity(
                        ownerId + ".desire.context.hunger",
                        ContextSchemaId,
                        ContextSchemaVersion),
                    context));
        }

        private string TransitionId(
            string kind,
            long worldTick,
            long consequenceDesireTick)
        {
            return ownerId + ".desire.transition." + kind + ".world-" +
                   worldTick.ToString(CultureInfo.InvariantCulture) +
                   ".desire-" +
                   consequenceDesireTick.ToString(CultureInfo.InvariantCulture);
        }

        internal static float RequireUnitInterval(
            float value,
            string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Zombie hunger must be in the inclusive range [0, 1].");
            }

            return value;
        }

        private static float RequireNonnegativeFinite(
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

        private static float RequireHungerDelta(
            float value,
            string parameterName)
        {
            float accepted = RequireNonnegativeFinite(value, parameterName);
            if (accepted > 0f && accepted < MinimumNonzeroHungerDelta)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"A nonzero zombie hunger delta must be at least " +
                    $"{MinimumNonzeroHungerDelta} so float quantization cannot " +
                    "silently erase the configured change.");
            }

            return accepted;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Zombie Desire values must be finite.");
            }
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Zombie Desire evidence requires a non-empty stable identity.",
                    parameterName);
            }

            return value;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }

        private sealed class FedDesireEvaluator :
            IKLEPDesireEvaluator<KLEPZombieDesireContext>
        {
            public string EvaluatorId => "evaluator.zombie.fed";
            public string EvaluatorVersion => "1";

            public KLEPDesireAssessment Evaluate(
                KLEPZombieDesireContext context,
                long desireTick)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                return new KLEPDesireAssessment(
                    1f - context.Hunger01,
                    context.Hunger01,
                    "Fed satisfaction is the inverse of factual hunger " +
                    context.Hunger01.ToString("0.000", CultureInfo.InvariantCulture) +
                    " after " + context.Phase + ".",
                    new[]
                    {
                        "state.zombie.hunger",
                        "world.tick." + context.WorldTick.ToString(
                            CultureInfo.InvariantCulture)
                    });
            }
        }
    }
}
