using System;
using System.Collections.Generic;
using System.Globalization;
using Roll4d4.Klep.Cognition;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.Observer;

namespace Roll4d4.Klep.ZombieTest
{
    /// <summary>
    /// Immutable project context for one factually applied zombie bite. It is
    /// evaluator input only; the retained Ethics record keeps its stable
    /// context identity and fully copied rule trace.
    /// </summary>
    internal sealed class KLEPZombieCognitionContext
    {
        internal KLEPZombieCognitionContext(
            string actorNeuronId,
            string actionStableId,
            long actionRunIndex,
            long actionCycle,
            string targetEntityId,
            string targetTeamId,
            long actionWorldTick,
            long consequenceWorldTick,
            float targetHealthBefore,
            float targetHealthAfter,
            bool targetKilled,
            string desireTransitionId)
        {
            ActorNeuronId = RequireId(actorNeuronId, nameof(actorNeuronId));
            ActionStableId = RequireId(
                actionStableId,
                nameof(actionStableId));
            TargetEntityId = RequireId(
                targetEntityId,
                nameof(targetEntityId));
            TargetTeamId = RequireId(targetTeamId, nameof(targetTeamId));
            DesireTransitionId = RequireId(
                desireTransitionId,
                nameof(desireTransitionId));
            if (actionRunIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionRunIndex));
            }

            if (actionCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionCycle));
            }

            if (actionWorldTick <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionWorldTick));
            }

            if (actionWorldTick == long.MaxValue ||
                consequenceWorldTick != actionWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(consequenceWorldTick),
                    "A bite consequence must be perceived on the next world Tick.");
            }

            RequireFiniteNonnegative(
                targetHealthBefore,
                nameof(targetHealthBefore));
            RequireFiniteNonnegative(
                targetHealthAfter,
                nameof(targetHealthAfter));
            if (targetHealthAfter >= targetHealthBefore)
            {
                throw new ArgumentException(
                    "A successful bite context requires positive applied damage.",
                    nameof(targetHealthAfter));
            }

            if (targetKilled != (targetHealthAfter <= 0f))
            {
                throw new ArgumentException(
                    "The bite kill fact must agree with the resulting health.",
                    nameof(targetKilled));
            }

            ActionRunIndex = actionRunIndex;
            ActionCycle = actionCycle;
            ActionWorldTick = actionWorldTick;
            ConsequenceWorldTick = consequenceWorldTick;
            TargetHealthBefore = targetHealthBefore;
            TargetHealthAfter = targetHealthAfter;
            TargetKilled = targetKilled;
        }

        internal string ActorNeuronId { get; }
        internal string ActionStableId { get; }
        internal long ActionRunIndex { get; }
        internal long ActionCycle { get; }
        internal string TargetEntityId { get; }
        internal string TargetTeamId { get; }
        internal long ActionWorldTick { get; }
        internal long ConsequenceWorldTick { get; }
        internal float TargetHealthBefore { get; }
        internal float TargetHealthAfter { get; }
        internal float AppliedDamage =>
            TargetHealthBefore - TargetHealthAfter;
        internal bool TargetKilled { get; }
        internal string DesireTransitionId { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A zombie bite context requires a non-empty identity.",
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
    /// Completed lower-branch transaction. The transition contains the exact
    /// Ethics, Emotion, Memory, and Desire records accepted atomically.
    /// </summary>
    internal sealed class KLEPZombieBiteExperienceResult
    {
        internal KLEPZombieBiteExperienceResult(
            long actionWorldTick,
            long consequenceWorldTick,
            string targetEntityId,
            string targetTeamId,
            float targetHealthBefore,
            float targetHealthAfter,
            bool targetKilled,
            KLEPCognitionTransition<KLEPZombieCognitionContext> transition)
        {
            ActionWorldTick = actionWorldTick;
            ConsequenceWorldTick = consequenceWorldTick;
            TargetEntityId = targetEntityId;
            TargetTeamId = targetTeamId;
            TargetHealthBefore = targetHealthBefore;
            TargetHealthAfter = targetHealthAfter;
            TargetKilled = targetKilled;
            Transition = transition ?? throw new ArgumentNullException(
                nameof(transition));
        }

        internal long ActionWorldTick { get; }
        internal long ConsequenceWorldTick { get; }
        internal string TargetEntityId { get; }
        internal string TargetTeamId { get; }
        internal float TargetHealthBefore { get; }
        internal float TargetHealthAfter { get; }
        internal float AppliedDamage =>
            TargetHealthBefore - TargetHealthAfter;
        internal bool TargetKilled { get; }
        internal KLEPCognitionTransition<KLEPZombieCognitionContext>
            Transition { get; }
    }

    /// <summary>
    /// Project-owned transaction seam for the demo's first real experience.
    /// The action Tick stages trusted world and Agent evidence. The first
    /// post-effect Neuron snapshot on the next Tick closes the consequence.
    /// </summary>
    internal sealed class KLEPZombieBiteExperienceCoordinator
    {
        private const string ContextSchemaId =
            "context.zombie.successful-bite";
        private const string ContextSchemaVersion = "1";

        private readonly string actorNeuronId;
        private readonly KLEPCognitionComposition<KLEPZombieCognitionContext>
            cognition;
        private PendingBite pending;
        private long lastCompletedActionRunIndex;

        internal KLEPZombieBiteExperienceCoordinator(
            string actorNeuronId,
            KLEPCognitionComposition<KLEPZombieCognitionContext> cognition)
        {
            this.actorNeuronId = RequireId(
                actorNeuronId,
                nameof(actorNeuronId));
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
                    "A pending bite must close instead of consuming its " +
                    "consequence Tick as an idle Emotion Tick.");
            }

            return cognition.AdvanceEmotionWithoutExperience(worldTick);
        }

        internal void StageSuccessfulBite(
            long worldTick,
            KLEPAgentTickTrace attackTrace,
            KLEPDesireEffectVector desireEffects,
            string targetEntityId,
            string targetTeamId,
            float targetHealthBefore,
            float targetHealthAfter,
            bool targetKilled)
        {
            if (pending != null)
            {
                throw new InvalidOperationException(
                    "Only one unresolved zombie bite experience may be staged.");
            }

            if (worldTick <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(worldTick));
            }

            if (attackTrace == null)
            {
                throw new ArgumentNullException(nameof(attackTrace));
            }

            if (attackTrace.Decision.Fault != null ||
                attackTrace.Decision.CycleIndex != worldTick)
            {
                throw new ArgumentException(
                    "The staged bite requires the exact unfaulted Agent trace " +
                    "from the same demo world Tick.",
                    nameof(attackTrace));
            }

            if (desireEffects == null)
            {
                throw new ArgumentNullException(nameof(desireEffects));
            }

            KLEPDesireAttributionEvidence attribution =
                desireEffects.Attribution;
            if (!attribution.IsEligibleForAutomaticExpectationLearning ||
                !attribution.ActionRunIndex.HasValue)
            {
                throw new ArgumentException(
                    "A staged bite requires exact ActionOwned Desire evidence.",
                    nameof(desireEffects));
            }

            long actionRunIndex = attribution.ActionRunIndex.Value;
            if (actionRunIndex <= lastCompletedActionRunIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(desireEffects),
                    "A completed bite action run cannot be staged again.");
            }

            string exactTargetId = RequireId(
                targetEntityId,
                nameof(targetEntityId));
            string exactTargetTeamId = RequireId(
                targetTeamId,
                nameof(targetTeamId));
            ValidateHealthFacts(
                targetHealthBefore,
                targetHealthAfter,
                targetKilled);

            KLEPExecutionResult actionResult = FindExactSuccessfulAction(
                attackTrace.Decision.ExecutableStates,
                attribution.ActionStableId,
                actionRunIndex,
                attackTrace.Decision.CycleIndex);
            string experienceId = BuildRunId(
                "experience",
                attribution.ActionStableId,
                actionRunIndex);
            string duringMomentId = experienceId + ".moment.during";
            if (StringComparer.Ordinal.Equals(
                    duringMomentId,
                    desireEffects.PriorMomentId) ||
                StringComparer.Ordinal.Equals(
                    duringMomentId,
                    desireEffects.ConsequenceMomentId) ||
                StringComparer.Ordinal.Equals(
                    desireEffects.PriorMomentId,
                    desireEffects.ConsequenceMomentId))
            {
                throw new ArgumentException(
                    "Bite experience moment identities must be distinct.",
                    nameof(desireEffects));
            }

            if (cognition.Emotion.Tick != worldTick)
            {
                throw new InvalidOperationException(
                    "The bite must be staged after Emotion advances exactly " +
                    "once for its action world Tick.");
            }

            KLEPMemoryMoment prior = KLEPMemoryMoment.Capture(
                desireEffects.PriorMomentId,
                KLEPMemoryMomentRole.Prior,
                attackTrace.Decision.InitialKeySnapshot);
            KLEPMemoryMoment during = KLEPMemoryMoment.Capture(
                duringMomentId,
                KLEPMemoryMomentRole.During,
                attackTrace.Decision.KeySnapshot);
            pending = new PendingBite(
                worldTick,
                experienceId,
                BuildRunId(
                    "evaluation",
                    attribution.ActionStableId,
                    actionRunIndex),
                BuildRunId(
                    "context",
                    attribution.ActionStableId,
                    actionRunIndex),
                exactTargetId,
                exactTargetTeamId,
                targetHealthBefore,
                targetHealthAfter,
                targetKilled,
                actionResult,
                prior,
                during,
                desireEffects);
        }

        internal KLEPZombieBiteExperienceResult CompletePendingBite(
            long worldTick,
            KLEPAgentTickTrace consequenceTrace)
        {
            PendingBite staged = pending ?? throw new InvalidOperationException(
                "No zombie bite experience is waiting for a consequence.");
            if (staged.ActionWorldTick == long.MaxValue ||
                worldTick != staged.ActionWorldTick + 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldTick),
                    "A zombie bite closes on the first world Tick after its action.");
            }

            if (consequenceTrace == null)
            {
                throw new ArgumentNullException(nameof(consequenceTrace));
            }

            if (consequenceTrace.Decision.Fault != null ||
                consequenceTrace.Decision.CycleIndex !=
                    staged.ActionResult.CycleIndex + 1 ||
                consequenceTrace.Decision.CycleIndex != worldTick)
            {
                throw new ArgumentException(
                    "A bite consequence requires the exact next unfaulted " +
                    "Agent trace.",
                    nameof(consequenceTrace));
            }

            KLEPMemoryMoment consequence = KLEPMemoryMoment.Capture(
                staged.DesireEffects.ConsequenceMomentId,
                KLEPMemoryMomentRole.Consequence,
                consequenceTrace.Decision.InitialKeySnapshot);
            KLEPMemoryActionOutcome actionOutcome =
                KLEPMemoryActionOutcome.Capture(
                    staged.ActionResult,
                    staged.ActionResult.CycleIndex,
                    staged.ActionResult.WaveIndex);
            var context = new KLEPZombieCognitionContext(
                actorNeuronId,
                staged.ActionResult.ExecutableStableId,
                staged.ActionResult.RunIndex,
                staged.ActionResult.CycleIndex,
                staged.TargetEntityId,
                staged.TargetTeamId,
                staged.ActionWorldTick,
                worldTick,
                staged.TargetHealthBefore,
                staged.TargetHealthAfter,
                staged.TargetKilled,
                staged.DesireEffects.TransitionId);
            var request = new KLEPCognitionExperienceRequest<
                KLEPZombieCognitionContext>(
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
                    actionOutcome: actionOutcome,
                    desireEffects: staged.DesireEffects);

            KLEPCognitionTransition<KLEPZombieCognitionContext> transition =
                cognition.Process(request);

            // Publish completion guards only after the entire coordinator
            // transaction succeeds. A rejected request remains retryable.
            lastCompletedActionRunIndex = staged.ActionResult.RunIndex;
            pending = null;
            return new KLEPZombieBiteExperienceResult(
                staged.ActionWorldTick,
                worldTick,
                staged.TargetEntityId,
                staged.TargetTeamId,
                staged.TargetHealthBefore,
                staged.TargetHealthAfter,
                staged.TargetKilled,
                transition);
        }

        private string BuildRunId(
            string domain,
            string actionStableId,
            long actionRunIndex)
        {
            return domain + "." + actorNeuronId + ".bite." +
                actionStableId + ".run." + actionRunIndex.ToString(
                    CultureInfo.InvariantCulture);
        }

        private static KLEPExecutionResult FindExactSuccessfulAction(
            IReadOnlyList<KLEPExecutableRuntimeSnapshot> roots,
            string actionStableId,
            long actionRunIndex,
            long actionCycle)
        {
            string exactActionId = RequireId(
                actionStableId,
                nameof(actionStableId));
            KLEPExecutionResult match = null;
            for (int i = 0; i < roots.Count; i++)
            {
                FindExactSuccessfulAction(
                    roots[i],
                    exactActionId,
                    actionRunIndex,
                    actionCycle,
                    ref match);
            }

            return match ?? throw new ArgumentException(
                "The bite receipt and Desire effect do not identify a " +
                "successful child action in the supplied Agent trace.",
                nameof(roots));
        }

        private static void FindExactSuccessfulAction(
            KLEPExecutableRuntimeSnapshot runtime,
            string actionStableId,
            long actionRunIndex,
            long actionCycle,
            ref KLEPExecutionResult match)
        {
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
                        "The exact bite child did not factually succeed in the " +
                        "declared action cycle.",
                        nameof(runtime));
                }

                if (match != null && !ReferenceEquals(match, result))
                {
                    throw new ArgumentException(
                        "The supplied trace contains more than one matching " +
                        "bite action result.",
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

        private static void ValidateHealthFacts(
            float healthBefore,
            float healthAfter,
            bool targetKilled)
        {
            if (float.IsNaN(healthBefore) ||
                float.IsInfinity(healthBefore) ||
                healthBefore < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthBefore));
            }

            if (float.IsNaN(healthAfter) ||
                float.IsInfinity(healthAfter) ||
                healthAfter < 0f ||
                healthAfter >= healthBefore)
            {
                throw new ArgumentOutOfRangeException(nameof(healthAfter));
            }

            if (targetKilled != (healthAfter <= 0f))
            {
                throw new ArgumentException(
                    "The bite kill fact must agree with resulting health.",
                    nameof(targetKilled));
            }
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A zombie bite experience requires a non-empty identity.",
                    parameterName);
            }

            return value;
        }

        private sealed class PendingBite
        {
            internal PendingBite(
                long actionWorldTick,
                string experienceId,
                string evaluationId,
                string contextId,
                string targetEntityId,
                string targetTeamId,
                float targetHealthBefore,
                float targetHealthAfter,
                bool targetKilled,
                KLEPExecutionResult actionResult,
                KLEPMemoryMoment priorMoment,
                KLEPMemoryMoment duringMoment,
                KLEPDesireEffectVector desireEffects)
            {
                ActionWorldTick = actionWorldTick;
                ExperienceId = experienceId;
                EvaluationId = evaluationId;
                ContextId = contextId;
                TargetEntityId = targetEntityId;
                TargetTeamId = targetTeamId;
                TargetHealthBefore = targetHealthBefore;
                TargetHealthAfter = targetHealthAfter;
                TargetKilled = targetKilled;
                ActionResult = actionResult;
                PriorMoment = priorMoment;
                DuringMoment = duringMoment;
                DesireEffects = desireEffects;
            }

            internal long ActionWorldTick { get; }
            internal string ExperienceId { get; }
            internal string EvaluationId { get; }
            internal string ContextId { get; }
            internal string TargetEntityId { get; }
            internal string TargetTeamId { get; }
            internal float TargetHealthBefore { get; }
            internal float TargetHealthAfter { get; }
            internal bool TargetKilled { get; }
            internal KLEPExecutionResult ActionResult { get; }
            internal KLEPMemoryMoment PriorMoment { get; }
            internal KLEPMemoryMoment DuringMoment { get; }
            internal KLEPDesireEffectVector DesireEffects { get; }
        }
    }

    /// <summary>
    /// Project-owned higher-cognition graph. Before an experience it supplies
    /// no selection influence. A factually applied bite against a human is
    /// evaluated positively for the zombie, moves Emotion, and is retained by
    /// Memory; the Observer's Memory and Emotion ranking policies still
    /// deliberately abstain.
    /// </summary>
    internal static class KLEPZombieCognitionDefaults
    {
        internal const string PolicyVersion = "zombie-bite-experience-1";
        internal const float HumanBiteValence = 0.25f;
        internal const float HumanBiteActivation = 0.10f;

        internal static KLEPCognitionComposition<KLEPZombieCognitionContext>
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
            var ethics = new KLEPEthics<KLEPZombieCognitionContext>(
                new KLEPWeightedEthicsEvaluator<KLEPZombieCognitionContext>(
                    observerStableId + ".ethics",
                    PolicyVersion,
                    axisXName,
                    axisYName,
                    KLEPEmotionVector.Zero,
                    new IKLEPWeightedEthicsRule<
                        KLEPZombieCognitionContext>[]
                    {
                        new HumanBiteEthicsRule()
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
            return new KLEPCognitionComposition<KLEPZombieCognitionContext>(
                observerStableId,
                PolicyVersion,
                ethics,
                emotion,
                memory,
                new AbstainingMemoryPolicy(observerStableId),
                new AbstainingEmotionPolicy(observerStableId),
                learnedExpectations: learnedExpectations);
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "The Zombie cognition composition requires a non-empty identity.",
                    parameterName);
            }
        }

        private sealed class HumanBiteEthicsRule :
            IKLEPWeightedEthicsRule<KLEPZombieCognitionContext>
        {
            public string RuleId => "zombie.bite-human";
            public float Weight => 1f;

            public KLEPEthicsRuleMatch Evaluate(
                KLEPZombieCognitionContext context)
            {
                bool applied = StringComparer.Ordinal.Equals(
                    context.TargetTeamId,
                    "team.human");
                return new KLEPEthicsRuleMatch(
                    applied,
                    new KLEPEmotionVector(
                        HumanBiteValence,
                        HumanBiteActivation),
                    applied
                        ? "zombie.bite-human-beneficial"
                        : "zombie.bite-nonhuman-unvalued",
                    new[]
                    {
                        "actor." + context.ActorNeuronId,
                        "action." + context.ActionStableId + ".run." +
                            context.ActionRunIndex.ToString(
                                CultureInfo.InvariantCulture),
                        "target." + context.TargetEntityId,
                        "target-team." + context.TargetTeamId,
                        "target-health-before." +
                            context.TargetHealthBefore.ToString(
                                "R",
                                CultureInfo.InvariantCulture),
                        "target-health-after." +
                            context.TargetHealthAfter.ToString(
                                "R",
                                CultureInfo.InvariantCulture),
                        "applied-damage." + context.AppliedDamage.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        "target-killed." + context.TargetKilled.ToString(),
                        "action-world.tick." +
                            context.ActionWorldTick.ToString(
                                CultureInfo.InvariantCulture),
                        "consequence-world.tick." +
                            context.ConsequenceWorldTick.ToString(
                                CultureInfo.InvariantCulture),
                        "desire-transition." + context.DesireTransitionId
                    });
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
