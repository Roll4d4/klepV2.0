using System;
using System.Collections.Generic;
using System.Reflection;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.LearnedExpectations;
using Roll4d4.Klep.Observer;
using Roll4d4.Klep.ZombieTest;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyIndependentOwnershipAndReadOnlyObserverSeam();
        VerifyRawDesireCriticMathAndIsolation();
        VerifyProductionPolicyFusesLearnedDesireIntoAgentSelection();
        VerifyCriticRejectsIneligibleAndReplayedEvidenceAtomically();
        VerifyZombieBodyToCriticBridge();

        Console.WriteLine(
            $"KLEP Learned Expectations smoke passed: {assertions} assertions.");
    }

    private static void VerifyIndependentOwnershipAndReadOnlyObserverSeam()
    {
        var learned = new KLEPLearnedExpectations(
            "observer.learned", "1", 2d);
        var observer = new KLEPObserver(
            "observer.learned",
            "1",
            learnedExpectations: learned);
        var plainObserver = new KLEPObserver("observer.plain", "1");

        Expect(ReferenceEquals(observer.LearnedExpectations, learned) &&
               plainObserver.LearnedExpectations == null,
            "Observer receives one optional read-only authority and constructs none");
        Expect(typeof(KLEPObserver).GetProperty("LearnedExpectations")
                   .PropertyType == typeof(IKLEPLearnedExpectationsView) &&
               typeof(KLEPObserver)
                   .GetMethod("QueryLearnedExpectation") != null &&
               typeof(IKLEPLearnedExpectationsView).GetMethod("Record") == null &&
               typeof(IKLEPLearnedExpectationsView).GetMethod("Reset") == null &&
               typeof(IKLEPLearnedExpectationsView).GetMethod("Replay") == null,
            "Observer's dependency type exposes query and snapshot but no mutation");
        Expect(typeof(KLEPObserver).GetProperty("Expectations") == null &&
               typeof(KLEPObserverConfiguration)
                   .GetProperty("ExpectationConfidenceScale") == null,
            "legacy Observer ownership and critic configuration surfaces are absent");
        Expect(typeof(IKLEPLearnedExpectationsDiagnosticView)
                   .GetMethod("Record") == null &&
               typeof(IKLEPLearnedExpectationsDiagnosticView)
                   .GetMethod("RecordDesireEffects") == null &&
               typeof(IKLEPLearnedExpectationsDiagnosticView)
                   .GetMethod("Reset") == null,
            "the combined diagnostic view exposes both evidence domains without mutation");
        Expect(Catch(() => new KLEPObserver(
                   "observer.other",
                   "1",
                   learnedExpectations: learned)) is ArgumentException,
            "a learned view cannot be relabeled under another Observer identity");
    }

    private static void VerifyRawDesireCriticMathAndIsolation()
    {
        var learned = new KLEPLearnedExpectations(
            "observer.critic", "1", 2d);
        var system = new KLEPDesireSystem<Context>(
            "creature.critic",
            new[]
            {
                new KLEPDesireDefinition<Context>(
                    "desire.fed",
                    "1",
                    100f,
                    new Evaluator("eval.fed", "1"))
            });

        KLEPDesireEffectVector first = EffectVector(
            system,
            1,
            0.1f,
            99f,
            0.6f,
            0f,
            "context.same",
            KLEPDesireEffectAttribution.ActionOwned,
            "action.eat",
            1);
        var firstTrial = new KLEPLearnedDesireEffectTrial(
            learned.OwnerStableId,
            learned.OwnerVersion,
            1,
            first);
        KLEPLearnedDesireEffectSnapshot firstSnapshot =
            learned.RecordDesireEffects(firstTrial);
        KLEPLearnedDesireEffectBucketIdentity bucket =
            KLEPLearnedDesireEffectBucketIdentity.From(
                first, first.Effects[0]);
        KLEPLearnedDesireEffectEstimate firstEstimate =
            learned.QueryDesireEffect(bucket);
        Expect(firstEstimate.Support == 1 &&
               Nearly(firstEstimate.MeanEffect, 0.5d) &&
               firstEstimate.SampleVariance == 0d &&
               Nearly(firstEstimate.LastPredictionError, 0.5d) &&
               Nearly(firstEstimate.Confidence, 1d / 3d),
            "first ActionOwned raw effect establishes mean, error, support, and confidence");
        Expect(firstSnapshot.Revision == 1 &&
               firstSnapshot.LastEvidenceSequence == 1 &&
               firstSnapshot.Estimates.Count == 1 &&
               learned.Revision == 0 &&
               learned.LastEvidenceSequence == 0,
            "Desire critic revision is independent of the exact later-Key ledger");

        KLEPDesireEffectVector second = EffectVector(
            system,
            3,
            0.2f,
            0f,
            0.4f,
            1000f,
            "context.same",
            KLEPDesireEffectAttribution.ActionOwned,
            "action.eat",
            2);
        learned.RecordDesireEffects(new KLEPLearnedDesireEffectTrial(
            learned.OwnerStableId,
            learned.OwnerVersion,
            2,
            second));
        KLEPLearnedDesireEffectEstimate secondEstimate =
            learned.QueryDesireEffect(bucket);
        Expect(secondEstimate.Support == 2 &&
               Nearly(secondEstimate.MeanEffect, 0.35d) &&
               Nearly(secondEstimate.SampleVariance, 0.045d) &&
               Nearly(secondEstimate.LastPredictionError, -0.3d) &&
               Nearly(secondEstimate.Confidence, 0.5d),
            "Welford update retains stable mean, sample variance, last error, and N over N plus scale confidence");
        Expect(Nearly(secondEstimate.MeanEffect,
                   (first.Effects[0].Effect + second.Effects[0].Effect) / 2d) &&
               first.Effects[0].Weight == 100f &&
               first.Effects[0].PressureBefore == 99f &&
               second.Effects[0].PressureAfter == 1000f,
            "critic learns raw effect without applying Desire weight or pressure");

        KLEPDesireEffectVector otherContext = EffectVector(
            system,
            5,
            0f,
            1f,
            1f,
            1f,
            "context.other",
            KLEPDesireEffectAttribution.ActionOwned,
            "action.eat",
            3);
        KLEPLearnedDesireEffectBucketIdentity otherBucket =
            KLEPLearnedDesireEffectBucketIdentity.From(
                otherContext, otherContext.Effects[0]);
        KLEPLearnedDesireEffectEstimate unknown =
            learned.QueryDesireEffect(otherBucket);
        Expect(!bucket.Equals(otherBucket) &&
               !unknown.IsKnown && unknown.Support == 0 &&
               unknown.Confidence == 0d,
            "exact prior context identity prevents learned effect bleed");
        Expect(IsReadOnlySnapshot(learned.CaptureDesireEffectSnapshot()),
            "critic snapshots and estimate rows are immutable read-only evidence");
    }

    private static void
        VerifyProductionPolicyFusesLearnedDesireIntoAgentSelection()
    {
        const string goalId = "selection.goal.eat";
        const string effectActionId = "selection.action.bite";
        const string discouragedId = "selection.action.harmful";
        const string unknownId = "selection.action.unknown";
        const string desireId = "selection.desire.fed";

        var desireSystem = new KLEPDesireSystem<Context>(
            "selection.creature",
            new[]
            {
                new KLEPDesireDefinition<Context>(
                    desireId,
                    "1",
                    2f,
                    new Evaluator("selection.eval.fed", "1"))
            });
        var learned = new KLEPLearnedExpectations(
            "selection.critic",
            "1",
            confidenceScale: 1d);

        KLEPDesireEffectVector helpful = EffectVector(
            desireSystem,
            1,
            0.25f,
            0.5f,
            0.75f,
            0.5f,
            "selection.context",
            KLEPDesireEffectAttribution.ActionOwned,
            effectActionId,
            1);
        learned.RecordDesireEffects(new KLEPLearnedDesireEffectTrial(
            learned.OwnerStableId,
            learned.OwnerVersion,
            1,
            helpful));

        KLEPDesireEffectVector harmful = EffectVector(
            desireSystem,
            3,
            0.75f,
            0.5f,
            0.25f,
            0.5f,
            "selection.context",
            KLEPDesireEffectAttribution.ActionOwned,
            discouragedId,
            2);
        learned.RecordDesireEffects(new KLEPLearnedDesireEffectTrial(
            learned.OwnerStableId,
            learned.OwnerVersion,
            2,
            harmful));

        KLEPDesireSnapshot current = desireSystem.Observe(
            new KLEPDesireObservationRequest<Context>(
                "selection.snapshot.current",
                5,
                "selection.moment.current",
                new KLEPDesireContextIdentity(
                    "selection.context", "critic-smoke", "1"),
                new Context(0.25f, 0.5f)));

        var bite = new ProbeExecutable(
            Definition(effectActionId, 0f),
            KLEPExecutableTickStatus.Succeeded);
        var goal = new KLEPGoal(
            Definition(
                goalId,
                0f,
                kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { bite })
            });
        var discouraged = new ProbeExecutable(
            Definition(discouragedId, 0.75f),
            KLEPExecutableTickStatus.Succeeded);
        var unknown = new ProbeExecutable(
            Definition(unknownId, 0.5f),
            KLEPExecutableTickStatus.Succeeded);

        var policy = new KLEPLearnedDesireSelectionPolicy(
            "selection.policy",
            "1",
            desireSystem,
            learned,
            new[]
            {
                Binding(goalId, effectActionId, desireId, 4f),
                Binding(discouragedId, discouragedId, desireId, 4f),
                Binding(unknownId, unknownId, desireId, 4f)
            });
        var neuron = new KLEPNeuron("selection.neuron");
        neuron.RegisterExecutable(goal);
        neuron.RegisterExecutable(discouraged);
        neuron.RegisterExecutable(unknown);
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            KLEPBaselineStructuralObserver.Instance,
            null,
            null,
            policy);

        KLEPAgentTickTrace trace = agent.Tick();
        CandidateEvaluation selected = Candidate(trace, goalId);
        CandidateEvaluation negative = Candidate(trace, discouragedId);
        CandidateEvaluation abstained = Candidate(trace, unknownId);
        KLEPLearnedDesireCandidateEvaluation selectedEvidence =
            LearnedEvidence(selected);
        KLEPLearnedDesireCandidateEvaluation negativeEvidence =
            LearnedEvidence(negative);
        KLEPLearnedDesireCandidateEvaluation unknownEvidence =
            LearnedEvidence(abstained);
        KLEPLearnedDesireContributionTrace selectedTerm =
            selectedEvidence.Desires[0];
        KLEPLearnedDesireContributionTrace negativeTerm =
            negativeEvidence.Desires[0];
        KLEPLearnedDesireContributionTrace unknownTerm =
            unknownEvidence.Desires[0];

        Expect(trace.Decision.SelectedExecutableId == goalId &&
               bite.TickCount == 1 &&
               selected.Score == 1f &&
               negative.Score == -0.25f &&
               abstained.Score == 0.5f,
            "the production policy changes only attraction and the Agent selects the best already-eligible root");
        Expect(selectedEvidence.EffectSourceExecutableId == effectActionId &&
               selectedEvidence.TargetExecutableId == goalId &&
               selectedEvidence.ScoreContribution == 1f &&
               selectedTerm.Disposition ==
                   KLEPLearnedDesireContributionDisposition.Applied &&
               selectedTerm.Support == 1 &&
               selectedTerm.MeanEffect == 0.5f &&
               selectedTerm.Confidence == 0.5f &&
               selectedTerm.CurrentWeight == 2f &&
               selectedTerm.CurrentPressure == 0.5f &&
               selectedTerm.SelectionScale == 4f &&
               selectedTerm.ScoreContribution == 1f,
            "an explicit Goal-root to child effect-action binding uses scale times Weight times Pressure times confidence times mean");
        Expect(negativeEvidence.ScoreContribution == -1f &&
               negativeTerm.MeanEffect == -0.5f &&
               negativeTerm.ScoreContribution == -1f &&
               negativeTerm.Disposition ==
                   KLEPLearnedDesireContributionDisposition.Applied,
            "signed harmful evidence lowers attraction with the same accepted formula");
        Expect(unknownEvidence.ScoreContribution == 0f &&
               unknownTerm.Support == 0 &&
               unknownTerm.Confidence == 0f &&
               unknownTerm.ScoreContribution == 0f &&
               unknownTerm.Disposition ==
                   KLEPLearnedDesireContributionDisposition.UnknownEvidence,
            "an explicitly bound but unseen action abstains at exactly zero and remains traceable");
        Expect(ReferenceEquals(
                   desireSystem.CurrentSnapshot, current) &&
               selectedEvidence.EvidenceFrame.DesireSnapshotId ==
                   current.SnapshotId &&
               selectedEvidence.EvidenceFrame.CriticRevision == 2 &&
               selectedEvidence.IsAgentEvidenceBound &&
               selectedEvidence.AgentCycleIndex == trace.Decision.CycleIndex &&
               selectedEvidence.CatalogFingerprint.Equals(
                   agent.ExecutableMap.Fingerprint),
            "the applied score retains the frozen Desire, critic, Agent-cycle, and structural-map evidence frame");
    }

    private static void
        VerifyCriticRejectsIneligibleAndReplayedEvidenceAtomically()
    {
        var learned = new KLEPLearnedExpectations(
            "observer.reject", "1");
        var system = new KLEPDesireSystem<Context>(
            "creature.reject",
            new[]
            {
                new KLEPDesireDefinition<Context>(
                    "desire.safety",
                    "1",
                    7f,
                    new Evaluator("eval.safety", "1"))
            });
        KLEPDesireEffectVector actionOwned = EffectVector(
            system,
            1,
            0.7f,
            2f,
            0.2f,
            9f,
            "context.reject",
            KLEPDesireEffectAttribution.ActionOwned,
            "action.risk",
            1);
        var trial = new KLEPLearnedDesireEffectTrial(
            learned.OwnerStableId,
            learned.OwnerVersion,
            1,
            actionOwned);
        learned.RecordDesireEffects(trial);
        long revision = learned.DesireEffectRevision;
        KLEPLearnedDesireEffectSnapshot before =
            learned.CaptureDesireEffectSnapshot();

        Exception replay = Catch(() => learned.RecordDesireEffects(trial));
        Exception wrongOwner = Catch(() => learned.RecordDesireEffects(
            new KLEPLearnedDesireEffectTrial(
                "observer.wrong",
                "1",
                2,
                actionOwned)));
        KLEPDesireEffectVector external = EffectVector(
            system,
            3,
            0.2f,
            0f,
            0.3f,
            0f,
            "context.reject",
            KLEPDesireEffectAttribution.External,
            null,
            null);
        Exception ineligible = Catch(() =>
            new KLEPLearnedDesireEffectTrial(
                learned.OwnerStableId,
                learned.OwnerVersion,
                2,
                external));
        KLEPLearnedDesireEffectSnapshot after =
            learned.CaptureDesireEffectSnapshot();
        Expect(replay is InvalidOperationException &&
               wrongOwner is ArgumentException &&
               ineligible is ArgumentException,
            "replay, foreign ownership, and non-ActionOwned effects are rejected");
        Expect(after.Revision == revision &&
               after.LastEvidenceSequence == 1 &&
               after.Estimates.Count == 1 &&
               ReferenceEquals(
                   before.Estimates[0].Bucket,
                   after.Estimates[0].Bucket) &&
               Nearly(
                   before.Estimates[0].MeanEffect,
                   after.Estimates[0].MeanEffect),
            "rejected critic evidence publishes no partial mutation");
    }

    private static void VerifyZombieBodyToCriticBridge()
    {
        var learned = new KLEPLearnedExpectations(
            "observer.zombie-bridge",
            "1");
        var bridge = new KLEPZombieDesireLearningBridge(learned);
        var body = new KLEPZombieDesireBody(
            "desire-body.zombie-bridge",
            "action.zombie.attack",
            initialHunger01: 0.6f,
            metabolismPerWorldTick: 0.1f,
            successfulBiteRelief: 0.3f,
            fedDesireWeight: 1f);

        KLEPZombieDesireStep metabolism = body.AdvanceMetabolism(1);
        Exception externalRejected = Catch(() =>
            bridge.RecordActionOwnedEffects(metabolism));
        Expect(externalRejected is ArgumentException &&
               bridge.EvidenceSequence == 0 &&
               learned.DesireEffectRevision == 0,
            "passive metabolism cannot enter the action-owned critic bridge");

        KLEPZombieDesireStep bite = body.RecordSuccessfulBite(
            1,
            "action.zombie.attack",
            1,
            "entity.human");
        KLEPLearnedDesireEffectSnapshot accepted =
            bridge.RecordActionOwnedEffects(bite);
        Expect(accepted.Estimates.Count == 1 &&
               accepted.Estimates[0].Support == 1 &&
               Nearly(accepted.Estimates[0].MeanEffect, 0.3d),
            "one factual bite trains exactly one raw fed-Desire estimate");
        Expect(bridge.EvidenceSequence == 1 &&
               bridge.LastActionRunIndex == 1 &&
               bridge.LastTransitionId == bite.Effects.TransitionId,
            "the bridge advances its writer guards only after critic acceptance");

        Exception replayRejected = Catch(() =>
            bridge.RecordActionOwnedEffects(bite));
        KLEPLearnedDesireEffectSnapshot afterReplay =
            learned.CaptureDesireEffectSnapshot();
        Expect(replayRejected is InvalidOperationException &&
               afterReplay.Revision == 1 &&
               afterReplay.Estimates[0].Support == 1,
            "the same factual bite cannot train the critic twice");
    }

    private static KLEPDesireEffectVector EffectVector(
        KLEPDesireSystem<Context> system,
        long priorTick,
        float priorSatisfaction,
        float priorPressure,
        float consequenceSatisfaction,
        float consequencePressure,
        string contextId,
        KLEPDesireEffectAttribution attribution,
        string actionStableId,
        long? actionRunIndex)
    {
        var contextIdentity = new KLEPDesireContextIdentity(
            contextId, "critic-smoke", "1");
        KLEPDesireSnapshot prior = system.Observe(
            new KLEPDesireObservationRequest<Context>(
                "snapshot." + priorTick,
                priorTick,
                "moment." + priorTick,
                contextIdentity,
                new Context(priorSatisfaction, priorPressure)));
        long consequenceTick = priorTick + 1;
        KLEPDesireSnapshot consequence = system.Observe(
            new KLEPDesireObservationRequest<Context>(
                "snapshot." + consequenceTick,
                consequenceTick,
                "moment." + consequenceTick,
                contextIdentity,
                new Context(
                    consequenceSatisfaction,
                    consequencePressure)));
        return system.EvaluateTransition(new KLEPDesireTransitionRequest(
            "transition." + priorTick,
            prior,
            consequence,
            new KLEPDesireAttributionEvidence(
                attribution,
                "provenance." + priorTick,
                actionStableId,
                actionRunIndex)));
    }

    private static KLEPLearnedDesireCandidateBinding Binding(
        string candidateRootId,
        string effectSourceId,
        string desireId,
        float selectionScale)
    {
        return new KLEPLearnedDesireCandidateBinding(
            candidateRootId,
            effectSourceId,
            new[]
            {
                new KLEPLearnedDesireBindingTerm(
                    desireId, selectionScale)
            });
    }

    private static KLEPExecutableDefinition Definition(
        string stableId,
        float score,
        KLEPExecutableKind kind = KLEPExecutableKind.Action)
    {
        return new KLEPExecutableDefinition(
            stableId,
            stableId,
            kind,
            baseAttractiveness: score);
    }

    private static CandidateEvaluation Candidate(
        KLEPAgentTickTrace trace,
        string stableId)
    {
        for (int index = 0; index < trace.Decision.Candidates.Count; index++)
        {
            CandidateEvaluation candidate = trace.Decision.Candidates[index];
            if (StringComparer.Ordinal.Equals(candidate.StableId, stableId))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Candidate '{stableId}' was not present in the Agent trace.");
    }

    private static KLEPLearnedDesireCandidateEvaluation LearnedEvidence(
        CandidateEvaluation candidate)
    {
        for (int index = 0;
             index < candidate.ScoreEvaluation.Components.Count;
             index++)
        {
            KLEPExecutableScoreComponent component =
                candidate.ScoreEvaluation.Components[index];
            if (component.Kind ==
                KLEPExecutableScoreComponentKind.LearnedDesireExpectation)
            {
                return component.LearnedDesireExpectation;
            }
        }

        throw new InvalidOperationException(
            $"Candidate '{candidate.StableId}' had no learned Desire evidence.");
    }

    private static bool IsReadOnlySnapshot(
        KLEPLearnedDesireEffectSnapshot snapshot)
    {
        if (!((IList<KLEPLearnedDesireEffectEstimate>)snapshot.Estimates)
                .IsReadOnly)
        {
            return false;
        }

        foreach (PropertyInfo property in snapshot.GetType().GetProperties())
        {
            if (property.GetSetMethod(nonPublic: false) != null)
            {
                return false;
            }
        }

        foreach (PropertyInfo property in snapshot.Estimates[0]
                     .GetType().GetProperties())
        {
            if (property.GetSetMethod(nonPublic: false) != null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Nearly(double left, double right)
    {
        return Math.Abs(left - right) < 0.0000001d;
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(
            "Expected an exception, but none was thrown.");
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

    private sealed class Context
    {
        internal Context(float satisfaction, float pressure)
        {
            Satisfaction = satisfaction;
            Pressure = pressure;
        }

        internal float Satisfaction { get; }
        internal float Pressure { get; }
    }

    private sealed class Evaluator : IKLEPDesireEvaluator<Context>
    {
        internal Evaluator(string id, string version)
        {
            EvaluatorId = id;
            EvaluatorVersion = version;
        }

        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }

        public KLEPDesireAssessment Evaluate(Context context, long desireTick)
        {
            return new KLEPDesireAssessment(
                context.Satisfaction,
                context.Pressure,
                "critic smoke at " + desireTick,
                new[] { "evidence." + desireTick });
        }
    }

    private sealed class ProbeExecutable : KLEPExecutableBase
    {
        private readonly KLEPExecutableTickStatus status;

        internal ProbeExecutable(
            KLEPExecutableDefinition definition,
            KLEPExecutableTickStatus status)
            : base(definition)
        {
            this.status = status;
        }

        internal int TickCount { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            return status;
        }
    }
}
