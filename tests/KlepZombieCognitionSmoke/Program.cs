using System;
using Roll4d4.Klep.Cognition;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.LearnedExpectations;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private const string NeuronId = "neuron.zombie.bite-smoke";
    private const string GoalId = "goal.zombie.eat-human";
    private const string AttackId = "action.zombie.attack";
    private const string TargetId = "entity.human.bite-smoke";
    private const string HumanTeamId = "team.human";

    private static int assertions;

    private static int Main()
    {
        try
        {
            OneRealBiteClosesBeforeCriticLearning();
            Console.WriteLine(
                $"KLEP zombie cognition smoke passed ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void OneRealBiteClosesBeforeCriticLearning()
    {
        var learned = new KLEPLearnedExpectations(
            "observer.zombie.bite-smoke",
            KLEPZombieCognitionDefaults.PolicyVersion);
        KLEPCognitionComposition<KLEPZombieCognitionContext> cognition =
            KLEPZombieCognitionDefaults.Create(
                "memory.zombie.bite-smoke",
                "observer.zombie.bite-smoke",
                "valence",
                "activation",
                learned);
        var experience = new KLEPZombieBiteExperienceCoordinator(
            NeuronId,
            cognition);
        var body = new KLEPZombieDesireBody(
            "desire.zombie.bite-smoke",
            AttackId,
            initialHunger01: 0.60f,
            metabolismPerWorldTick: 0.10f,
            successfulBiteRelief: 0.35f,
            fedDesireWeight: 1f);
        var critic = new KLEPZombieDesireLearningBridge(learned);
        KLEPAgent agent = CreateAgent();

        KLEPZombieDesireStep metabolismAtAction =
            body.AdvanceMetabolism(worldTick: 1);
        KLEPAgentTickTrace attackTrace = agent.Tick();
        KLEPExecutionResult attackResult = ExactAttackResult(attackTrace);

        KLEPEmotionSnapshot idle =
            experience.AdvanceEmotionWithoutExperience(worldTick: 1);
        Expect(idle.Tick == 1 && idle.Influences.Count == 0,
            "the action world Tick is an explicit friction-only Emotion Tick");
        Expect(idle.Position == KLEPEmotionVector.Zero &&
               idle.Velocity == KLEPEmotionVector.Zero,
            "idle Emotion remains at rest before the bite is evaluated");
        Expect(cognition.Coordinator.LastTransition == null,
            "an idle Emotion Tick invents no cognition experience");
        Expect(cognition.Memory.Snapshot.Tick == 0 &&
               cognition.Memory.Snapshot.Clusters.Count == 0,
            "the bite is not in Memory before its consequence exists");
        Expect(learned.DesireEffectRevision == 0 &&
               learned.CaptureDesireEffectSnapshot().Estimates.Count == 0,
            "the critic is untrained before factual closure");

        KLEPZombieDesireStep bite = body.RecordSuccessfulBite(
            worldTick: 1,
            actionStableId: attackResult.ExecutableStableId,
            actionRunIndex: attackResult.RunIndex,
            targetEntityId: TargetId);
        experience.StageSuccessfulBite(
            worldTick: 1,
            attackTrace: attackTrace,
            desireEffects: bite.Effects,
            targetEntityId: TargetId,
            targetTeamId: HumanTeamId,
            targetHealthBefore: 1f,
            targetHealthAfter: 0.75f,
            targetKilled: false);

        Expect(experience.HasPendingExperience,
            "a successful bite waits for the first post-effect snapshot");
        Expect(bite.Effects.Attribution.ActionStableId == AttackId &&
               bite.Effects.Attribution.ActionRunIndex == attackResult.RunIndex,
            "Desire retains the exact successful child action/run identity");
        ExpectNear(
            bite.Effects.Effects[0].Effect,
            0.35f,
            "the factual bite produces the raw fed-Desire effect");
        Expect(metabolismAtAction.Effects.Attribution.Kind ==
               KLEPDesireEffectAttribution.External,
            "same-Tick metabolism remains separate External evidence");

        Exception idleWhilePending = Catch(() =>
            experience.AdvanceEmotionWithoutExperience(worldTick: 2));
        Exception wrongCloseTick = Catch(() =>
            experience.CompletePendingBite(
                worldTick: 3,
                consequenceTrace: attackTrace));
        Expect(idleWhilePending is InvalidOperationException &&
               wrongCloseTick is ArgumentOutOfRangeException,
            "a pending bite cannot be skipped, consumed as idle, or closed late");
        Expect(experience.HasPendingExperience &&
               cognition.Emotion.Tick == 1 &&
               cognition.Memory.Snapshot.Tick == 0 &&
               learned.DesireEffectRevision == 0,
            "rejected closure attempts publish no partial subsystem mutation");

        body.AdvanceMetabolism(worldTick: 2);
        KLEPAgentTickTrace consequenceTrace = agent.Tick();
        KLEPZombieBiteExperienceResult completed =
            experience.CompletePendingBite(
                worldTick: 2,
                consequenceTrace: consequenceTrace);
        KLEPCognitionTransition<KLEPZombieCognitionContext> transition =
            completed.Transition;

        Expect(!experience.HasPendingExperience,
            "the exact next trace closes the pending bite once");
        Expect(completed.ActionWorldTick == 1 &&
               completed.ConsequenceWorldTick == 2 &&
               completed.TargetEntityId == TargetId &&
               completed.TargetTeamId == HumanTeamId,
            "the completed transaction retains exact world and target facts");
        ExpectNear(completed.AppliedDamage, 0.25f,
            "the completed transaction retains factual applied damage");

        Expect(transition.EthicsEvaluation.ContextIdentity.ContextId.Contains(
                   AttackId) &&
               transition.EthicsEvaluation.ContextIdentity.ContextId.Contains(
                   ".run." + attackResult.RunIndex),
            "Ethics retains a context identity bound to the exact action run");
        Expect(transition.EthicsEvaluation.Judgment.Trace.Count == 2 &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "actor." + NeuronId) &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "action." + AttackId + ".run." + attackResult.RunIndex),
            "Ethics traces the exact actor/action/run identity");
        Expect(Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "target." + TargetId) &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "target-team." + HumanTeamId) &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "target-health-before.1") &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "target-health-after.0.75") &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "applied-damage.0.25") &&
               Contains(
                   transition.EthicsEvaluation.Judgment.Trace[1].EvidenceIds,
                   "desire-transition." + bite.Effects.TransitionId),
            "Ethics traces exact target, health, and Desire-transition facts");
        ExpectNear(
            transition.EthicsEvaluation.Judgment.Impulse.X,
            KLEPZombieCognitionDefaults.HumanBiteValence,
            "zombie Ethics evaluates a human bite with +0.25 valence");
        ExpectNear(
            transition.EthicsEvaluation.Judgment.Impulse.Y,
            KLEPZombieCognitionDefaults.HumanBiteActivation,
            "zombie Ethics evaluates a human bite with +0.10 activation");

        KLEPEmotionSnapshot emotion = transition.EmotionSnapshot;
        Expect(emotion.Tick == 2 && emotion.Influences.Count == 1,
            "the Ethics result advances exactly one consecutive Emotion Tick");
        ExpectNear(emotion.NetInfluence.X, 0.25f,
            "Emotion receives the exact Ethics valence impulse");
        ExpectNear(emotion.NetInfluence.Y, 0.10f,
            "Emotion receives the exact Ethics activation impulse");
        ExpectNear(emotion.Position.X, 0.25f,
            "the bite moves the valence position");
        ExpectNear(emotion.Position.Y, 0.10f,
            "the bite moves the activation position");

        KLEPMemoryExperience episode = transition.Experience;
        Expect(episode.RecordedTick == 2 &&
               episode.ActionOutcome != null &&
               episode.ActionOutcome.ExecutableStableId == AttackId &&
               episode.ActionOutcome.RunIndex == attackResult.RunIndex &&
               episode.ActionOutcome.WasSuccessful,
            "Memory archives the exact successful child action/run outcome");
        Expect(episode.Moments.Count == 3 &&
               episode.Moments[0].Role == KLEPMemoryMomentRole.Prior &&
               episode.Moments[1].Role == KLEPMemoryMomentRole.During &&
               episode.Moments[2].Role == KLEPMemoryMomentRole.Consequence,
            "Memory preserves the ordered Prior-During-Consequence episode");
        Expect(episode.Moments[0].MomentId == bite.Effects.PriorMomentId &&
               episode.Moments[2].MomentId ==
                   bite.Effects.ConsequenceMomentId &&
               episode.Moments[1].MomentId != episode.Moments[0].MomentId &&
               episode.Moments[1].MomentId != episode.Moments[2].MomentId,
            "Memory uses the exact Desire endpoints and a distinct During identity");
        Expect(episode.DesireEffects != null &&
               episode.DesireEffects.TransitionId == bite.Effects.TransitionId &&
               episode.DesireEffects.Attribution.ActionStableId == AttackId &&
               episode.DesireEffects.Attribution.ActionRunIndex ==
                   attackResult.RunIndex,
            "Memory retains the original ActionOwned Desire vector");
        Expect(episode.Ethics.Count == 1 &&
               episode.Emotion != null,
            "the Memory episode contains both Ethics and emotional consequence");
        Expect(transition.MemorySnapshot.Tick == 2 &&
               transition.MemorySnapshot.Clusters.Count == 1 &&
               transition.MemorySnapshot.Clusters[0].RecentEpisodes.Count == 1,
            "one bite creates exactly one retained Memory episode");

        Expect(learned.DesireEffectRevision == 0,
            "Memory closes before the derived critic is allowed to learn");
        KLEPLearnedDesireEffectSnapshot learnedAfterMemory =
            critic.RecordActionOwnedEffects(transition.DesireEffects);
        Expect(learnedAfterMemory.Revision == 1 &&
               learnedAfterMemory.Estimates.Count == 1 &&
               learnedAfterMemory.Estimates[0].Support == 1,
            "the critic gains one support only after factual Memory exists");
        ExpectNear(
            (float)learnedAfterMemory.Estimates[0].MeanEffect,
            bite.Effects.Effects[0].Effect,
            "the critic learns the original raw Desire effect without revaluation");
        Expect(critic.LastActionRunIndex == attackResult.RunIndex &&
               critic.LastTransitionId == bite.Effects.TransitionId,
            "critic writer guards retain the exact learned action/run transition");

        Exception completionReplay = Catch(() =>
            experience.CompletePendingBite(
                worldTick: 2,
                consequenceTrace: consequenceTrace));
        Exception criticReplay = Catch(() =>
            critic.RecordActionOwnedEffects(transition.DesireEffects));
        KLEPLearnedDesireEffectSnapshot afterReplay =
            learned.CaptureDesireEffectSnapshot();
        Expect(completionReplay is InvalidOperationException &&
               criticReplay is InvalidOperationException,
            "neither the Memory transaction nor critic training can replay");
        Expect(cognition.Memory.Snapshot.Clusters[0]
                   .RecentEpisodes.Count == 1 &&
               afterReplay.Revision == 1 &&
               afterReplay.Estimates[0].Support == 1,
            "replay rejection leaves one episode and one critic support");
    }

    private static KLEPAgent CreateAgent()
    {
        var attack = new SuccessfulAttackExecutable(
            new KLEPExecutableDefinition(
                AttackId,
                "Bite Human",
                KLEPExecutableKind.Action));
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                GoalId,
                "Eat Human",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 10f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { attack })
            });
        var neuron = new KLEPNeuron(NeuronId);
        neuron.RegisterExecutable(goal);
        return new KLEPAgent(neuron);
    }

    private static KLEPExecutionResult ExactAttackResult(
        KLEPAgentTickTrace trace)
    {
        Expect(trace != null && trace.Decision != null &&
               trace.Decision.Fault == null &&
               trace.Decision.CycleIndex == 1,
            "the real Agent produces the action world Tick trace");
        Expect(trace.Decision.ExecutableStates.Count == 1,
            "the fixture has exactly one root Goal runtime");
        KLEPExecutableRuntimeSnapshot root =
            trace.Decision.ExecutableStates[0];
        Expect(root.ExecutableStableId == GoalId && root.Goal != null &&
               root.Goal.Layers.Count == 1 &&
               root.Goal.Layers[0].Children.Count == 1,
            "the recursive runtime exposes the authored Goal/child structure");
        KLEPExecutionResult result =
            root.Goal.Layers[0].Children[0].Runtime.LastResult;
        Expect(result != null &&
               result.ExecutableStableId == AttackId &&
               result.State == KLEPExecutableState.Succeeded &&
               result.ExitReason == KLEPExecutableExitReason.Succeeded &&
               result.CycleIndex == trace.Decision.CycleIndex,
            "the bite evidence comes from a real successful Goal child result");
        return result;
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static bool Contains(
        System.Collections.Generic.IReadOnlyList<string> values,
        string expected)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (StringComparer.Ordinal.Equals(values[index], expected))
            {
                return true;
            }
        }

        return false;
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(
                "Assertion failed: " + message);
        }
    }

    private static void ExpectNear(float actual, float expected, string message)
    {
        Expect(Math.Abs(actual - expected) <= 0.00001f,
            message + $" (expected {expected}, actual {actual})");
    }

    private sealed class SuccessfulAttackExecutable : KLEPExecutableBase
    {
        internal SuccessfulAttackExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
