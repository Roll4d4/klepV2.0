using System;
using System.Collections.Generic;
using System.Globalization;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Cognition;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.LearnedExpectations;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.Observer;
using Roll4d4.Klep.Unity;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// Immutable project-owned facts for one discharged civilian Fire action.
    /// A miss, an obstacle impact, and an entity impact are all factual shots;
    /// Ethics decides their meaning without changing action success.
    /// </summary>
    internal sealed class KLEPCivilianShotContext
    {
        internal KLEPCivilianShotContext(
            string actorNeuronId,
            long actionWorldTick,
            long consequenceWorldTick,
            KLEPWeaponAppliedShotReceipt receipt)
        {
            ActorNeuronId = RequireId(
                actorNeuronId,
                nameof(actorNeuronId));
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            if (actionWorldTick <= 0 ||
                receipt.ObservedWorldTick != actionWorldTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionWorldTick),
                    "A shot context uses the receipt's exact observed world Tick.");
            }

            if (actionWorldTick == long.MaxValue ||
                consequenceWorldTick != actionWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(consequenceWorldTick),
                    "A shot consequence is the first post-effect world Tick.");
            }

            ActionStableId = RequireId(
                receipt.ActionStableId,
                nameof(receipt.ActionStableId));
            WeaponId = RequireId(receipt.WeaponId, nameof(receipt.WeaponId));
            ShooterEntityId = RequireId(
                receipt.ShooterEntityId,
                nameof(receipt.ShooterEntityId));
            ShooterTeamId = RequireId(
                receipt.ShooterTeamId,
                nameof(receipt.ShooterTeamId));
            if (receipt.ActionRunIndex <= 0 || receipt.AgentCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receipt));
            }

            ActionRunIndex = receipt.ActionRunIndex;
            ActionCycle = receipt.AgentCycle;
            ActionWorldTick = actionWorldTick;
            ConsequenceWorldTick = consequenceWorldTick;
            MagazineBefore = receipt.MagazineBefore;
            MagazineAfter = receipt.MagazineAfter;
            HitKind = receipt.HitKind;
            HitEntityId = receipt.HitEntityId;
            HitTeamId = receipt.HitTeamId;
            HitDistance = receipt.HitDistance;
            HealthBefore = receipt.HealthBefore;
            HealthAfter = receipt.HealthAfter;
            TargetKilled = receipt.TargetKilled;
            FriendlyFire = receipt.FriendlyFire;
        }

        internal string ActorNeuronId { get; }
        internal string ActionStableId { get; }
        internal long ActionRunIndex { get; }
        internal long ActionCycle { get; }
        internal long ActionWorldTick { get; }
        internal long ConsequenceWorldTick { get; }
        internal string WeaponId { get; }
        internal string ShooterEntityId { get; }
        internal string ShooterTeamId { get; }
        internal int MagazineBefore { get; }
        internal int MagazineAfter { get; }
        internal KLEPWeaponLineHitKind HitKind { get; }
        internal bool DidHitEntity =>
            HitKind == KLEPWeaponLineHitKind.Entity;
        internal string HitEntityId { get; }
        internal string HitTeamId { get; }
        internal float HitDistance { get; }
        internal float HealthBefore { get; }
        internal float HealthAfter { get; }
        internal float AppliedDamage => HealthBefore - HealthAfter;
        internal bool TargetKilled { get; }
        internal bool FriendlyFire { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A civilian shot context requires non-empty identities.",
                    parameterName);
            }

            return value;
        }
    }

    internal sealed class KLEPCivilianShotExperienceResult
    {
        internal KLEPCivilianShotExperienceResult(
            KLEPCivilianShotContext context,
            KLEPCognitionTransition<KLEPCivilianShotContext> transition)
        {
            Context = context ?? throw new ArgumentNullException(
                nameof(context));
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
        }

        internal KLEPCivilianShotContext Context { get; }
        internal KLEPCognitionTransition<KLEPCivilianShotContext>
            Transition { get; }
    }

    /// <summary>
    /// Stages an exact successful Fire result and immutable applied-shot
    /// receipt at Tick N, then closes it against the first post-effect Neuron
    /// snapshot at Tick N+1. The transaction order is Ethics, Emotion, Memory.
    /// </summary>
    internal sealed class KLEPCivilianShotExperienceCoordinator
    {
        private const string ContextSchemaId =
            "context.civilian.applied-shot";
        private const string ContextSchemaVersion = "1";

        private readonly string actorNeuronId;
        private readonly string shooterEntityId;
        private readonly string shooterTeamId;
        private readonly KLEPCognitionComposition<KLEPCivilianShotContext>
            cognition;
        private PendingShot pending;
        private long lastCompletedActionCycle;

        internal KLEPCivilianShotExperienceCoordinator(
            string actorNeuronId,
            string shooterEntityId,
            string shooterTeamId,
            KLEPCognitionComposition<KLEPCivilianShotContext> cognition)
        {
            this.actorNeuronId = RequireId(
                actorNeuronId,
                nameof(actorNeuronId));
            this.shooterEntityId = RequireId(
                shooterEntityId,
                nameof(shooterEntityId));
            this.shooterTeamId = RequireId(
                shooterTeamId,
                nameof(shooterTeamId));
            this.cognition = cognition ?? throw new ArgumentNullException(
                nameof(cognition));
        }

        internal bool HasPendingExperience => pending != null;

        internal KLEPEmotionSnapshot AdvanceEmotionWithoutExperience(
            long worldTick)
        {
            if (pending != null)
            {
                throw new InvalidOperationException(
                    "A pending civilian shot must close instead of consuming " +
                    "its consequence Tick as idle Emotion time.");
            }

            return cognition.AdvanceEmotionWithoutExperience(worldTick);
        }

        internal void StageAppliedShot(
            long worldTick,
            KLEPWeaponAppliedShotReceipt receipt,
            KLEPAgentTickTrace fireTrace)
        {
            if (pending != null)
            {
                throw new InvalidOperationException(
                    "Only one unresolved civilian shot may be staged.");
            }

            if (worldTick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldTick));
            }

            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            if (fireTrace == null)
            {
                throw new ArgumentNullException(nameof(fireTrace));
            }

            if (fireTrace.Decision.Fault != null ||
                fireTrace.Decision.CycleIndex != worldTick ||
                receipt.AgentCycle != fireTrace.Decision.CycleIndex ||
                receipt.ObservedWorldTick != worldTick)
            {
                throw new ArgumentException(
                    "A staged shot requires the exact unfaulted Fire trace and " +
                    "applied receipt from the same world Tick.",
                    nameof(fireTrace));
            }

            if (!StringComparer.Ordinal.Equals(
                    receipt.ShooterEntityId,
                    shooterEntityId) ||
                !StringComparer.Ordinal.Equals(
                    receipt.ShooterTeamId,
                    shooterTeamId))
            {
                throw new ArgumentException(
                    "The shot receipt belongs to a different shooter.",
                    nameof(receipt));
            }

            if (receipt.AgentCycle <= lastCompletedActionCycle)
            {
                throw new InvalidOperationException(
                    "A completed Fire cycle cannot be staged again.");
            }

            KLEPExecutionResult actionResult = FindExactSuccessfulAction(
                fireTrace.Decision.ExecutableStates,
                receipt.ActionStableId,
                receipt.ActionRunIndex,
                receipt.AgentCycle);
            if (cognition.Emotion.Tick != worldTick)
            {
                throw new InvalidOperationException(
                    "Stage the shot after Emotion advances exactly once for " +
                    "its action world Tick.");
            }

            string experienceId = BuildRunId(
                "experience",
                receipt.ActionStableId,
                receipt.ActionRunIndex,
                receipt.AgentCycle);
            KLEPMemoryMoment prior = KLEPMemoryMoment.Capture(
                experienceId + ".moment.prior",
                KLEPMemoryMomentRole.Prior,
                fireTrace.Decision.InitialKeySnapshot);
            KLEPMemoryMoment during = KLEPMemoryMoment.Capture(
                experienceId + ".moment.during",
                KLEPMemoryMomentRole.During,
                fireTrace.Decision.KeySnapshot);
            pending = new PendingShot(
                worldTick,
                experienceId,
                BuildRunId(
                    "evaluation",
                    receipt.ActionStableId,
                    receipt.ActionRunIndex,
                    receipt.AgentCycle),
                BuildRunId(
                    "context",
                    receipt.ActionStableId,
                    receipt.ActionRunIndex,
                    receipt.AgentCycle),
                actionResult,
                receipt,
                prior,
                during);
        }

        internal KLEPCivilianShotExperienceResult CompletePendingShot(
            long worldTick,
            KLEPAgentTickTrace consequenceTrace)
        {
            PendingShot staged = pending ?? throw new InvalidOperationException(
                "No civilian shot is waiting for a consequence.");
            if (staged.ActionWorldTick == long.MaxValue ||
                worldTick != staged.ActionWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "A civilian shot closes on the first post-effect world Tick.");
            }

            if (consequenceTrace == null)
            {
                throw new ArgumentNullException(nameof(consequenceTrace));
            }

            if (consequenceTrace.Decision.Fault != null ||
                consequenceTrace.Decision.CycleIndex != worldTick ||
                consequenceTrace.Decision.CycleIndex !=
                    staged.ActionResult.CycleIndex + 1)
            {
                throw new ArgumentException(
                    "A shot consequence requires the exact next unfaulted " +
                    "Agent trace.",
                    nameof(consequenceTrace));
            }

            KLEPMemoryMoment consequence = KLEPMemoryMoment.Capture(
                staged.ExperienceId + ".moment.consequence",
                KLEPMemoryMomentRole.Consequence,
                consequenceTrace.Decision.InitialKeySnapshot);
            KLEPMemoryActionOutcome actionOutcome =
                KLEPMemoryActionOutcome.Capture(
                    staged.ActionResult,
                    staged.ActionResult.CycleIndex,
                    staged.ActionResult.WaveIndex);
            var context = new KLEPCivilianShotContext(
                actorNeuronId,
                staged.ActionWorldTick,
                worldTick,
                staged.Receipt);
            var request = new KLEPCognitionExperienceRequest<
                KLEPCivilianShotContext>(
                    staged.ExperienceId,
                    memoryTick: worldTick,
                    emotionTick: worldTick,
                    evaluationId: staged.EvaluationId,
                    evaluationTick: worldTick,
                    causeOrigin: KLEPEmotionInfluenceOrigin.Internal,
                    contextIdentity: new KLEPEthicsContextIdentity(
                        staged.ContextId,
                        ContextSchemaId,
                        ContextSchemaVersion),
                    context: context,
                    moments: new[]
                    {
                        staged.PriorMoment,
                        staged.DuringMoment,
                        consequence
                    },
                    actionOutcome: actionOutcome);

            KLEPCognitionTransition<KLEPCivilianShotContext> transition =
                cognition.Process(request);

            // Publish completion guards only after the atomic transaction.
            lastCompletedActionCycle = staged.ActionResult.CycleIndex;
            pending = null;
            return new KLEPCivilianShotExperienceResult(
                context,
                transition);
        }

        private string BuildRunId(
            string domain,
            string actionStableId,
            long actionRunIndex,
            long actionCycle)
        {
            return domain + "." + actorNeuronId + ".shot." +
                actionStableId + ".cycle." + actionCycle.ToString(
                    CultureInfo.InvariantCulture) + ".run." +
                actionRunIndex.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static KLEPExecutionResult FindExactSuccessfulAction(
            IReadOnlyList<KLEPExecutableRuntimeSnapshot> roots,
            string actionStableId,
            long actionRunIndex,
            long actionCycle)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            string exactActionId = RequireId(
                actionStableId,
                nameof(actionStableId));
            KLEPExecutionResult match = null;
            for (int index = 0; index < roots.Count; index++)
            {
                FindExactSuccessfulAction(
                    roots[index],
                    exactActionId,
                    actionRunIndex,
                    actionCycle,
                    ref match);
            }

            return match ?? throw new ArgumentException(
                "The shot receipt does not identify a successful Fire action " +
                "in the supplied Agent trace.",
                nameof(roots));
        }

        private static void FindExactSuccessfulAction(
            KLEPExecutableRuntimeSnapshot runtime,
            string actionStableId,
            long actionRunIndex,
            long actionCycle,
            ref KLEPExecutionResult match)
        {
            if (runtime == null)
            {
                throw new ArgumentException(
                    "An Agent trace cannot contain a null runtime snapshot.",
                    nameof(runtime));
            }

            if (StringComparer.Ordinal.Equals(
                    runtime.ExecutableStableId,
                    actionStableId))
            {
                KLEPExecutionResult result = runtime.LastResult;
                if (result == null ||
                    result.RunIndex != actionRunIndex ||
                    result.CycleIndex != actionCycle ||
                    result.State != KLEPExecutableState.Succeeded ||
                    result.ExitReason != KLEPExecutableExitReason.Succeeded)
                {
                    throw new ArgumentException(
                        "The exact Fire action did not factually succeed in " +
                        "the declared cycle.",
                        nameof(runtime));
                }

                if (match != null && !ReferenceEquals(match, result))
                {
                    throw new ArgumentException(
                        "The supplied trace contains duplicate matching Fire " +
                        "action results.",
                        nameof(runtime));
                }

                match = result;
            }

            KLEPGoalRuntimeSnapshot goal = runtime.Goal;
            if (goal == null)
            {
                return;
            }

            for (int layerIndex = 0;
                 layerIndex < goal.Layers.Count;
                 layerIndex++)
            {
                KLEPGoalLayerRuntimeSnapshot layer = goal.Layers[layerIndex];
                for (int childIndex = 0;
                     childIndex < layer.Children.Count;
                     childIndex++)
                {
                    FindExactSuccessfulAction(
                        layer.Children[childIndex].Runtime,
                        actionStableId,
                        actionRunIndex,
                        actionCycle,
                        ref match);
                }
            }
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A civilian shot experience requires non-empty identities.",
                    parameterName);
            }

            return value;
        }

        private sealed class PendingShot
        {
            internal PendingShot(
                long actionWorldTick,
                string experienceId,
                string evaluationId,
                string contextId,
                KLEPExecutionResult actionResult,
                KLEPWeaponAppliedShotReceipt receipt,
                KLEPMemoryMoment priorMoment,
                KLEPMemoryMoment duringMoment)
            {
                ActionWorldTick = actionWorldTick;
                ExperienceId = experienceId;
                EvaluationId = evaluationId;
                ContextId = contextId;
                ActionResult = actionResult;
                Receipt = receipt;
                PriorMoment = priorMoment;
                DuringMoment = duringMoment;
            }

            internal long ActionWorldTick { get; }
            internal string ExperienceId { get; }
            internal string EvaluationId { get; }
            internal string ContextId { get; }
            internal KLEPExecutionResult ActionResult { get; }
            internal KLEPWeaponAppliedShotReceipt Receipt { get; }
            internal KLEPMemoryMoment PriorMoment { get; }
            internal KLEPMemoryMoment DuringMoment { get; }
        }
    }

    /// <summary>
    /// Project morality for human civilians in this demonstration. The rules
    /// are ordered and additive; misses and obstacle impacts match no rule and
    /// therefore produce a traced zero impulse while still entering Memory.
    /// </summary>
    internal static class KLEPCivilianCognitionDefaults
    {
        internal const string PolicyVersion =
            "civilian-shot-experience-1";
        internal const string HumanTeamId = "team.human";
        internal const string ZombieTeamId = "team.zombie";

        internal static KLEPCognitionComposition<KLEPCivilianShotContext>
            Create(
                string memoryOwnerId,
                string observerStableId,
                string axisXName,
                string axisYName,
                IKLEPLearnedExpectationsView learnedExpectations = null)
        {
            RequireId(memoryOwnerId, nameof(memoryOwnerId));
            RequireId(observerStableId, nameof(observerStableId));
            RequireId(axisXName, nameof(axisXName));
            RequireId(axisYName, nameof(axisYName));

            var emotionConfiguration = new KLEPEmotionConfiguration(
                axisXName,
                axisYName,
                frictionPerTick: 0.05f,
                maximumSpeed: 1f);
            var ethics = new KLEPEthics<KLEPCivilianShotContext>(
                new KLEPWeightedEthicsEvaluator<
                    KLEPCivilianShotContext>(
                        observerStableId + ".ethics",
                        PolicyVersion,
                        axisXName,
                        axisYName,
                        KLEPEmotionVector.Zero,
                        new IKLEPWeightedEthicsRule<
                            KLEPCivilianShotContext>[]
                        {
                            new ShotEthicsRule(
                                "human.damage-zombie",
                                ZombieTeamId,
                                killOnly: false,
                                new KLEPEmotionVector(0.25f, 0.10f)),
                            new ShotEthicsRule(
                                "human.kill-zombie-bonus",
                                ZombieTeamId,
                                killOnly: true,
                                new KLEPEmotionVector(0.10f, -0.05f)),
                            new ShotEthicsRule(
                                "human.damage-human",
                                HumanTeamId,
                                killOnly: false,
                                new KLEPEmotionVector(-0.50f, 0.40f)),
                            new ShotEthicsRule(
                                "human.kill-human-bonus",
                                HumanTeamId,
                                killOnly: true,
                                new KLEPEmotionVector(-0.35f, 0.30f))
                        }));
            var emotion = new KLEPEmotion(
                emotionConfiguration,
                KLEPEmotionVector.Zero,
                KLEPEmotionVector.Zero);
            var memory = new KLEPMemory(
                memoryOwnerId,
                new KLEPMemoryConfiguration(
                    axisXName: axisXName,
                    axisYName: axisYName));
            return new KLEPCognitionComposition<KLEPCivilianShotContext>(
                observerStableId,
                PolicyVersion,
                ethics,
                emotion,
                memory,
                new AbstainingMemoryPolicy(observerStableId),
                new AbstainingEmotionPolicy(observerStableId),
                learnedExpectations: learnedExpectations);
        }

        private static string[] BuildEvidence(
            KLEPCivilianShotContext context)
        {
            return new[]
            {
                "actor." + context.ActorNeuronId,
                "action." + context.ActionStableId + ".run." +
                    context.ActionRunIndex.ToString(
                        CultureInfo.InvariantCulture),
                "weapon." + context.WeaponId,
                "shooter." + context.ShooterEntityId,
                "shooter-team." + context.ShooterTeamId,
                "hit-kind." + context.HitKind,
                "hit-entity." +
                    (context.HitEntityId.Length == 0
                        ? "none"
                        : context.HitEntityId),
                "hit-team." +
                    (context.HitTeamId.Length == 0
                        ? "none"
                        : context.HitTeamId),
                "applied-damage." + context.AppliedDamage.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                "target-killed." + context.TargetKilled,
                "friendly-fire." + context.FriendlyFire,
                "action-world.tick." + context.ActionWorldTick.ToString(
                    CultureInfo.InvariantCulture),
                "consequence-world.tick." +
                    context.ConsequenceWorldTick.ToString(
                        CultureInfo.InvariantCulture)
            };
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "The civilian cognition composition requires non-empty " +
                    "identities.",
                    parameterName);
            }
        }

        private sealed class ShotEthicsRule :
            IKLEPWeightedEthicsRule<KLEPCivilianShotContext>
        {
            private readonly string targetTeamId;
            private readonly bool killOnly;
            private readonly KLEPEmotionVector impulse;

            internal ShotEthicsRule(
                string ruleId,
                string targetTeamId,
                bool killOnly,
                KLEPEmotionVector impulse)
            {
                RuleId = RequireRuleId(ruleId, nameof(ruleId));
                this.targetTeamId = RequireRuleId(
                    targetTeamId,
                    nameof(targetTeamId));
                this.killOnly = killOnly;
                this.impulse = impulse;
            }

            public string RuleId { get; }
            public float Weight => 1f;

            public KLEPEthicsRuleMatch Evaluate(
                KLEPCivilianShotContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                bool teamMatches = context.DidHitEntity &&
                    context.AppliedDamage > 0f &&
                    StringComparer.Ordinal.Equals(
                        context.HitTeamId,
                        targetTeamId);
                bool applied = teamMatches &&
                    (!killOnly || context.TargetKilled);
                return new KLEPEthicsRuleMatch(
                    applied,
                    impulse,
                    applied
                        ? RuleId + ".applied"
                        : RuleId + ".not-applicable",
                    BuildEvidence(context));
            }

            private static string RequireRuleId(
                string value,
                string parameterName)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        "A civilian Ethics rule requires non-empty identities.",
                        parameterName);
                }

                return value;
            }
        }

        private sealed class AbstainingMemoryPolicy :
            IKLEPMemoryObserverEvidencePolicy
        {
            internal AbstainingMemoryPolicy(string observerStableId)
            {
                StableId = observerStableId + ".memory-abstain";
            }

            public string StableId { get; }
            public string Version => PolicyVersion;

            public KLEPMemoryCue CreateCue(
                KLEPObserverEvidenceContext context,
                KLEPExecutableDefinition eligibleTarget)
            {
                return null;
            }

            public KLEPCognitionEvidenceContribution Evaluate(
                KLEPObserverEvidenceContext context,
                KLEPExecutableDefinition eligibleTarget,
                KLEPMemoryRecallResult recall)
            {
                return null;
            }
        }

        private sealed class AbstainingEmotionPolicy :
            IKLEPEmotionObserverEvidencePolicy
        {
            internal AbstainingEmotionPolicy(string observerStableId)
            {
                StableId = observerStableId + ".emotion-abstain";
            }

            public string StableId { get; }
            public string Version => PolicyVersion;

            public KLEPCognitionEvidenceContribution Evaluate(
                KLEPObserverEvidenceContext context,
                KLEPExecutableDefinition eligibleTarget,
                KLEPEmotionObserverEvidenceState emotionState)
            {
                return null;
            }
        }
    }
}
