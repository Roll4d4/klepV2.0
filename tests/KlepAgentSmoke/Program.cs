using System;
using System.Collections.Generic;
using System.Reflection;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyConfigurationDefaults();
        VerifyProjectedSatisfactionUsesWeightedDeltaAndAuthoredOrder();
        VerifyUnitDesiresEqualNetSatisfiedCount();
        VerifyProjectedSatisfactionValidationAndCompleteStateSemantics();
        VerifyProjectedSatisfactionAppendsOneAggregateScoreComponent();
        VerifyBaselineProjectionAbstainsForEarlierOneCycleEmission();
        VerifyProjectedSatisfactionReordersOnlyEligibleSoloRoots();
        VerifyProjectedSatisfactionUsesPostTandemSnapshot();
        VerifyMismatchedProjectionFaultsAndIsTraced();
        VerifyAgentScoreLayersEndWithOptionalObserverPolish();
        VerifyLearnedDesirePositiveEvidenceReordersEligibleSolos();
        VerifyLearnedDesireNegativeUnknownAndTieSemantics();
        VerifyLearnedDesireReceivesPostTandemEligibleRootsOnly();
        VerifyLearnedDesirePrecedesObserverPolish();
        VerifyMalformedLearnedDesireBatchFaultsAtomically();
        VerifyLearnedDesireBindingMutationFaults();
        VerifyLearnedDesireOverflowFaultsAtomically();
        VerifyStructuralMapCapturesRecursiveCatalog();
        VerifyStructuralMapFingerprintAndProjectionAreDeterministic();
        VerifyStructuralMapReturnsValidationDiagnostics();
        VerifyAgentStructuralMapLifecycleAndRejectedProposal();
        VerifyStructuralObserverFaultsAreFrozenAndRethrown();
        VerifyAgentAdvancesNeuronExactlyOnce();
        VerifyAgentOwnsOneContinuousNeuronTickPath();
        VerifyEnvironmentSignatureUsesStableKeyIdsOnly();
        VerifyGuidanceFingerprintTracksPayloadWithoutSplittingLearningState();
        VerifyGuidanceFingerprintCanonicalizesSupportedEvidence();
        VerifyEnvironmentEntryIncludesScope();
        VerifyTandemOutputChangesEnvironmentInSameTick();
        VerifySoloOutputChangesEnvironmentOnFollowingTick();
        VerifyDurationCountsAgentOwnedTicksNotWorldIndexDistance();
        VerifyRepeatedGoalSuccessRaisesConfidence();
        VerifyInterruptionLearnsExactlyOnce();
        VerifyInterruptionSurvivesFaultingChallenger();
        VerifyFailureLearnsOnFollowingTick();
        VerifyNonLearningCancellationDoesNotChangeQ();
        VerifyGuidanceIsImmutableAndDoesNotOverrideSelection();
        VerifyGuidanceObserverCannotMutateAgentBoundary();
        VerifyEquivalentRunsAreDeterministic();
        VerifyFaultIsObservedAndRethrownUnchanged();

        Console.WriteLine($"KLEP Agent smoke passed: {assertions} assertions.");
    }

    private static void VerifyProjectedSatisfactionUsesWeightedDeltaAndAuthoredOrder()
    {
        var policy = new KLEPProjectedSatisfactionPolicy(
            new KLEPAgentDesire[]
            {
                new KLEPAgentDesire(
                    "desire.food",
                    "1",
                    new KLEPKeyPresent("key.food"),
                    2f,
                    3f,
                    "Acquire food."),
                new KLEPAgentDesire(
                    "desire.safety",
                    "2",
                    new KLEPNot(new KLEPKeyPresent("key.danger")),
                    4f,
                    0.5f,
                    "Remain out of danger."),
                new KLEPAgentDesire(
                    "desire.signed",
                    "1",
                    new KLEPKeyPresent("key.rest"),
                    -2f,
                    -0.5f,
                    "Signed project policy remains legal."),
                new KLEPAgentDesire(
                    "desire.shelter",
                    "1",
                    new KLEPAny(
                        new KLEPKeyPresent("key.shelter"),
                        new KLEPKeyPresent("key.camp")),
                    7f,
                    1.25f,
                    "Either shelter satisfies this one desire.")
            });

        KLEPProjectedSatisfactionEvaluation evaluation = policy.Evaluate(
            "goal.survive",
            new CompleteKeyState("key.shelter"),
            new CompleteKeyState(
                "key.food",
                "key.danger",
                "key.rest",
                "key.camp"));

        Expect(evaluation.Desires.Count == 4 &&
               evaluation.Desires[0].DesireStableId == "desire.food" &&
               evaluation.Desires[1].DesireStableId == "desire.safety" &&
               evaluation.Desires[2].DesireStableId == "desire.signed" &&
               evaluation.Desires[3].DesireStableId == "desire.shelter",
            "Projected satisfaction preserves authored desire order in its trace");
        Expect(evaluation.Desires[0].CurrentTruth == 0 &&
               evaluation.Desires[0].ProjectedTruth == 1 &&
               evaluation.Desires[0].Contribution == 6d,
            "Projected satisfaction applies weight times pressure to a newly satisfied desire");
        Expect(evaluation.Desires[1].CurrentTruth == 1 &&
               evaluation.Desires[1].ProjectedTruth == 0 &&
               evaluation.Desires[1].Contribution == -2d,
            "Projected satisfaction retains a negative contribution when a desire is lost");
        Expect(evaluation.Desires[2].Weight == -2f &&
               evaluation.Desires[2].Pressure == -0.5f &&
               evaluation.Desires[2].Contribution == 1d,
            "Finite signed designer weight and pressure are accepted without normalization");
        Expect(evaluation.Desires[3].CurrentTruth == 1 &&
               evaluation.Desires[3].ProjectedTruth == 1 &&
               evaluation.Desires[3].Contribution == 0d,
            "A desire that remains satisfied contributes zero even when different leaves satisfy it");
        Expect(evaluation.RawTotal == 5d &&
               evaluation.ScoreContribution == 5f &&
               evaluation.TargetExecutableId == "goal.survive" &&
               evaluation.Desires[0].DesireVersion == "1" &&
               evaluation.Desires[0].Explanation == "Acquire food.",
            "Projected satisfaction exposes the checked total and complete per-desire provenance");
    }

    private static void VerifyUnitDesiresEqualNetSatisfiedCount()
    {
        var policy = new KLEPProjectedSatisfactionPolicy(
            new KLEPAgentDesire[]
            {
                UnitDesire("desire.unit.a", new KLEPKeyPresent("key.a")),
                UnitDesire("desire.unit.b", new KLEPKeyPresent("key.b")),
                UnitDesire(
                    "desire.unit.not-d",
                    new KLEPNot(new KLEPKeyPresent("key.d"))),
                UnitDesire(
                    "desire.unit.all",
                    new KLEPAll(
                        new KLEPKeyPresent("key.c"),
                        new KLEPKeyPresent("key.b"))),
                UnitDesire(
                    "desire.unit.any",
                    new KLEPAny(
                        new KLEPKeyPresent("key.a"),
                        new KLEPKeyPresent("key.z")))
            });

        KLEPProjectedSatisfactionEvaluation evaluation = policy.Evaluate(
            "action.unit-count",
            new CompleteKeyState("key.a", "key.c"),
            new CompleteKeyState("key.b", "key.c", "key.z"));

        int currentSatisfied = 0;
        int projectedSatisfied = 0;
        for (int index = 0; index < evaluation.Desires.Count; index++)
        {
            currentSatisfied += evaluation.Desires[index].CurrentTruth;
            projectedSatisfied += evaluation.Desires[index].ProjectedTruth.Value;
        }

        Expect(currentSatisfied == 3 && projectedSatisfied == 4,
            "Composite desired expressions are each counted as one boolean desire");
        Expect(evaluation.RawTotal ==
                   projectedSatisfied - currentSatisfied &&
               evaluation.ScoreContribution == 1f,
            "Unit weights and pressures exactly equal the net change in satisfied-desire count");
    }

    private static void VerifyProjectedSatisfactionValidationAndCompleteStateSemantics()
    {
        Expect(Catch(() => new KLEPAgentDesire(
                   " ", "1", new KLEPKeyPresent("key.valid"), 1f))
                   is ArgumentException,
            "A desire requires a stable identity");
        Expect(Catch(() => new KLEPAgentDesire(
                   "desire.version", "", new KLEPKeyPresent("key.valid"), 1f))
                   is ArgumentException,
            "A desire requires a stable version");
        Expect(Catch(() => new KLEPAgentDesire(
                   "desire.expression", "1", null, 1f))
                   is ArgumentNullException,
            "A desire requires an immutable Lock expression");
        Expect(Catch(() => new KLEPAgentDesire(
                   "desire.weight", "1", new KLEPKeyPresent("key.valid"),
                   float.NaN)) is ArgumentOutOfRangeException,
            "A desire rejects a non-finite weight");
        Expect(Catch(() => new KLEPAgentDesire(
                   "desire.pressure", "1", new KLEPKeyPresent("key.valid"),
                   1f, float.NegativeInfinity)) is ArgumentOutOfRangeException,
            "A desire rejects a non-finite pressure");

        var authored = new List<KLEPAgentDesire>
        {
            UnitDesire("desire.copy.first", new KLEPKeyPresent("key.first")),
            UnitDesire("desire.copy.second", new KLEPKeyPresent("key.second"))
        };
        var policy = new KLEPProjectedSatisfactionPolicy(authored);
        authored.Clear();
        Expect(policy.Desires.Count == 2 &&
               policy.Desires[0].StableId == "desire.copy.first" &&
               policy.Desires[1].StableId == "desire.copy.second",
            "Projected-satisfaction policy owns an immutable authored-order copy");
        Expect(Catch(() => new KLEPProjectedSatisfactionPolicy(
                   new[]
                   {
                       UnitDesire(
                           "desire.duplicate",
                           new KLEPKeyPresent("key.first")),
                       new KLEPAgentDesire(
                           "desire.duplicate",
                           "different-version",
                           new KLEPKeyPresent("key.second"),
                           1f)
                   })) is ArgumentException,
            "Projected-satisfaction policy rejects duplicate ordinal desire IDs");

        var completeStatePolicy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                UnitDesire(
                    "desire.complete-state",
                    new KLEPKeyPresent("key.kept"))
            });
        KLEPProjectedSatisfactionEvaluation removed =
            completeStatePolicy.Evaluate(
                "action.complete-state",
                new CompleteKeyState("key.kept"),
                new CompleteKeyState());
        Expect(removed.ScoreContribution == -1f,
            "The projected argument is a complete state, so an absent current Key is projected lost rather than unchanged");
        Expect(Catch(() => completeStatePolicy.Evaluate(
                   "", new CompleteKeyState(), new CompleteKeyState()))
                   is ArgumentException &&
               Catch(() => completeStatePolicy.Evaluate(
                   "action.null-current", null, new CompleteKeyState()))
                   is ArgumentNullException &&
               Catch(() => completeStatePolicy.Evaluate(
                   "action.null-projected", new CompleteKeyState(), null))
                   is ArgumentNullException,
            "Projected-satisfaction evaluation requires a target and both complete states");

        var outOfScoreRange = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.range",
                    "1",
                    new KLEPKeyPresent("key.range"),
                    float.MaxValue,
                    float.MaxValue)
            });
        Expect(Catch(() => outOfScoreRange.Evaluate(
                   "action.range",
                   new CompleteKeyState(),
                   new CompleteKeyState("key.range"))) is InvalidOperationException,
            "Double intermediates reject a finite calculation that cannot enter the Single score model");
    }

    private static void VerifyProjectedSatisfactionAppendsOneAggregateScoreComponent()
    {
        const string targetId = "goal.score-components";
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.score",
                    "3",
                    new KLEPKeyPresent("key.score"),
                    3f)
            });
        KLEPProjectedSatisfactionEvaluation satisfaction = policy.Evaluate(
            targetId,
            new CompleteKeyState(),
            new CompleteKeyState("key.score"));
        KLEPKeySnapshot snapshot =
            (new KLEPNeuron("agent.projected-score.snapshot"))
                .TickViaAgent().KeySnapshot;
        KLEPExecutableScoreEvaluation authored =
            Definition(targetId, 2f).EvaluateScore(snapshot);
        KLEPExecutableScoreEvaluation intrinsic =
            authored.WithGoalIntrinsicAttraction(
                "goal.attraction.policy", "1", 1f, "Intrinsic now-state.");
        KLEPExecutableScoreEvaluation projected =
            intrinsic.WithProjectedSatisfaction(satisfaction);
        KLEPExecutableScoreEvaluation polished =
            projected.WithObserverInfluence("observer.direction", 2f);

        Expect(authored.Total == 2f &&
               intrinsic.Total == 3f &&
               projected.Total == 6f &&
               polished.Total == 8f,
            "Intrinsic attraction, projected satisfaction, and Observer polish each contribute exactly once");
        Expect(polished.Components.Count == authored.Components.Count + 3 &&
               polished.Components[polished.Components.Count - 3].Kind ==
                   KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction &&
               polished.Components[polished.Components.Count - 2].Kind ==
                   KLEPExecutableScoreComponentKind.ProjectedSatisfaction &&
               polished.Components[polished.Components.Count - 1].Kind ==
                   KLEPExecutableScoreComponentKind.ObserverInfluence,
            "Projected satisfaction is one aggregate component between intrinsic attraction and final Observer influence");
        KLEPExecutableScoreComponent component =
            polished.Components[polished.Components.Count - 2];
        Expect(component.Value == 3f &&
               component.SourceId == satisfaction.PolicyStableId &&
               component.SourceVersion == satisfaction.PolicyVersion &&
               ReferenceEquals(component.ProjectedSatisfaction, satisfaction) &&
               component.ProjectedSatisfaction.Desires.Count == 1,
            "The aggregate score component retains its complete per-desire evaluation");
        Expect(polished.Components[polished.Components.Count - 3]
                   .ProjectedSatisfaction == null &&
               polished.Components[polished.Components.Count - 1]
                   .ProjectedSatisfaction == null,
            "Only the projected-satisfaction component carries projected-satisfaction detail");
        Expect(Catch(() => authored.WithProjectedSatisfaction(
                   policy.Evaluate(
                       "different.target",
                       new CompleteKeyState(),
                       new CompleteKeyState("key.score")))) is ArgumentException,
            "A projected-satisfaction evaluation cannot be attached to another Executable");

        var maximumPolicy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.score-overflow",
                    "1",
                    new KLEPKeyPresent("key.score-overflow"),
                    float.MaxValue)
            });
        KLEPProjectedSatisfactionEvaluation maximum = maximumPolicy.Evaluate(
            "action.score-overflow",
            new CompleteKeyState(),
            new CompleteKeyState("key.score-overflow"));
        KLEPExecutableScoreEvaluation maximumAuthored =
            Definition("action.score-overflow", float.MaxValue)
                .EvaluateScore(snapshot);
        Expect(Catch(() => maximumAuthored.WithProjectedSatisfaction(maximum))
                   is InvalidOperationException,
            "Aggregate projected satisfaction rejects overflow against the existing score");
    }

    private static void VerifyProjectedSatisfactionReordersOnlyEligibleSoloRoots()
    {
        const string projectedId = "agent.satisfaction.projected";
        const string baselineId = "agent.satisfaction.baseline";
        const string lockedId = "agent.satisfaction.locked";
        KLEPKeyDefinition desired = Key("agent.satisfaction.desired");
        KLEPKeyDefinition permit = Key("agent.satisfaction.permit");
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.agent.satisfaction",
                    "1",
                    new KLEPKeyPresent(desired.Id.Value),
                    5f)
            });
        var projected = new ProbeExecutable(
            Definition(
                projectedId,
                1f,
                declaredOutputs: new[] { desired }),
            KLEPExecutableTickStatus.Running);
        var baseline = new ProbeExecutable(
            Definition(baselineId, 4f),
            KLEPExecutableTickStatus.Running);
        var locked = new ProbeExecutable(
            new KLEPExecutableDefinition(
                lockedId,
                lockedId,
                KLEPExecutableKind.Action,
                validationLocks: new[]
                {
                    new KLEPLock(
                        "agent.satisfaction.locked.permit",
                        "Permit is present",
                        new KLEPKeyPresent(permit.Id.Value))
                },
                baseAttractiveness: 100f,
                declaredOutputs: new[] { desired }),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.satisfaction.neuron");
        neuron.RegisterExecutable(baseline);
        neuron.RegisterExecutable(locked);
        neuron.RegisterExecutable(projected);
        var projector = new CompletingProjectionObserver(
            projectedId,
            desired.Id.Value);
        var agent = new KLEPAgent(
            neuron, null, null, null, policy, projector);

        KLEPAgentTickTrace trace = agent.Tick();
        CandidateEvaluation projectedCandidate = Candidate(trace, projectedId);
        CandidateEvaluation baselineCandidate = Candidate(trace, baselineId);
        CandidateEvaluation lockedCandidate = Candidate(trace, lockedId);
        KLEPExecutableScoreComponent projectedSatisfaction =
            projectedCandidate.ScoreEvaluation.Components[
                projectedCandidate.ScoreEvaluation.Components.Count - 1];

        Expect(trace.Decision.SelectedExecutableId == projectedId &&
               projectedCandidate.Score == 6f &&
               baselineCandidate.Score == 4f,
            "A complete Observer projection lets satisfaction reorder eligible Solo roots");
        Expect(projectedSatisfaction.Kind ==
                   KLEPExecutableScoreComponentKind.ProjectedSatisfaction &&
               projectedSatisfaction.Value == 5f &&
               projectedSatisfaction.ProjectedSatisfaction.Desires[0]
                   .CurrentTruth == 0 &&
               projectedSatisfaction.ProjectedSatisfaction.Desires[0]
                   .ProjectedTruth == 1,
            "Agent arbitration retains the exact satisfaction delta from a complete projected state");
        Expect(!lockedCandidate.IsEligible &&
               lockedCandidate.ScoreEvaluation == null &&
               locked.TickCount == 0,
            "Projected satisfaction is never evaluated to make a closed-Lock Solo eligible");
        Expect(projectedSatisfaction.ProjectedSatisfaction.ProjectorStableId ==
                   projector.StableId &&
               projectedSatisfaction.ProjectedSatisfaction.ProjectorVersion ==
                   projector.Version &&
               projectedSatisfaction.ProjectedSatisfaction.ProjectionProvenance ==
                   CompletingProjectionObserver.Provenance &&
               projectedSatisfaction.ProjectedSatisfaction.Projection
                   .TargetRootTenureId.Length > 0 &&
               projectedSatisfaction.ProjectedSatisfaction.Projection.Horizon ==
                   KLEPCandidateStateProjectionHorizon.SuccessfulRunCompletion,
            "A complete projected-satisfaction trace freezes projector, root-tenure, and horizon provenance");
    }

    private static void VerifyProjectedSatisfactionUsesPostTandemSnapshot()
    {
        const string producerId = "agent.satisfaction.post-tandem.producer";
        const string baselineId = "agent.satisfaction.post-tandem.baseline";
        KLEPKeyDefinition desired =
            Key("agent.satisfaction.post-tandem.desired");
        var sensor = new ProbeExecutable(
            Definition(
                "agent.satisfaction.post-tandem.sensor",
                0f,
                KLEPExecutionMode.Tandem,
                KLEPExecutableKind.Sensor,
                new[] { desired }),
            KLEPExecutableTickStatus.Succeeded,
            emitDefinition: desired,
            emitOnTick: 1);
        var producer = new ProbeExecutable(
            Definition(
                producerId,
                1f,
                declaredOutputs: new[] { desired }),
            KLEPExecutableTickStatus.Running);
        var baseline = new ProbeExecutable(
            Definition(baselineId, 2f),
            KLEPExecutableTickStatus.Running);
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.agent.post-tandem",
                    "1",
                    new KLEPKeyPresent(desired.Id.Value),
                    100f)
            });
        var neuron = new KLEPNeuron(
            "agent.satisfaction.post-tandem.neuron");
        neuron.RegisterExecutable(baseline);
        neuron.RegisterExecutable(producer);
        neuron.RegisterExecutable(sensor);
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            null,
            policy,
            new CompletingProjectionObserver(
                producerId,
                desired.Id.Value));

        KLEPAgentTickTrace trace = agent.Tick();
        CandidateEvaluation producerCandidate = Candidate(trace, producerId);
        KLEPProjectedSatisfactionEvaluation evaluation =
            producerCandidate.ScoreEvaluation.Components[
                producerCandidate.ScoreEvaluation.Components.Count - 1]
                .ProjectedSatisfaction;

        Expect(!trace.Decision.InitialKeySnapshot.Contains(desired.Id) &&
               trace.Decision.KeySnapshot.Contains(desired.Id) &&
               trace.Decision.KeySnapshot.WaveIndex >
                   trace.Decision.InitialKeySnapshot.WaveIndex,
            "The Tandem sensor publishes its Key before Solo arbitration in the same Agent Tick");
        Expect(evaluation.Desires[0].CurrentTruth == 1 &&
               evaluation.Desires[0].ProjectedTruth == 1 &&
               evaluation.ScoreContribution == 0f &&
               trace.Decision.SelectedExecutableId == baselineId,
            "Projected satisfaction compares guaranteed outputs against the settled post-Tandem Key snapshot");
    }

    private static void
        VerifyBaselineProjectionAbstainsForEarlierOneCycleEmission()
    {
        const string actionId = "agent.satisfaction.one-cycle.action";
        KLEPKeyDefinition transient = new KLEPKeyDefinition(
            new KLEPKeyId("agent.satisfaction.one-cycle.transient"),
            "Transient successful emission",
            defaultLifetime: KLEPKeyLifetime.OneCycle);
        var action = new EmitsOnceCompletesOnThirdTickExecutable(
            Definition(
                actionId,
                1f,
                declaredOutputs: new[] { transient }),
            transient);
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                UnitDesire(
                    "desire.agent.one-cycle",
                    new KLEPKeyPresent(transient.Id.Value))
            });
        var neuron = new KLEPNeuron("agent.satisfaction.one-cycle.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(neuron, null, null, null, policy);

        KLEPAgentTickTrace first = agent.Tick();
        KLEPAgentTickTrace second = agent.Tick();
        KLEPAgentTickTrace third = agent.Tick();
        KLEPProjectedSatisfactionEvaluation evaluation =
            Candidate(third, actionId).ScoreEvaluation.Components[
                Candidate(third, actionId).ScoreEvaluation.Components.Count - 1]
                .ProjectedSatisfaction;

        Expect(!first.Decision.KeySnapshot.Contains(transient.Id) &&
               second.Decision.KeySnapshot.Contains(transient.Id) &&
               !third.Decision.KeySnapshot.Contains(transient.Id) &&
               action.TickCount == 3,
            "A OneCycle declared output may be emitted earlier in a successful run and be absent at completion");
        Expect(evaluation.ProjectionAbstained &&
               evaluation.ScoreContribution == 0f &&
               evaluation.RawTotal == 0d &&
               !evaluation.Desires[0].IsProjectedTruthKnown &&
               evaluation.Desires[0].Contribution == 0d &&
               evaluation.ProjectorStableId ==
                   KLEPBaselineCandidateStateProjectionObserver.Instance.StableId &&
               evaluation.ProjectionAbstentionReason.Length > 0,
            "The baseline explicitly abstains with unknown projected truth and zero influence instead of inferring final presence from DeclaredOutputs");
    }

    private static void VerifyMismatchedProjectionFaultsAndIsTraced()
    {
        const string actionId = "agent.satisfaction.stale.action";
        KLEPKeyDefinition desired = Key("agent.satisfaction.stale.desired");
        var action = new ProbeExecutable(
            Definition(actionId, 1f, declaredOutputs: new[] { desired }),
            KLEPExecutableTickStatus.Running);
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                UnitDesire(
                    "desire.agent.stale",
                    new KLEPKeyPresent(desired.Id.Value))
            });
        var neuron = new KLEPNeuron("agent.satisfaction.stale.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            null,
            policy,
            new MismatchedProjectionObserver());

        Exception fault = Catch(() => agent.Tick());

        Expect(fault is InvalidOperationException &&
               fault.Message.Contains("stale or bound") &&
               agent.LastTrace.Decision.Fault != null &&
               agent.LastTrace.Decision.Fault.ExecutableStableId == actionId &&
               agent.LastTrace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage.ProjectedSatisfactionEvaluation,
            "A stale or mismatched candidate-state projection faults at the projected-satisfaction boundary");
    }

    private static void VerifyAgentScoreLayersEndWithOptionalObserverPolish()
    {
        const string goalId = "agent.satisfaction.layered.goal";
        KLEPKeyDefinition desired = Key("agent.satisfaction.layered.desired");
        var child = new ProbeExecutable(
            Definition("agent.satisfaction.layered.child", 0f),
            KLEPExecutableTickStatus.Running);
        var goal = new KLEPGoal(
            Definition(
                goalId,
                1f,
                kind: KLEPExecutableKind.Goal,
                declaredOutputs: new[] { desired }),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            },
            null,
            new ConstantGoalAttractionEvaluator(
                "agent.satisfaction.layered.intrinsic",
                "1",
                2f));
        var policy = new KLEPProjectedSatisfactionPolicy(
            new[]
            {
                new KLEPAgentDesire(
                    "desire.agent.layered",
                    "1",
                    new KLEPKeyPresent(desired.Id.Value),
                    3f)
            });
        var guidance = new PolishingGuidanceObserver(goalId, 4f);
        var neuron = new KLEPNeuron("agent.satisfaction.layered.neuron");
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(
            neuron,
            null,
            guidance,
            KLEPBaselineStructuralObserver.Instance,
            policy,
            new CompletingProjectionObserver(goalId, desired.Id.Value));

        KLEPAgentTickTrace unpolished = agent.Tick();
        CandidateEvaluation firstCandidate = Candidate(unpolished, goalId);
        KLEPAgentTickTrace polished = agent.Tick();
        CandidateEvaluation secondCandidate = Candidate(polished, goalId);
        IReadOnlyList<KLEPExecutableScoreComponent> firstComponents =
            firstCandidate.ScoreEvaluation.Components;
        IReadOnlyList<KLEPExecutableScoreComponent> secondComponents =
            secondCandidate.ScoreEvaluation.Components;

        Expect(firstCandidate.Score == 6f &&
               firstComponents.Count == 3 &&
               firstComponents[0].Kind ==
                   KLEPExecutableScoreComponentKind.BaseAttractiveness &&
               firstComponents[1].Kind ==
                   KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction &&
               firstComponents[2].Kind ==
                   KLEPExecutableScoreComponentKind.ProjectedSatisfaction,
            "Without advice, Agent score order is authored then intrinsic then projected satisfaction");
        Expect(secondCandidate.Score == 10f &&
               secondComponents.Count == 4 &&
               secondComponents[1].Kind ==
                   KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction &&
               secondComponents[2].Kind ==
                   KLEPExecutableScoreComponentKind.ProjectedSatisfaction &&
               secondComponents[3].Kind ==
                   KLEPExecutableScoreComponentKind.ObserverInfluence,
            "Optional Observer polish is appended only after intrinsic and projected satisfaction components");
        Expect(guidance.ObserveCount == 1 &&
               polished.Decision.GuidanceAdvice != null &&
               polished.Decision.GuidanceAdvice.WasApplied &&
               polished.Decision.GuidanceAdvice.PreObserverScore == 6f &&
               polished.Decision.GuidanceAdvice.EffectiveScore == 10f,
            "One-use Observer advice reports the complete pre-polish and effective Agent scores");
    }

    private static void VerifyLearnedDesirePositiveEvidenceReordersEligibleSolos()
    {
        const string promotedId = "agent.learned.positive.a";
        const string baselineId = "agent.learned.positive.b";
        var promoted = new ProbeExecutable(
            Definition(promotedId, 2f),
            KLEPExecutableTickStatus.Succeeded);
        var baseline = new ProbeExecutable(
            Definition(baselineId, 4f),
            KLEPExecutableTickStatus.Succeeded);
        var neuron = new KLEPNeuron("agent.learned.positive.neuron");
        neuron.RegisterExecutable(baseline);
        neuron.RegisterExecutable(promoted);
        var policy = new RecordingLearnedDesirePolicy(
            (request, candidate) =>
                StringComparer.Ordinal.Equals(
                    candidate.ExecutableStableId, promotedId)
                    ? 3f
                    : 0f);
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            KLEPBaselineStructuralObserver.Instance,
            null,
            null,
            policy);

        KLEPAgentTickTrace trace = agent.Tick();
        CandidateEvaluation promotedCandidate = Candidate(trace, promotedId);
        CandidateEvaluation baselineCandidate = Candidate(trace, baselineId);
        KLEPExecutableScoreComponent learned =
            promotedCandidate.ScoreEvaluation.Components[
                promotedCandidate.ScoreEvaluation.Components.Count - 1];

        Expect(trace.Decision.SelectedExecutableId == promotedId &&
               promotedCandidate.Score == 5f &&
               baselineCandidate.Score == 4f,
            "Positive learned Desire evidence can reorder already-eligible Solo roots");
        Expect(learned.Kind ==
                   KLEPExecutableScoreComponentKind.LearnedDesireExpectation &&
               learned.LearnedDesireExpectation != null &&
               learned.LearnedDesireExpectation.ScoreContribution == 3f &&
               learned.LearnedDesireExpectation.Desires.Count == 1 &&
               learned.LearnedDesireExpectation.IsAgentEvidenceBound &&
               learned.LearnedDesireExpectation.AgentCycleIndex ==
                   trace.Decision.CycleIndex &&
               learned.LearnedDesireExpectation.CatalogFingerprint.Equals(
                   agent.ExecutableMap.Fingerprint),
            "The learned score component preserves its typed candidate and per-Desire evidence");
        Expect(policy.EvaluateCount == 1 &&
               policy.LastRequest.Candidates.Count == 2 &&
               ReferenceEquals(
                   policy.LastRequest.AcceptedStructuralMap,
                   agent.ExecutableMap) &&
               policy.LastRequest.Candidates[0].ExecutableStableId == promotedId &&
               policy.LastRequest.Candidates[0].PrePolicyScore == 2f &&
               policy.LastRequest.Candidates[0].RootTenureId.Length > 0,
            "One batch receives the accepted map and stable ordered eligible root evidence");
    }

    private static void VerifyLearnedDesireNegativeUnknownAndTieSemantics()
    {
        const string discouragedId = "agent.learned.negative.action";
        var discouragedNeuron = new KLEPNeuron(
            "agent.learned.negative.neuron");
        discouragedNeuron.RegisterExecutable(new ProbeExecutable(
            Definition(discouragedId, 1f),
            KLEPExecutableTickStatus.Succeeded));
        var negativePolicy = new RecordingLearnedDesirePolicy(
            (request, candidate) => -2f);
        var discouragedAgent = new KLEPAgent(
            discouragedNeuron,
            new KLEPAgentConfiguration(actionCertaintyThreshold: 0f),
            null,
            KLEPBaselineStructuralObserver.Instance,
            null,
            null,
            negativePolicy);

        KLEPAgentTickTrace discouraged = discouragedAgent.Tick();
        Expect(discouraged.Decision.SelectedExecutableId == null &&
               discouraged.Decision.IsPatient &&
               Candidate(discouraged, discouragedId).Score == -1f,
            "Negative learned Desire evidence can move a candidate below the unchanged certainty threshold");

        const string unknownId = "agent.learned.unknown.action";
        var unknownNeuron = new KLEPNeuron("agent.learned.unknown.neuron");
        unknownNeuron.RegisterExecutable(new ProbeExecutable(
            Definition(unknownId, 3f),
            KLEPExecutableTickStatus.Succeeded));
        var unknownPolicy = new RecordingLearnedDesirePolicy(
            (request, candidate) => 0f,
            zeroDisposition:
                KLEPLearnedDesireContributionDisposition.UnknownEvidence);
        var unknownAgent = new KLEPAgent(
            unknownNeuron,
            null,
            null,
            KLEPBaselineStructuralObserver.Instance,
            null,
            null,
            unknownPolicy);
        KLEPAgentTickTrace unknown = unknownAgent.Tick();
        KLEPLearnedDesireCandidateEvaluation unknownEvaluation =
            Candidate(unknown, unknownId).ScoreEvaluation.Components[1]
                .LearnedDesireExpectation;
        Expect(Candidate(unknown, unknownId).Score == 3f &&
               unknownEvaluation.ScoreContribution == 0f &&
               unknownEvaluation.Desires[0].Disposition ==
                   KLEPLearnedDesireContributionDisposition.UnknownEvidence,
            "Unknown evidence is explicitly traced and preserves authored attraction exactly");

        const string currentId = "agent.learned.tie.z-current";
        const string challengerId = "agent.learned.tie.a-challenger";
        var current = new ProbeExecutable(
            Definition(currentId, 3f), KLEPExecutableTickStatus.Running);
        var challenger = new ProbeExecutable(
            Definition(challengerId, 2f), KLEPExecutableTickStatus.Running);
        var tieNeuron = new KLEPNeuron("agent.learned.tie.neuron");
        tieNeuron.RegisterExecutable(current);
        tieNeuron.RegisterExecutable(challenger);
        var tiePolicy = new RecordingLearnedDesirePolicy(
            (request, candidate) =>
                request.CurrentSnapshot.Tick > 1 &&
                StringComparer.Ordinal.Equals(
                    candidate.ExecutableStableId, challengerId)
                    ? 1f
                    : 0f);
        var tieAgent = new KLEPAgent(
            tieNeuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, tiePolicy);
        tieAgent.Tick();
        KLEPAgentTickTrace tied = tieAgent.Tick();
        Expect(tied.Decision.SelectedExecutableId == currentId &&
               tieAgent.CurrentSoloExecutableId == currentId &&
               tiePolicy.LastRequest.Candidates[1].IsCurrentRunning,
            "A learned-score tie retains the Running Solo under the existing strict interruption rule");
    }

    private static void VerifyLearnedDesireReceivesPostTandemEligibleRootsOnly()
    {
        const string soloId = "agent.learned.boundary.solo";
        const string lockedId = "agent.learned.boundary.locked";
        const string tandemId = "agent.learned.boundary.tandem";
        KLEPKeyDefinition sensed = Key("agent.learned.boundary.sensed");
        var tandem = new ProbeExecutable(
            Definition(
                tandemId,
                100f,
                KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { sensed }),
            KLEPExecutableTickStatus.Succeeded,
            sensed,
            emitOnTick: 1);
        var solo = new ProbeExecutable(
            Definition(soloId, 2f), KLEPExecutableTickStatus.Succeeded);
        var locked = new ProbeExecutable(
            new KLEPExecutableDefinition(
                lockedId,
                lockedId,
                KLEPExecutableKind.Action,
                validationLocks: new[]
                {
                    new KLEPLock(
                        "agent.learned.boundary.closed-lock",
                        "closed",
                        new KLEPKeyPresent("agent.learned.boundary.missing"))
                },
                baseAttractiveness: 500f),
            KLEPExecutableTickStatus.Succeeded);
        var neuron = new KLEPNeuron("agent.learned.boundary.neuron");
        neuron.RegisterExecutable(locked);
        neuron.RegisterExecutable(tandem);
        neuron.RegisterExecutable(solo);
        var policy = new RecordingLearnedDesirePolicy(
            (request, candidate) => 0f);
        var agent = new KLEPAgent(
            neuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, policy);

        agent.Tick();
        Expect(policy.EvaluateCount == 1 &&
               policy.LastRequest.CurrentSnapshot.Contains(sensed.Id) &&
               policy.LastRequest.CurrentSnapshot.WaveIndex > 0 &&
               policy.LastRequest.Candidates.Count == 1 &&
               policy.LastRequest.Candidates[0].ExecutableStableId == soloId,
            "The policy sees the settled post-Tandem snapshot but never receives Tandem or ineligible roots");

        var noEligibleNeuron = new KLEPNeuron(
            "agent.learned.no-eligible.neuron");
        noEligibleNeuron.RegisterExecutable(new ProbeExecutable(
            new KLEPExecutableDefinition(
                "agent.learned.no-eligible.locked",
                "locked",
                KLEPExecutableKind.Action,
                validationLocks: new[]
                {
                    new KLEPLock(
                        "agent.learned.no-eligible.lock",
                        "closed",
                        new KLEPKeyPresent("agent.learned.no-eligible.missing"))
                },
                baseAttractiveness: 1f),
            KLEPExecutableTickStatus.Succeeded));
        noEligibleNeuron.RegisterExecutable(new ProbeExecutable(
            Definition(
                "agent.learned.no-eligible.tandem",
                1f,
                KLEPExecutionMode.Tandem),
            KLEPExecutableTickStatus.Succeeded));
        var untouchedPolicy = new RecordingLearnedDesirePolicy(
            (request, candidate) => 0f);
        var noEligibleAgent = new KLEPAgent(
            noEligibleNeuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, untouchedPolicy);
        noEligibleAgent.Tick();
        Expect(untouchedPolicy.EvaluateCount == 0,
            "The optional policy is not invoked when no already-eligible root Solo exists");
    }

    private static void VerifyLearnedDesirePrecedesObserverPolish()
    {
        const string actionId = "agent.learned.order.action";
        var action = new ProbeExecutable(
            Definition(actionId, 2f), KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.learned.order.neuron");
        neuron.RegisterExecutable(action);
        var guidance = new PolishingGuidanceObserver(actionId, 4f);
        var policy = new RecordingLearnedDesirePolicy(
            (request, candidate) => 3f);
        var agent = new KLEPAgent(
            neuron,
            null,
            guidance,
            KLEPBaselineStructuralObserver.Instance,
            null,
            null,
            policy);

        agent.Tick();
        KLEPAgentTickTrace polished = agent.Tick();
        CandidateEvaluation candidate = Candidate(polished, actionId);
        IReadOnlyList<KLEPExecutableScoreComponent> components =
            candidate.ScoreEvaluation.Components;
        Expect(candidate.Score == 9f &&
               components.Count == 3 &&
               components[0].Kind ==
                   KLEPExecutableScoreComponentKind.BaseAttractiveness &&
               components[1].Kind ==
                   KLEPExecutableScoreComponentKind.LearnedDesireExpectation &&
               components[2].Kind ==
                   KLEPExecutableScoreComponentKind.ObserverInfluence &&
               polished.Decision.GuidanceAdvice.PreObserverScore == 5f,
            "Learned Desire expectation is appended before the final one-use Observer polish");
    }

    private static void VerifyMalformedLearnedDesireBatchFaultsAtomically()
    {
        const string firstId = "agent.learned.malformed.a";
        const string secondId = "agent.learned.malformed.b";
        var neuron = new KLEPNeuron("agent.learned.malformed.neuron");
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(firstId, 2f), KLEPExecutableTickStatus.Succeeded));
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(secondId, 3f), KLEPExecutableTickStatus.Succeeded));
        var malformed = new RecordingLearnedDesirePolicy(
            (request, candidate) => 1f,
            omitLastResult: true);
        var agent = new KLEPAgent(
            neuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, malformed);

        Exception fault = Catch(() => agent.Tick());
        Expect(fault is InvalidOperationException &&
               agent.LastTrace.Decision.Fault != null &&
               agent.LastTrace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage
                       .LearnedDesireExpectationEvaluation,
            "An incomplete learned Desire batch faults at its dedicated lifecycle boundary");
        Expect(Candidate(agent.LastTrace, firstId).Score == 2f &&
               Candidate(agent.LastTrace, secondId).Score == 3f &&
               Candidate(agent.LastTrace, firstId)
                   .ScoreEvaluation.Components.Count == 1 &&
               Candidate(agent.LastTrace, secondId)
                   .ScoreEvaluation.Components.Count == 1,
            "Malformed batch validation is atomic and leaves every candidate at its pre-policy score");
    }

    private static void VerifyLearnedDesireOverflowFaultsAtomically()
    {
        const string safeId = "agent.learned.overflow.a-safe";
        const string overflowId = "agent.learned.overflow.z-overflow";
        var neuron = new KLEPNeuron("agent.learned.overflow.neuron");
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(safeId, 1f), KLEPExecutableTickStatus.Succeeded));
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(overflowId, float.MaxValue),
            KLEPExecutableTickStatus.Succeeded));
        var overflow = new RecordingLearnedDesirePolicy(
            (request, candidate) =>
                StringComparer.Ordinal.Equals(
                    candidate.ExecutableStableId, overflowId)
                    ? float.MaxValue
                    : 1f);
        var agent = new KLEPAgent(
            neuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, overflow);

        Exception fault = Catch(() => agent.Tick());
        Expect(fault is InvalidOperationException &&
               agent.LastTrace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage
                       .LearnedDesireExpectationEvaluation,
            "A finite term whose aggregate score overflows faults at the learned Desire boundary");
        Expect(Candidate(agent.LastTrace, safeId).Score == 1f &&
               Candidate(agent.LastTrace, overflowId).Score == float.MaxValue &&
               Candidate(agent.LastTrace, safeId)
                   .ScoreEvaluation.Components.Count == 1,
            "A later overflow cannot publish an earlier candidate's otherwise-valid learned score");
    }

    private static void VerifyLearnedDesireBindingMutationFaults()
    {
        const string actionId = "agent.learned.binding-mutation.action";
        var neuron = new KLEPNeuron(
            "agent.learned.binding-mutation.neuron");
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(actionId, 2f), KLEPExecutableTickStatus.Succeeded));
        var mutating = new RecordingLearnedDesirePolicy(
            (request, candidate) => 1f,
            mutateBindingsDuringEvaluation: true);
        var agent = new KLEPAgent(
            neuron, null, null,
            KLEPBaselineStructuralObserver.Instance,
            null, null, mutating);

        Exception fault = Catch(() => agent.Tick());
        Expect(fault is InvalidOperationException &&
               agent.LastTrace.Decision.Fault.Stage ==
                   KLEPExecutableLifecycleStage
                       .LearnedDesireExpectationEvaluation &&
               Candidate(agent.LastTrace, actionId).Score == 2f,
            "A policy cannot change its binding identity during a batch or publish partial influence");
    }

    private static void VerifyAgentStructuralMapLifecycleAndRejectedProposal()
    {
        const string primaryId = "agent.map.lifecycle.primary";
        const string addedId = "agent.map.lifecycle.added";
        const string rejectedGoalId = "agent.map.lifecycle.rejected-goal";
        var primary = new ProbeExecutable(
            Definition(primaryId, 5f),
            KLEPExecutableTickStatus.Succeeded);
        var neuron = new KLEPNeuron("agent.map.lifecycle.neuron");
        neuron.RegisterExecutable(primary);
        var structuralObserver = new CountingStructuralObserver();
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            structuralObserver,
            null);

        KLEPAgentTickTrace initial = agent.Tick();
        KLEPExecutableStructuralMap initialMap = agent.ExecutableMap;
        KLEPStructuralMapDecisionTrace initialStructural =
            initial.Decision.StructuralMap;
        Expect(initialMap != null &&
               initialMap.IsValid &&
               ReferenceEquals(initialMap, agent.LastExecutableMapAttempt) &&
               initialMap.Snapshot.ProposedCatalogRevision == "1" &&
               neuron.CatalogRevision == 1 &&
               structuralObserver.ObserveCount == 1,
            "The first Agent Tick accepts an inspectable initial structural map and catalog revision");
        Expect(initialStructural.Trigger ==
                   KLEPStructuralMapTrigger.InitialCatalog &&
               initialStructural.Disposition ==
                   KLEPStructuralMapDisposition.Accepted &&
               initialStructural.ObserverStableId ==
                   structuralObserver.StableId &&
               initialStructural.ObserverVersion ==
                   structuralObserver.Version &&
               initialStructural.DidObserve &&
               !initialStructural.DidReuse &&
               !initialStructural.RejectedCatalogProposal &&
               initialStructural.Fault == null,
            "The first decision freezes its structural Observer identity, trigger, and accepted disposition");
        Expect(ReferenceEquals(
                   initialStructural.RequestedCatalog,
                   initialMap.Snapshot) &&
               ReferenceEquals(
                   initialStructural.AttemptedAssessment,
                   initialMap) &&
               ReferenceEquals(
                   initialStructural.ActiveAssessment,
                   initialMap) &&
               initialStructural.ProposedRevision == "1" &&
               initialStructural.ActiveRevision == "1" &&
               initialStructural.ProposedFingerprint.Equals(
                   initialMap.Fingerprint) &&
               initialStructural.ActiveFingerprint.Equals(
                   initialMap.Fingerprint),
            "The initial structural trace freezes the exact requested, attempted, and active revision and fingerprint");

        KLEPAgentTickTrace unchanged = agent.Tick();
        KLEPStructuralMapDecisionTrace unchangedStructural =
            unchanged.Decision.StructuralMap;
        Expect(unchanged.Confidence > 0f &&
               !unchanged.NeedsGuidance &&
               structuralObserver.ObserveCount == 1 &&
               ReferenceEquals(initialMap, agent.ExecutableMap),
            "An unchanged confident catalog reuses its accepted map without another structural observation");
        Expect(unchangedStructural.Trigger ==
                   KLEPStructuralMapTrigger.UnchangedReuse &&
               unchangedStructural.Disposition ==
                   KLEPStructuralMapDisposition.Reused &&
               unchangedStructural.ObserverStableId ==
                   structuralObserver.StableId &&
               unchangedStructural.ObserverVersion ==
                   structuralObserver.Version &&
               !unchangedStructural.DidObserve &&
               unchangedStructural.DidReuse &&
               unchangedStructural.AttemptedAssessment == null &&
               ReferenceEquals(
                   unchangedStructural.ActiveAssessment,
                   initialMap) &&
               unchangedStructural.ProposedRevision == "1" &&
               unchangedStructural.ProposedFingerprint.Equals(
                   initialMap.Fingerprint),
            "Unchanged map reuse is explicit and retains the exact accepted map identity without a false observation");

        var added = new ProbeExecutable(
            Definition(addedId, 1f),
            KLEPExecutableTickStatus.Succeeded);
        neuron.RegisterExecutable(added);
        KLEPAgentTickTrace mutated = agent.Tick();
        KLEPExecutableStructuralMap mutatedMap = agent.ExecutableMap;
        KLEPStructuralMapDecisionTrace mutatedStructural =
            mutated.Decision.StructuralMap;
        Expect(mutated.Confidence > 0f &&
               !mutated.NeedsGuidance &&
               structuralObserver.ObserveCount == 2 &&
               neuron.CatalogRevision == 2 &&
               mutatedMap.Snapshot.ProposedCatalogRevision == "2" &&
               mutatedMap.Snapshot.Roots.Count == 2 &&
               !ReferenceEquals(initialMap, mutatedMap),
            "Catalog mutation forces structural remapping even after Agent confidence suppresses guidance");
        Expect(mutatedStructural.Trigger ==
                   KLEPStructuralMapTrigger.RevisionChanged &&
               mutatedStructural.Disposition ==
                   KLEPStructuralMapDisposition.Accepted &&
               mutatedStructural.ObserverStableId ==
                   structuralObserver.StableId &&
               mutatedStructural.ObserverVersion ==
                   structuralObserver.Version &&
               ReferenceEquals(
                   mutatedStructural.AttemptedAssessment,
                   mutatedMap) &&
               ReferenceEquals(
                   mutatedStructural.ActiveAssessment,
                   mutatedMap) &&
               mutatedStructural.ProposedRevision == "2" &&
               mutatedStructural.ActiveRevision == "2" &&
               mutatedStructural.ProposedFingerprint.Equals(
                   mutatedMap.Fingerprint) &&
               mutatedStructural.ActiveFingerprint.Equals(
                   mutatedMap.Fingerprint),
            "A changed catalog freezes its accepted revision and graph fingerprint in that decision");

        agent.RequestExecutableRemap();
        KLEPAgentTickTrace remapped = agent.Tick();
        KLEPExecutableStructuralMap explicitMap = agent.ExecutableMap;
        KLEPStructuralMapDecisionTrace explicitStructural =
            remapped.Decision.StructuralMap;
        Expect(structuralObserver.ObserveCount == 3 &&
               neuron.CatalogRevision == 2 &&
               explicitMap.Snapshot.ProposedCatalogRevision == "2" &&
               explicitMap.Fingerprint.Equals(mutatedMap.Fingerprint) &&
               !ReferenceEquals(explicitMap, mutatedMap),
            "An explicit remap reassesses unchanged structure without inventing a catalog revision");
        Expect(explicitStructural.Trigger ==
                   KLEPStructuralMapTrigger.ExplicitRemap &&
               explicitStructural.Disposition ==
                   KLEPStructuralMapDisposition.Accepted &&
               explicitStructural.ObserverStableId ==
                   structuralObserver.StableId &&
               explicitStructural.ObserverVersion ==
                   structuralObserver.Version &&
               ReferenceEquals(
                   explicitStructural.AttemptedAssessment,
                   explicitMap) &&
               ReferenceEquals(
                   explicitStructural.ActiveAssessment,
                   explicitMap) &&
               explicitStructural.ProposedRevision == "2" &&
               explicitStructural.ActiveRevision == "2" &&
               explicitStructural.ProposedFingerprint.Equals(
                   explicitMap.Fingerprint) &&
               explicitStructural.ActiveFingerprint.Equals(
                   explicitMap.Fingerprint),
            "An explicit remap is distinguished from revision change while freezing its exact reassessment");

        var duplicateDescendant = new ProbeExecutable(
            Definition(addedId, 100f),
            KLEPExecutableTickStatus.Succeeded);
        var rejectedGoal = new KLEPGoal(
            Definition(
                rejectedGoalId,
                100f,
                kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[] { duplicateDescendant })
            });
        neuron.RegisterExecutable(rejectedGoal);
        int primaryTicksBeforeRejection = primary.TickCount;

        KLEPAgentTickTrace rejected = agent.Tick();
        KLEPStructuralMapDecisionTrace rejectedStructural =
            rejected.Decision.StructuralMap;
        Expect(structuralObserver.ObserveCount == 4 &&
               !agent.LastExecutableMapAttempt.IsValid &&
               HasDiagnostic(
                   agent.LastExecutableMapAttempt,
                   KLEPStructuralMapDiagnosticCode.DuplicateExecutableStableId) &&
               agent.LastExecutableMapAttempt.Snapshot
                   .ProposedCatalogRevision == "3",
            "A duplicate descendant is returned as an inspectable invalid structural-map attempt");
        Expect(rejectedStructural.Trigger ==
                   KLEPStructuralMapTrigger.RevisionChanged &&
               rejectedStructural.Disposition ==
                   KLEPStructuralMapDisposition.Rejected &&
               rejectedStructural.ObserverStableId ==
                   structuralObserver.StableId &&
               rejectedStructural.ObserverVersion ==
                   structuralObserver.Version &&
               rejectedStructural.DidObserve &&
               !rejectedStructural.DidReuse &&
               rejectedStructural.RejectedCatalogProposal &&
               ReferenceEquals(
                   rejectedStructural.AttemptedAssessment,
                   agent.LastExecutableMapAttempt) &&
               !rejectedStructural.AttemptedAssessment.IsValid &&
               ReferenceEquals(
                   rejectedStructural.ActiveAssessment,
                   explicitMap) &&
               rejectedStructural.ProposedRevision == "3" &&
               rejectedStructural.ActiveRevision == "2" &&
               rejectedStructural.ProposedFingerprint.Equals(
                   agent.LastExecutableMapAttempt.Fingerprint) &&
               rejectedStructural.ActiveFingerprint.Equals(
                   explicitMap.Fingerprint),
            "A rejected revision freezes both the invalid attempted map and the valid retained active map");
        Expect(ReferenceEquals(agent.ExecutableMap, explicitMap) &&
               neuron.CatalogRevision == 2 &&
               neuron.GetRootExecutableDefinitionsSnapshot().Count == 2 &&
               !HasRootDefinition(neuron, rejectedGoalId),
            "An invalid catalog proposal is rejected without replacing the prior accepted map or revision");
        Expect(rejected.Decision.SelectedExecutableId == primaryId &&
               primary.TickCount == primaryTicksBeforeRejection + 1,
            "The previously accepted catalog continues running in the same Tick that rejects an invalid proposal");
        Expect(ReferenceEquals(
                   initialStructural.ActiveAssessment,
                   initialMap) &&
               initialStructural.ActiveRevision == "1" &&
               initialStructural.ActiveFingerprint.Equals(
                   initialMap.Fingerprint) &&
               initialStructural.ActiveAssessment.Snapshot.Roots.Count == 1 &&
               ReferenceEquals(
                   mutatedStructural.ActiveAssessment,
                   mutatedMap) &&
               mutatedStructural.ActiveRevision == "2" &&
               ReferenceEquals(
                   explicitStructural.ActiveAssessment,
                   explicitMap),
            "Later remaps and rejection cannot rewrite an earlier decision's frozen structural evidence");
    }

    private static void VerifyStructuralObserverFaultsAreFrozenAndRethrown()
    {
        var sentinel = new ApplicationException(
            "structural Observer sentinel fault");
        VerifyStructuralObserverFault(
            new FaultingStructuralObserver(
                "agent.test.structural.throwing",
                snapshot => throw sentinel),
            sentinel,
            typeof(ApplicationException).FullName,
            sentinel.Message,
            "A throwing Structural Observer");

        const string nullMessage =
            "A Structural Observer returned no catalog assessment.";
        VerifyStructuralObserverFault(
            new FaultingStructuralObserver(
                "agent.test.structural.null",
                snapshot => null),
            null,
            typeof(InvalidOperationException).FullName,
            nullMessage,
            "A null Structural Observer result");

        const string mismatchMessage =
            "A Structural Observer returned an assessment for a different " +
            "catalog revision or graph fingerprint.";
        VerifyStructuralObserverFault(
            new FaultingStructuralObserver(
                "agent.test.structural.mismatched",
                snapshot => KLEPExecutableStructuralMapper.Build(
                    "different-revision",
                    Array.Empty<KLEPExecutableCatalogRoot>())),
            null,
            typeof(InvalidOperationException).FullName,
            mismatchMessage,
            "A mismatched Structural Observer result");
    }

    private static void VerifyStructuralObserverFault(
        IKLEPExecutableStructuralObserver structuralObserver,
        Exception expectedReference,
        string expectedExceptionType,
        string expectedMessage,
        string caseLabel)
    {
        var neuron = new KLEPNeuron(
            structuralObserver.StableId + ".neuron");
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(structuralObserver.StableId + ".executable", 1f),
            KLEPExecutableTickStatus.Succeeded));
        var agent = new KLEPAgent(
            neuron,
            null,
            null,
            structuralObserver,
            null);

        Exception observed = Catch(() => agent.Tick());
        KLEPStructuralMapDecisionTrace structural =
            agent.LastTrace.Decision.StructuralMap;
        Expect(expectedReference == null
                ? observed.GetType().FullName == expectedExceptionType &&
                  observed.Message == expectedMessage
                : ReferenceEquals(observed, expectedReference),
            caseLabel + " preserves the expected generic Agent.Tick throw");
        Expect(structural.Trigger ==
                   KLEPStructuralMapTrigger.InitialCatalog &&
               structural.Disposition ==
                   KLEPStructuralMapDisposition.Faulted &&
               structural.ObserverStableId == structuralObserver.StableId &&
               structural.ObserverVersion == structuralObserver.Version &&
               structural.ActiveAssessment == null &&
               !structural.RejectedCatalogProposal &&
               structural.Fault != null &&
               structural.Fault.ExceptionType == expectedExceptionType &&
               structural.Fault.Message == expectedMessage,
            caseLabel + " freezes a dedicated structural-map fault type and message");
        Expect(agent.LastTrace.Decision.Fault != null &&
               agent.LastTrace.Decision.Fault.ExceptionType ==
                   expectedExceptionType &&
               agent.LastTrace.Decision.Fault.Message == expectedMessage,
            caseLabel + " remains visible in the generic decision fault channel");
    }

    private static void VerifyConfigurationDefaults()
    {
        KLEPAgentConfiguration configuration = KLEPAgentConfiguration.Default;

        Expect(configuration.ActionCertaintyThreshold == 0f &&
               configuration.GuidanceConfidenceThreshold == 0f,
            "Agent defaults preserve authored action threshold zero and guidance threshold zero");
        Expect(configuration.LearningRate == 0.2f &&
               configuration.DiscountFactor == 0.9f,
            "Agent defaults use the approved alpha and gamma");
        Expect(configuration.SuccessReward == 1f &&
               configuration.FailureReward == -1f &&
               configuration.InterruptionReward == -0.25f,
            "Agent defaults use distinct approved terminal rewards");
        Expect(configuration.FamiliarityScale == 4f,
            "Agent defaults use the approved familiarity scale");

        Exception invalidBound = Catch(() =>
            new KLEPAgentConfiguration(
                discountFactor: 0.9f,
                successReward: float.MaxValue));
        Expect(invalidBound is ArgumentOutOfRangeException,
            "Agent rejects a configuration whose positive Q bound cannot remain finite");
        Exception invalidNegativeBound = Catch(() =>
            new KLEPAgentConfiguration(
                discountFactor: 0.9f,
                failureReward: -float.MaxValue));
        Expect(invalidNegativeBound is ArgumentOutOfRangeException,
            "Agent rejects a configuration whose negative Q bound cannot remain finite");
    }

    private static void VerifyStructuralMapCapturesRecursiveCatalog()
    {
        KLEPKeyDefinition nearby = Key("map.nearby-human");
        KLEPKeyDefinition inRange = Key("map.human-in-range");
        KLEPKeyDefinition pacified = Key("map.pacified");
        KLEPKeyDefinition ate = Key("map.ate-human");

        var sensor = new ProbeExecutable(
            Definition(
                "map.sensor.human",
                0f,
                KLEPExecutionMode.Tandem,
                KLEPExecutableKind.Sensor,
                new[] { nearby }),
            KLEPExecutableTickStatus.Succeeded);
        var move = new ProbeExecutable(
            new KLEPExecutableDefinition(
                "map.action.move",
                "Move to human",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "map.lock.move.nearby",
                        "A human is nearby",
                        new KLEPKeyPresent(nearby.Id.Value))
                },
                baseAttractiveness: 1f,
                declaredOutputs: new[] { inRange }),
            KLEPExecutableTickStatus.Succeeded);
        var eat = new ProbeExecutable(
            new KLEPExecutableDefinition(
                "map.action.eat",
                "Eat human",
                KLEPExecutableKind.Action,
                validationLocks: new[]
                {
                    new KLEPLock(
                        "map.lock.eat.ready",
                        "In range and not pacified",
                        new KLEPAll(
                            new KLEPKeyPresent(inRange.Id.Value),
                            new KLEPNot(
                                new KLEPKeyPresent(pacified.Id.Value))))
                },
                baseAttractiveness: 2f,
                declaredOutputs: new[] { ate }),
            KLEPExecutableTickStatus.Succeeded);
        var sequence = new KLEPGoal(
            Definition(
                "map.goal.sequence",
                5f,
                KLEPExecutionMode.Solo,
                KLEPExecutableKind.Goal,
                new[] { ate }),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { move }),
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { eat })
            });
        var eatGoal = new KLEPGoal(
            Definition(
                "map.goal.eat-human",
                10f,
                KLEPExecutionMode.Solo,
                KLEPExecutableKind.Goal,
                new[] { ate }),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[] { sequence })
            });

        KLEPExecutableStructuralMap map = KLEPExecutableStructuralMapper.Build(
            "catalog.preview.1",
            new[]
            {
                new KLEPExecutableCatalogRoot(
                    eatGoal,
                    "tenure.goal.eat-human.1"),
                new KLEPExecutableCatalogRoot(
                    sensor,
                    "tenure.sensor.human.1")
            });

        Expect(map.IsValid && map.Diagnostics.Count == 0 &&
               map.Snapshot.Roots.Count == 2 &&
               map.Snapshot.Nodes.Count == 5,
            "Structural mapper captures every root and recursively owned Goal descendant");
        Expect(map.TryGetExecutable(
                   sequence.StableId,
                   out KLEPExecutableStructuralNode sequenceNode) &&
               sequenceNode.ParentExecutableId == eatGoal.StableId &&
               sequenceNode.ParentLayerIndex == 0 &&
               sequenceNode.ParentLayerRequirement ==
                   KLEPGoalLayerRequirement.AnyCanFire &&
               sequenceNode.ParentChildIndex == 0 &&
               sequenceNode.ExecutionMode == KLEPExecutionMode.Solo,
            "Recursive snapshot retains parent, layer, child, and execution-mode provenance");
        Expect(map.TryGetExecutable(
                   eat.StableId,
                   out KLEPExecutableStructuralNode eatNode) &&
               eatNode.Locks.Count == 1 &&
               eatNode.Locks[0].Group == KLEPExecutableLockGroup.Validation &&
               eatNode.Locks[0].Expression.Kind == KLEPLockExpressionKind.All &&
               eatNode.GuaranteedDeclaredOutputs.Count == 1 &&
               eatNode.GuaranteedDeclaredOutputs[0].KeyId == ate.Id,
            "Structural nodes retain recursive Locks and guaranteed DeclaredOutput IDs");

        Expect(map.TryGetKeyRelation(
                   nearby.Id,
                   out KLEPStructuralKeyRelation nearbyRelation) &&
               nearbyRelation.Producers.Count == 1 &&
               nearbyRelation.Producers[0].Producer.StableExecutableId ==
                   sensor.StableId &&
               nearbyRelation.PositiveConsumers.Count == 1 &&
               nearbyRelation.PositiveConsumers[0].Consumer.StableExecutableId ==
                   move.StableId,
            "Structural relations connect one guaranteed producer to its positive Lock consumer");
        Expect(map.TryGetKeyRelation(
                   pacified.Id,
                   out KLEPStructuralKeyRelation pacifiedRelation) &&
               pacifiedRelation.PositiveConsumers.Count == 0 &&
               pacifiedRelation.NegativeConsumers.Count == 1 &&
               pacifiedRelation.NegativeConsumers[0].Consumer.StableExecutableId ==
                   eat.StableId &&
               pacifiedRelation.NegativeConsumers[0].ExpressionPath ==
                   "root.all[1].not",
            "Structural relations preserve negative polarity and exact expression provenance");
        Expect(Catch(() =>
                ((IList<KLEPExecutableStructuralNode>)map.Snapshot.Nodes).Clear())
                is NotSupportedException,
            "Captured catalog node collections are immutable");
    }

    private static void VerifyStructuralMapFingerprintAndProjectionAreDeterministic()
    {
        KLEPKeyDefinition input = Key("map.fingerprint.input");
        KLEPKeyDefinition result = Key("map.fingerprint.result");
        var producer = new ProbeExecutable(
            new KLEPExecutableDefinition(
                "map.fingerprint.producer",
                "Fingerprint Producer",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "map.fingerprint.lock",
                        "Input present",
                        new KLEPKeyPresent(input.Id.Value))
                },
                declaredOutputs: new[] { result }),
            KLEPExecutableTickStatus.Succeeded);
        var unrelated = new ProbeExecutable(
            Definition("map.fingerprint.unrelated", 1f),
            KLEPExecutableTickStatus.Succeeded);
        var firstRoot = new KLEPExecutableCatalogRoot(
            producer,
            "tenure.map.producer.1");
        var secondRoot = new KLEPExecutableCatalogRoot(
            unrelated,
            "tenure.map.unrelated.1");

        KLEPExecutableStructuralMap forward = KLEPExecutableStructuralMapper.Build(
            "catalog.fingerprint.1",
            new[] { firstRoot, secondRoot });
        KLEPExecutableStructuralMap reversed = KLEPExecutableStructuralMapper.Build(
            "catalog.fingerprint.1",
            new[] { secondRoot, firstRoot });
        KLEPExecutableStructuralMap changedRevision =
            KLEPExecutableStructuralMapper.Build(
                "catalog.fingerprint.2",
                new[] { firstRoot, secondRoot });
        KLEPExecutableStructuralMap changedTenure =
            KLEPExecutableStructuralMapper.Build(
                "catalog.fingerprint.1",
                new[]
                {
                    new KLEPExecutableCatalogRoot(
                        producer,
                        "tenure.map.producer.2"),
                    secondRoot
                });
        var changedLockProducer = new ProbeExecutable(
            new KLEPExecutableDefinition(
                "map.fingerprint.producer",
                "Fingerprint Producer",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "map.fingerprint.lock",
                        "Input present",
                        new KLEPKeyPresent("map.fingerprint.changed-input"))
                },
                declaredOutputs: new[] { result }),
            KLEPExecutableTickStatus.Succeeded);
        KLEPExecutableStructuralMap changedStructure =
            KLEPExecutableStructuralMapper.Build(
                "catalog.fingerprint.1",
                new[]
                {
                    new KLEPExecutableCatalogRoot(
                        changedLockProducer,
                        "tenure.map.producer.1"),
                    secondRoot
                });

        Expect(forward.Fingerprint.Equals(reversed.Fingerprint) &&
               forward.Fingerprint.Value == reversed.Fingerprint.Value &&
               forward.Fingerprint.CompareTo(reversed.Fingerprint) == 0,
            "Structural fingerprint canonicalizes root enumeration in ordinal order");
        Expect(!forward.Fingerprint.Equals(changedRevision.Fingerprint) &&
               !forward.Fingerprint.Equals(changedTenure.Fingerprint) &&
               !forward.Fingerprint.Equals(changedStructure.Fingerprint),
            "Structural fingerprint binds revision, stable tenures, and recursive authored structure");

        KLEPDirectSuccessfulCandidateProjection projection =
            forward.ProjectDirectSuccessfulCandidates(result.Id);
        Expect(projection.SourceMapIsValid &&
               projection.Candidates.Count == 1 &&
               projection.Candidates[0].ExecutableStableId == producer.StableId &&
               projection.Candidates[0].GuaranteedOutput.KeyId == result.Id &&
               projection.CatalogFingerprint.Equals(forward.Fingerprint),
            "Direct candidate projection reports the authored successful-output guarantee");
        Expect(typeof(KLEPDirectSuccessfulCandidate).GetProperty("IsEligible") == null &&
               typeof(KLEPDirectSuccessfulCandidateProjection).GetProperty(
                   "IsEligible") == null,
            "Structural candidate projection exposes no current-eligibility assertion");
        Expect(Catch(() =>
                ((IList<KLEPDirectSuccessfulCandidate>)projection.Candidates).Clear())
                is NotSupportedException,
            "Direct candidate projections are immutable");
    }

    private static void VerifyStructuralMapReturnsValidationDiagnostics()
    {
        KLEPKeyDefinition output = Key("map.invalid.output");
        var first = new ProbeExecutable(
            Definition(
                "map.invalid.duplicate",
                1f,
                declaredOutputs: new[] { output }),
            KLEPExecutableTickStatus.Succeeded);
        var second = new ProbeExecutable(
            Definition(
                "map.invalid.duplicate",
                2f,
                declaredOutputs: new[] { output }),
            KLEPExecutableTickStatus.Succeeded);

        KLEPExecutableStructuralMap duplicate =
            KLEPExecutableStructuralMapper.Build(
                "catalog.invalid.1",
                new[]
                {
                    new KLEPExecutableCatalogRoot(first, "tenure.duplicate"),
                    new KLEPExecutableCatalogRoot(second, "tenure.duplicate")
                });
        Expect(!duplicate.IsValid &&
               HasDiagnostic(
                   duplicate,
                   KLEPStructuralMapDiagnosticCode.DuplicateExecutableStableId) &&
               HasDiagnostic(
                   duplicate,
                   KLEPStructuralMapDiagnosticCode.DuplicateTenureId),
            "Structural mapper returns diagnostics for duplicate Executable and tenure IDs");
        KLEPDirectSuccessfulCandidateProjection invalidProjection =
            duplicate.ProjectDirectSuccessfulCandidates(output.Id);
        Expect(!invalidProjection.SourceMapIsValid &&
               invalidProjection.Candidates.Count == 0 &&
               invalidProjection.Diagnostics.Count == duplicate.Diagnostics.Count,
            "An invalid structural map cannot project an ambiguous direct candidate");

        KLEPExecutableStructuralMap missingIdentity =
            KLEPExecutableStructuralMapper.Build(
                " ",
                new[] { new KLEPExecutableCatalogRoot(first, "") });
        Expect(!missingIdentity.IsValid &&
               HasDiagnostic(
                   missingIdentity,
                   KLEPStructuralMapDiagnosticCode.MissingCatalogRevision) &&
               HasDiagnostic(
                   missingIdentity,
                   KLEPStructuralMapDiagnosticCode.MissingTenureId),
            "Structural mapper returns diagnostics for missing revision and tenure identities");

        var cyclic = new KLEPGoal(
            Definition(
                "map.invalid.cycle",
                1f,
                kind: KLEPExecutableKind.Goal));
        var cycleLayers = new System.Collections.ObjectModel.ReadOnlyCollection<
            KLEPGoalLayer>(new List<KLEPGoalLayer>
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { cyclic })
            });
        FieldInfo layersField = typeof(KLEPGoal).GetField(
            "layers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (layersField == null)
        {
            throw new InvalidOperationException(
                "The cycle regression could not find the Goal recipe layers.");
        }

        layersField.SetValue(cyclic, cycleLayers);
        KLEPExecutableStructuralMap cycle = KLEPExecutableStructuralMapper.Build(
            "catalog.invalid.cycle",
            new[]
            {
                new KLEPExecutableCatalogRoot(cyclic, "tenure.invalid.cycle")
            });
        Expect(!cycle.IsValid &&
               HasDiagnostic(
                   cycle,
                   KLEPStructuralMapDiagnosticCode.RecursiveExecutableCycle),
            "Structural mapper terminates and diagnoses a recursively corrupted Goal graph");
    }

    private static void VerifyAgentAdvancesNeuronExactlyOnce()
    {
        var neuron = new KLEPNeuron("agent.once.neuron");
        var agent = new KLEPAgent(neuron);

        var trace = agent.Tick();

        Expect(neuron.CycleIndex == 1 && trace.Decision.CycleIndex == 1,
            "One Agent Tick advances its Neuron exactly once");
        Expect(ReferenceEquals(agent.Neuron, neuron) &&
               ReferenceEquals(agent.LastTrace, trace),
            "Agent exposes its owned Neuron and latest immutable observation");
        Expect(agent.Configuration != null,
            "Omitted configuration resolves to the documented defaults");
        Expect(trace.Decision.IsPatient &&
               agent.CurrentSoloExecutableId == null,
            "An empty Agent remains patient without inventing an action");
        Expect(agent.GetExperienceSnapshot() != null,
            "Agent exposes a non-null read-only experience snapshot");
    }

    private static void VerifyAgentOwnsOneContinuousNeuronTickPath()
    {
        var runningNeuron = new KLEPNeuron("agent.attachment.running");
        runningNeuron.RegisterExecutable(new ProbeExecutable(
            Definition("agent.attachment.action", 1f),
            KLEPExecutableTickStatus.Running));
        var runningOwner = new KLEPAgent(runningNeuron);
        runningOwner.Tick();
        Exception attachmentFault = Catch(() => new KLEPAgent(runningNeuron));
        Expect(attachmentFault is InvalidOperationException,
            "A running Neuron remains exclusively claimed by its Agent");

        var patientNeuron = new KLEPNeuron("agent.attachment.patient");
        var patientOwner = new KLEPAgent(patientNeuron);
        patientOwner.Tick();
        Exception patientAttachmentFault =
            Catch(() => new KLEPAgent(patientNeuron));
        Expect(patientAttachmentFault is InvalidOperationException &&
               patientOwner.Tick().DidCompleteObservation,
            "Patient history remains on the same exclusive Agent owner");

        var claimedNeuron = new KLEPNeuron("agent.attachment.claimed");
        var firstOwner = new KLEPAgent(claimedNeuron);
        Exception secondOwnerFault = Catch(() => new KLEPAgent(claimedNeuron));
        Expect(secondOwnerFault is InvalidOperationException &&
               firstOwner.Tick().DidCompleteObservation &&
               claimedNeuron.CycleIndex == 1,
            "A second Agent cannot claim a Neuron while its first decision owner remains usable");

        MethodInfo publicNeuronTick = typeof(KLEPNeuron).GetMethod(
            "Tick",
            BindingFlags.Instance | BindingFlags.Public);
        Expect(publicNeuronTick == null,
            "Neuron exposes no independent public decision Tick beside its owning Agent");
    }

    private static void VerifyEnvironmentSignatureUsesStableKeyIdsOnly()
    {
        KLEPKeyDefinition alpha = Key("agent.signature.alpha");
        KLEPKeyDefinition beta = Key("agent.signature.beta");
        var firstNeuron = new KLEPNeuron("agent.signature.first");
        firstNeuron.InitializeKey(
            beta,
            Payload("value", 1),
            sourceId: "first.beta");
        firstNeuron.InitializeKey(
            alpha,
            Payload("value", 10),
            sourceId: "first.alpha.1");
        firstNeuron.InitializeKey(
            alpha,
            Payload("value", 20),
            sourceId: "first.alpha.2");
        KLEPKeySnapshot firstSnapshot = firstNeuron.TickViaAgent().KeySnapshot;

        var secondNeuron = new KLEPNeuron("agent.signature.second");
        secondNeuron.InitializeKey(
            alpha,
            Payload("value", -999),
            sourceId: "second.alpha");
        secondNeuron.InitializeKey(
            beta,
            Payload("value", 777),
            sourceId: "second.beta");
        KLEPKeySnapshot secondSnapshot = secondNeuron.TickViaAgent().KeySnapshot;

        KLEPKeyEnvironmentSignature first =
            KLEPKeyEnvironmentSignature.FromSnapshot(firstSnapshot);
        KLEPKeyEnvironmentSignature second =
            KLEPKeyEnvironmentSignature.FromSnapshot(secondSnapshot);

        Expect(first.Equals(second),
            "Environment identity ignores insertion order, duplicate occurrences, payload, source, and store occurrence IDs");
        Expect(first.GetHashCode() == second.GetHashCode(),
            "Equal Key-ID environments have equal hash codes");

        var novelNeuron = new KLEPNeuron("agent.signature.novel");
        novelNeuron.InitializeKey(alpha);
        novelNeuron.InitializeKey(beta);
        novelNeuron.InitializeKey(Key("agent.signature.gamma"));
        KLEPKeyEnvironmentSignature novel =
            KLEPKeyEnvironmentSignature.FromSnapshot(
                novelNeuron.TickViaAgent().KeySnapshot);
        Expect(!first.Equals(novel),
            "Adding one new stable Key ID creates a different environment");
    }

    private static void VerifyEnvironmentEntryIncludesScope()
    {
        var keyId = new KLEPKeyId("agent.signature.scoped");
        var local = new KLEPKeyEnvironmentEntry(KLEPKeyScope.Local, keyId);
        var global = new KLEPKeyEnvironmentEntry(KLEPKeyScope.Global, keyId);
        var distinct = new HashSet<KLEPKeyEnvironmentEntry> { local, global };

        Expect(!local.Equals(global) && distinct.Count == 2,
            "Environment entry identity includes both Key scope and stable Key ID");
    }

    private static void
        VerifyGuidanceFingerprintTracksPayloadWithoutSplittingLearningState()
    {
        KLEPKeyDefinition condition = Key("agent.evidence.condition");
        var neuron = new KLEPNeuron("agent.evidence.neuron");
        neuron.InitializeKey(
            condition,
            Payload("health", 10L),
            sourceId: "agent.evidence.first");
        var agent = new KLEPAgent(neuron);

        KLEPAgentTickTrace first = agent.Tick();
        Expect(first.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out KLEPKeyFact firstFact),
            "Guidance evidence test observes its initialized Key fact");

        neuron.ReplaceKey(
            firstFact,
            Payload("health", 9L),
            sourceId: "agent.evidence.changed");
        KLEPAgentTickTrace changed = agent.Tick();

        Expect(first.Environment.Equals(changed.Environment) &&
               first.PriorVisitCount == 0 &&
               changed.PriorVisitCount == 1 &&
               agent.GetVisitCount(first.Environment) == 2,
            "Payload changes stay inside the same presence-only Agent learning state");
        Expect(!first.EvidenceFingerprint.Equals(
                   changed.EvidenceFingerprint),
            "Guidance evidence identity changes when a visible Key payload changes");
        Expect(first.GuidanceRequest != null &&
               changed.GuidanceRequest != null &&
               first.GuidanceRequest.EvidenceFingerprint.Equals(
                   first.EvidenceFingerprint) &&
               changed.GuidanceRequest.EvidenceFingerprint.Equals(
                   changed.EvidenceFingerprint),
            "Each guidance request records the payload evidence visible in its trace");

        Expect(changed.Decision.KeySnapshot.TryGetFirst(
                condition.Id, out KLEPKeyFact changedFact),
            "Changed payload produces a replacement fact");
        neuron.ReplaceKey(
            changedFact,
            Payload("health", 9L),
            sourceId: "agent.evidence.same-payload-new-authority");
        KLEPAgentTickTrace samePayload = agent.Tick();

        Expect(changed.EvidenceFingerprint.Equals(
                   samePayload.EvidenceFingerprint) &&
               changed.EvidenceFingerprint.GetHashCode() ==
                   samePayload.EvidenceFingerprint.GetHashCode() &&
               changed.EvidenceFingerprint.CompareTo(
                   samePayload.EvidenceFingerprint) == 0,
            "Guidance evidence ignores replacement authority, source, and timing metadata when payload is unchanged");
        Expect(changed.Environment.Equals(samePayload.Environment) &&
               samePayload.PriorVisitCount == 2 &&
               agent.GetVisitCount(first.Environment) == 3,
            "Metadata-only replacement continues the same Agent learning state");
    }

    private static void VerifyGuidanceFingerprintCanonicalizesSupportedEvidence()
    {
        KLEPKeyDefinition key = Key("agent.fingerprint.key");
        var samples = new[]
        {
            (Id: "boolean.false", Value: KLEPKeyValue.FromBoolean(false)),
            (Id: "boolean.true", Value: KLEPKeyValue.FromBoolean(true)),
            (Id: "integer.min", Value: KLEPKeyValue.FromInteger(long.MinValue)),
            (Id: "integer.zero", Value: KLEPKeyValue.FromInteger(0L)),
            (Id: "integer.max", Value: KLEPKeyValue.FromInteger(long.MaxValue)),
            (Id: "number.min", Value: KLEPKeyValue.FromNumber(double.MinValue)),
            (Id: "number.zero", Value: KLEPKeyValue.FromNumber(0d)),
            (Id: "number.epsilon", Value: KLEPKeyValue.FromNumber(double.Epsilon)),
            (Id: "number.max", Value: KLEPKeyValue.FromNumber(double.MaxValue)),
            (Id: "text.zero", Value: KLEPKeyValue.FromText("0")),
            (Id: "text.delimited", Value: KLEPKeyValue.FromText(
                "3:a;1:b;:Omega=\u03A9;rocket=\uD83D\uDE80;e\u0301"))
        };

        var distinct = new HashSet<KLEPGuidanceEvidenceFingerprint>();
        var fields = new List<KeyValuePair<string, KLEPKeyValue>>();
        foreach ((string Id, KLEPKeyValue Value) sample in samples)
        {
            distinct.Add(Fingerprint(
                "agent.fingerprint.value." + sample.Id,
                key,
                Payload("value", sample.Value)));
            fields.Add(new KeyValuePair<string, KLEPKeyValue>(
                sample.Id,
                sample.Value));
        }

        Expect(distinct.Count == samples.Length,
            "Guidance fingerprint distinguishes all supported kinds, numeric limits, and delimiter-rich Unicode text");

        var reversedFields =
            new List<KeyValuePair<string, KLEPKeyValue>>(fields);
        reversedFields.Reverse();
        var forwardPayload = new KLEPKeyPayload(fields);
        var reversedPayload = new KLEPKeyPayload(reversedFields);

        KLEPGuidanceEvidenceFingerprint forward = Fingerprint(
            "agent.fingerprint.order.forward",
            key,
            forwardPayload,
            forwardPayload,
            KLEPKeyPayload.Empty);
        KLEPGuidanceEvidenceFingerprint reversed = Fingerprint(
            "agent.fingerprint.order.reversed",
            key,
            KLEPKeyPayload.Empty,
            reversedPayload,
            reversedPayload);
        KLEPGuidanceEvidenceFingerprint oneFewer = Fingerprint(
            "agent.fingerprint.order.fewer",
            key,
            forwardPayload,
            KLEPKeyPayload.Empty);

        Expect(forward.Equals(reversed) &&
               forward.GetHashCode() == reversed.GetHashCode() &&
               forward.CompareTo(reversed) == 0 &&
               forward.CanonicalId == reversed.CanonicalId,
            "Ordinal field and fact ordering produces equal canonical fingerprints, hashes, and comparison");
        Expect(!forward.Equals(oneFewer) &&
               forward.CompareTo(oneFewer) != 0,
            "Removing one duplicate payload occurrence changes the fingerprint");

        KLEPGuidanceEvidenceFingerprint noFacts = Fingerprint(
            "agent.fingerprint.empty.snapshot",
            key);
        KLEPGuidanceEvidenceFingerprint emptyPayloadFact = Fingerprint(
            "agent.fingerprint.empty.payload",
            key,
            KLEPKeyPayload.Empty);
        Expect(noFacts.Equals(KLEPGuidanceEvidenceFingerprint.Empty) &&
               noFacts.GetHashCode() ==
                   KLEPGuidanceEvidenceFingerprint.Empty.GetHashCode() &&
               noFacts.CompareTo(KLEPGuidanceEvidenceFingerprint.Empty) == 0 &&
               !noFacts.Equals(emptyPayloadFact),
            "An empty-payload fact remains distinct from an empty snapshot");
        Expect(Catch(() =>
                   KLEPGuidanceEvidenceFingerprint.FromSnapshot(null))
                   is ArgumentNullException,
            "Guidance fingerprint rejects a null snapshot explicitly");

        KLEPKeyValue positiveZero = KLEPKeyValue.FromNumber(0d);
        KLEPKeyValue negativeZero = KLEPKeyValue.FromNumber(
            BitConverter.Int64BitsToDouble(long.MinValue));
        KLEPGuidanceEvidenceFingerprint positiveZeroFingerprint = Fingerprint(
            "agent.fingerprint.zero.positive",
            key,
            Payload("value", positiveZero));
        KLEPGuidanceEvidenceFingerprint negativeZeroFingerprint = Fingerprint(
            "agent.fingerprint.zero.negative",
            key,
            Payload("value", negativeZero));
        Expect(positiveZero.Equals(negativeZero) &&
               positiveZero.GetHashCode() == negativeZero.GetHashCode() &&
               positiveZeroFingerprint.Equals(negativeZeroFingerprint) &&
               positiveZeroFingerprint.GetHashCode() ==
                   negativeZeroFingerprint.GetHashCode() &&
               positiveZeroFingerprint.CompareTo(negativeZeroFingerprint) == 0,
            "Signed zero has consistent Key-value and fingerprint equality, hashing, and comparison");

        KLEPKeyValue nullText = KLEPKeyValue.FromText(null);
        KLEPKeyValue emptyText = KLEPKeyValue.FromText(string.Empty);
        KLEPGuidanceEvidenceFingerprint nullTextFingerprint = Fingerprint(
            "agent.fingerprint.text.null",
            key,
            Payload("value", nullText));
        KLEPGuidanceEvidenceFingerprint emptyTextFingerprint = Fingerprint(
            "agent.fingerprint.text.empty",
            key,
            Payload("value", emptyText));
        Expect(nullText.Equals(emptyText) &&
               nullText.GetHashCode() == emptyText.GetHashCode() &&
               nullTextFingerprint.Equals(emptyTextFingerprint) &&
               nullTextFingerprint.GetHashCode() ==
                   emptyTextFingerprint.GetHashCode(),
            "Null text normalizes to empty text consistently");

        KLEPGuidanceEvidenceFingerprint local = FingerprintForScope(
            KLEPKeyScope.Local,
            KLEPKeyPayload.Empty);
        KLEPGuidanceEvidenceFingerprint global = FingerprintForScope(
            KLEPKeyScope.Global,
            KLEPKeyPayload.Empty);
        Expect(!local.Equals(global),
            "Guidance evidence identity includes visible Key scope");

        Expect(Catch(() => Payload("none", default)) is ArgumentException,
            "Unsupported None values cannot enter fingerprint evidence");
    }

    private static void VerifyTandemOutputChangesEnvironmentInSameTick()
    {
        KLEPKeyDefinition signal = Key("agent.tandem.signal");
        var sensor = new ProbeExecutable(
            Definition(
                "agent.tandem.sensor",
                0f,
                KLEPExecutionMode.Tandem,
                KLEPExecutableKind.Sensor,
                declaredOutputs: new[] { signal }),
            KLEPExecutableTickStatus.Succeeded,
            emitDefinition: signal,
            emitOnTick: 2);
        var neuron = new KLEPNeuron("agent.tandem.neuron");
        neuron.RegisterExecutable(sensor);
        var agent = new KLEPAgent(neuron);

        var before = agent.Tick();
        var emitted = agent.Tick();
        var repeated = agent.Tick();

        Expect(!before.Decision.KeySnapshot.Contains(signal.Id) &&
               emitted.Decision.KeySnapshot.Contains(signal.Id),
            "A Tandem sensor's Local output enters the Agent environment in its emission Tick");
        Expect(!before.Environment.Equals(emitted.Environment) &&
               emitted.IsNewEnvironment,
            "Same-Tick Tandem output is recognized as a novel Key environment");
        Expect(repeated.Environment.Equals(emitted.Environment) &&
               !repeated.IsNewEnvironment,
            "Revisiting the same settled Key-ID set is familiar rather than perpetually novel");
        Expect(agent.GetQValue(before.Environment, sensor.StableId) == 0f &&
               agent.GetQValue(emitted.Environment, sensor.StableId) == 0f &&
               emitted.LearningUpdates.Count == 0 &&
               repeated.LearningUpdates.Count == 0,
            "Terminal Tandem sensors never become Agent Q-learning samples");
    }

    private static void VerifySoloOutputChangesEnvironmentOnFollowingTick()
    {
        KLEPKeyDefinition command = Key("agent.solo.command");
        var action = new ProbeExecutable(
            Definition(
                "agent.solo.emitter",
                5f,
                declaredOutputs: new[] { command }),
            KLEPExecutableTickStatus.Succeeded,
            emitDefinition: command,
            emitOnTick: 1);
        var neuron = new KLEPNeuron("agent.solo.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(neuron);

        var emitted = agent.Tick();
        var visible = agent.Tick();

        Expect(!emitted.Decision.KeySnapshot.Contains(command.Id),
            "Solo output is absent from the environment used for the emitting decision");
        Expect(visible.Decision.KeySnapshot.Contains(command.Id) &&
               !emitted.Environment.Equals(visible.Environment),
            "Solo output becomes an Agent environment novelty at the following top-level Tick");
    }

    private static void VerifyDurationCountsAgentOwnedTicksNotWorldIndexDistance()
    {
        var world = new KLEPKeyStore(
            "agent.duration.world", KLEPKeyScope.Global);
        world.CommitBoundary(1);
        const string actionId = "agent.duration.action";
        var neuron = new KLEPNeuron("agent.duration.neuron", world);
        neuron.InitializeKey(Key("agent.duration.context"));
        neuron.RegisterExecutable(new CompletesOnSecondTickExecutable(
            Definition(actionId, 5f)));
        var agent = new KLEPAgent(neuron);

        var entered = agent.Tick();
        Exception earlyBoundaryFault = Catch(() => agent.Tick());
        Expect(earlyBoundaryFault is InvalidOperationException &&
               neuron.CycleIndex == 1 &&
               agent.LastTrace.Environment.Equals(entered.Environment) &&
               !agent.LastTrace.IsNewEnvironment,
            "A pre-boundary fault consumes no duration Tick and retains the last environment diagnostic");

        world.CommitBoundary(100);
        agent.Tick();
        world.CommitBoundary(101);
        var learned = agent.Tick();

        Expect(learned.LearningUpdates.Count == 1 &&
               learned.LearningUpdates[0].ExecutableStableId == actionId &&
               learned.LearningUpdates[0].ElapsedTicks == 2,
            "Run duration counts two Agent-owned Neuron Ticks despite a world index jump");
    }

    private static void VerifyRepeatedGoalSuccessRaisesConfidence()
    {
        const string goalId = "agent.goal.repeat";
        var goal = new KLEPGoal(
            Definition(goalId, 5f, kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            });
        var neuron = new KLEPNeuron("agent.goal.neuron");
        neuron.RegisterExecutable(goal);
        var agent = new KLEPAgent(neuron);

        var first = agent.Tick();
        float beforeLearning = agent.GetQValue(first.Environment, goalId);
        var second = agent.Tick();
        float afterOneUpdate = agent.GetQValue(second.Environment, goalId);
        var third = agent.Tick();
        float afterTwoUpdates = agent.GetQValue(third.Environment, goalId);

        Expect(beforeLearning == 0f &&
               afterOneUpdate > beforeLearning &&
               afterTwoUpdates > afterOneUpdate,
            "Back-to-back Goal success raises Q confidence for the same Key environment");
        Expect(Math.Abs(afterOneUpdate - 0.2f) < 0.00001f &&
               Math.Abs(afterTwoUpdates - 0.396f) < 0.00001f,
            "Goal learning applies alpha, gamma, and next-state bootstrap exactly once");
        Expect(second.LearningUpdates.Count == 1 &&
               third.LearningUpdates.Count == 1,
            "Each prior terminal Goal run produces one delayed learning update");
        Expect(third.Confidence > first.Confidence,
            "Repeated successful navigation raises the reported environment confidence");
    }

    private static void VerifyInterruptionLearnsExactlyOnce()
    {
        const string currentId = "agent.interrupt.current";
        var current = new ProbeExecutable(
            Definition(currentId, 5f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.interrupt.neuron");
        neuron.RegisterExecutable(current);
        var agent = new KLEPAgent(neuron);

        var entered = agent.Tick();
        var challenger = new ProbeExecutable(
            Definition("agent.interrupt.challenger", 6f),
            KLEPExecutableTickStatus.Running);
        neuron.RegisterExecutable(challenger);
        var interrupted = agent.Tick();

        Expect(interrupted.Decision.Executions.Count >= 2 &&
               agent.GetQValue(entered.Environment, currentId) == 0f,
            "An interruption is recorded now but waits for the next successful Tick's state");

        var learned = agent.Tick();
        float interruptedQ = agent.GetQValue(entered.Environment, currentId);
        var following = agent.Tick();

        Expect(interruptedQ < 0f && learned.LearningUpdates.Count == 1,
            "Interrupted is a negative sample applied exactly once on the following Tick");
        Expect(agent.GetQValue(entered.Environment, currentId) == interruptedQ &&
               following.LearningUpdates.Count == 0,
            "A continuing challenger cannot replay the prior interruption sample");
    }

    private static void VerifyInterruptionSurvivesFaultingChallenger()
    {
        const string currentId = "agent.interrupt.fault.current";
        const string challengerId = "agent.interrupt.fault.challenger";
        var current = new ProbeExecutable(
            Definition(currentId, 1f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.interrupt.fault.neuron");
        neuron.RegisterExecutable(current);
        var agent = new KLEPAgent(neuron);

        var entered = agent.Tick();
        var sentinel = new InvalidOperationException(
            "agent interruption challenger fault");
        neuron.RegisterExecutable(new ProbeExecutable(
            Definition(challengerId, 10f),
            KLEPExecutableTickStatus.Running,
            tickFault: sentinel));

        Exception caught = Catch(() => agent.Tick());
        Expect(ReferenceEquals(caught, sentinel) &&
               !agent.LastTrace.DidCompleteObservation &&
               agent.LastTrace.LearningUpdates.Count == 0,
            "Faulting challenger is rethrown and the faulted Tick performs no Q update");

        neuron.RemoveExecutable(challengerId);
        var recovered = agent.Tick();
        Expect(recovered.LearningUpdates.Count == 1 &&
               recovered.LearningUpdates[0].Outcome ==
                   KLEPAgentLearningOutcome.Interrupted &&
               agent.GetQValue(entered.Environment, currentId) < 0f,
            "Committed interruption remains pending when its challenger faults");

        float learnedQ = agent.GetQValue(entered.Environment, currentId);
        agent.Tick();
        Expect(agent.GetQValue(entered.Environment, currentId) == learnedQ,
            "Recovered interruption sample is committed exactly once");
    }

    private static void VerifyFailureLearnsOnFollowingTick()
    {
        const string failureId = "agent.failure.action";
        var failure = new ProbeExecutable(
            Definition(failureId, 5f),
            KLEPExecutableTickStatus.Failed);
        var neuron = new KLEPNeuron("agent.failure.neuron");
        neuron.RegisterExecutable(failure);
        var agent = new KLEPAgent(neuron);

        var failed = agent.Tick();
        Expect(agent.GetQValue(failed.Environment, failureId) == 0f,
            "Failure waits for a following successfully observed state before learning");

        var learned = agent.Tick();
        Expect(agent.GetQValue(failed.Environment, failureId) < 0f &&
               learned.LearningUpdates.Count == 1,
            "A failed Solo run becomes one negative delayed-learning sample");
    }

    private static void VerifyNonLearningCancellationDoesNotChangeQ()
    {
        const string actionId = "agent.cancel.locked";
        KLEPKeyDefinition permit = Key("agent.cancel.permit");
        var locked = new ProbeExecutable(
            new KLEPExecutableDefinition(
                actionId,
                actionId,
                KLEPExecutableKind.Action,
                new[]
                {
                    new KLEPLock(
                        "agent.cancel.lock",
                        "Permit",
                        new KLEPKeyPresent(permit.Id.Value))
                },
                baseAttractiveness: 5f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.cancel.neuron");
        KLEPKeyFact pendingPermit = neuron.InitializeKey(permit);
        neuron.RegisterExecutable(locked);
        var agent = new KLEPAgent(neuron);

        var running = agent.Tick();
        Expect(agent.CurrentSoloExecutableId == actionId,
            "Lock-gated action is Running before its non-learning cancellation");
        Expect(neuron.RemoveKey(pendingPermit),
            "Test stages removal of the exact permit occurrence");
        agent.Tick();
        var following = agent.Tick();

        Expect(agent.GetQValue(running.Environment, actionId) == 0f &&
               following.LearningUpdates.Count == 0,
            "LocksClosed cancellation is tracked but does not become a Q-learning sample");

        const string removedId = "agent.cancel.removed";
        var removedAction = new ProbeExecutable(
            Definition(removedId, 5f),
            KLEPExecutableTickStatus.Running);
        var removalNeuron = new KLEPNeuron("agent.cancel.removal-neuron");
        removalNeuron.RegisterExecutable(removedAction);
        var removalAgent = new KLEPAgent(removalNeuron);
        var removalStart = removalAgent.Tick();
        removalNeuron.RemoveExecutable(removedId);
        removalAgent.Tick();
        var removalFollowing = removalAgent.Tick();

        Expect(removalAgent.GetQValue(removalStart.Environment, removedId) == 0f &&
               removalFollowing.LearningUpdates.Count == 0,
            "Removed cancellation is observable but does not become a Q-learning sample");
    }

    private static void VerifyGuidanceIsImmutableAndDoesNotOverrideSelection()
    {
        const string actionId = "agent.guidance.action";
        var action = new ProbeExecutable(
            Definition(actionId, 5f),
            KLEPExecutableTickStatus.Running);
        var neuron = new KLEPNeuron("agent.guidance.neuron");
        neuron.RegisterExecutable(action);
        var agent = new KLEPAgent(neuron);

        var trace = agent.Tick();

        Expect(trace.NeedsGuidance && trace.GuidanceRequest != null,
            "A new low-confidence environment returns an explicit guidance request");
        Expect(trace.Decision.SelectedExecutableId == actionId &&
               agent.CurrentSoloExecutableId == actionId,
            "Guidance reporting does not override eligibility, ranking, or Solo selection");
        Expect(HasNoPublicSetters(trace.GuidanceRequest),
            "Guidance request exposes no public mutation surface");
    }

    private static void VerifyGuidanceObserverCannotMutateAgentBoundary()
    {
        const string directMutationMessage =
            "Direct Key mutation is not allowed during KLEPAgent.Tick.";

        KLEPKeyDefinition blockedKey =
            Key("agent.guidance.mutation.blocked-key");
        var addNeuron = new KLEPNeuron(
            "agent.guidance.mutation.add.neuron");
        var addAction = new ProbeExecutable(
            Definition("agent.guidance.mutation.add.action", 5f),
            KLEPExecutableTickStatus.Running);
        addNeuron.RegisterExecutable(addAction);
        var addObserver = new OneShotMutationGuidanceObserver(
            "agent.guidance.mutation.add.observer",
            () => addNeuron.AddKey(
                blockedKey,
                sourceId: "forbidden-guidance-observer"));
        var addAgent = new KLEPAgent(addNeuron, null, addObserver);

        Exception addFault = Catch(() => addAgent.Tick());
        Expect(addObserver.MutationAttempted &&
               addObserver.ObserveCount == 1 &&
               addFault is InvalidOperationException &&
               addFault.Message.StartsWith(
                   directMutationMessage,
                   StringComparison.Ordinal),
            "A guidance Observer cannot AddKey through a captured Neuron during Agent.Tick");
        KLEPAgentTickTrace addRecovered = addAgent.Tick();
        Expect(addRecovered.DidCompleteObservation &&
               addNeuron.CycleIndex == 2 &&
               addAction.TickCount == 2 &&
               !addRecovered.Decision.KeySnapshot.Contains(blockedKey.Id) &&
               addObserver.ObserveCount == 1,
            "A failed Observer AddKey attempt stages nothing and releases the next Agent Tick");

        const string blockedExecutableId =
            "agent.guidance.mutation.blocked-registration";
        var registerNeuron = new KLEPNeuron(
            "agent.guidance.mutation.register.neuron");
        var registerAction = new ProbeExecutable(
            Definition("agent.guidance.mutation.register.action", 5f),
            KLEPExecutableTickStatus.Running);
        var blockedRegistration = new ProbeExecutable(
            Definition(blockedExecutableId, 100f),
            KLEPExecutableTickStatus.Running);
        registerNeuron.RegisterExecutable(registerAction);
        var registerObserver = new OneShotMutationGuidanceObserver(
            "agent.guidance.mutation.register.observer",
            () => registerNeuron.RegisterExecutable(blockedRegistration));
        var registerAgent = new KLEPAgent(
            registerNeuron,
            null,
            registerObserver);

        Exception registrationFault = Catch(() => registerAgent.Tick());
        Expect(registerObserver.MutationAttempted &&
               registerObserver.ObserveCount == 1 &&
               registrationFault is InvalidOperationException &&
               registrationFault.Message.StartsWith(
                   directMutationMessage,
                   StringComparison.Ordinal),
            "A guidance Observer cannot RegisterExecutable through a captured Neuron during Agent.Tick");
        KLEPAgentTickTrace registrationRecovered = registerAgent.Tick();
        Expect(registrationRecovered.DidCompleteObservation &&
               registerNeuron.CycleIndex == 2 &&
               registerAction.TickCount == 2 &&
               !HasRootDefinition(registerNeuron, blockedExecutableId) &&
               blockedRegistration.TickCount == 0 &&
               registerObserver.ObserveCount == 1,
            "A failed Observer registration stages nothing and releases the next Agent Tick");

        var remapNeuron = new KLEPNeuron(
            "agent.guidance.mutation.remap.neuron");
        var remapAction = new ProbeExecutable(
            Definition("agent.guidance.mutation.remap.action", 5f),
            KLEPExecutableTickStatus.Running);
        remapNeuron.RegisterExecutable(remapAction);
        var structuralObserver = new CountingStructuralObserver();
        KLEPAgent remapAgent = null;
        var remapObserver = new OneShotMutationGuidanceObserver(
            "agent.guidance.mutation.remap.observer",
            () => remapAgent.RequestExecutableRemap());
        remapAgent = new KLEPAgent(
            remapNeuron,
            null,
            remapObserver,
            structuralObserver,
            null);

        Exception remapFault = Catch(() => remapAgent.Tick());
        KLEPExecutableStructuralMap acceptedBeforeRecovery =
            remapAgent.ExecutableMap;
        Expect(remapObserver.MutationAttempted &&
               remapObserver.ObserveCount == 1 &&
               remapFault is InvalidOperationException &&
               remapFault.Message ==
                   "An Executable remap cannot be requested during Agent.Tick." &&
               structuralObserver.ObserveCount == 1,
            "A guidance Observer cannot RequestExecutableRemap during Agent.Tick");
        KLEPAgentTickTrace remapRecovered = remapAgent.Tick();
        Expect(remapRecovered.DidCompleteObservation &&
               remapNeuron.CycleIndex == 2 &&
               remapAction.TickCount == 2 &&
               structuralObserver.ObserveCount == 1 &&
               ReferenceEquals(
                   acceptedBeforeRecovery,
                   remapAgent.ExecutableMap) &&
               remapObserver.ObserveCount == 1,
            "A failed Observer remap request schedules no remap and releases the next Agent Tick");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        KLEPAgent first = MakeRepeatingGoalAgent("agent.repeat.a");
        KLEPAgent second = MakeRepeatingGoalAgent("agent.repeat.b");
        const string goalId = "agent.repeat.goal";

        for (int tick = 0; tick < 5; tick++)
        {
            var firstTrace = first.Tick();
            var secondTrace = second.Tick();
            Expect(firstTrace.Environment.Equals(secondTrace.Environment) &&
                   firstTrace.VisitCount == secondTrace.VisitCount &&
                   firstTrace.Familiarity == secondTrace.Familiarity &&
                   firstTrace.BestEligibleQValue == secondTrace.BestEligibleQValue &&
                   firstTrace.Confidence == secondTrace.Confidence &&
                   firstTrace.IsNewEnvironment == secondTrace.IsNewEnvironment &&
                   firstTrace.NeedsGuidance == secondTrace.NeedsGuidance &&
                   firstTrace.LearningUpdates.Count ==
                       secondTrace.LearningUpdates.Count,
                $"Equivalent Agent history is deterministic at Tick {tick + 1}");
            Expect(first.GetQValue(firstTrace.Environment, goalId) ==
                   second.GetQValue(secondTrace.Environment, goalId),
                $"Equivalent Q tables are deterministic at Tick {tick + 1}");
        }
    }

    private static void VerifyFaultIsObservedAndRethrownUnchanged()
    {
        var sentinel = new InvalidOperationException("agent fault sentinel");
        const string actionId = "agent.fault.action";
        var faulting = new ProbeExecutable(
            Definition(actionId, 5f),
            KLEPExecutableTickStatus.Running,
            tickFault: sentinel);
        var neuron = new KLEPNeuron("agent.fault.neuron");
        neuron.RegisterExecutable(faulting);
        var agent = new KLEPAgent(neuron);

        Exception caught = Catch(() => agent.Tick());

        Expect(ReferenceEquals(caught, sentinel),
            "Agent rethrows the original Neuron lifecycle exception unchanged");
        Expect(agent.LastTrace != null &&
               agent.LastTrace.Decision.Fault != null &&
               agent.LastTrace.Decision.Fault.ExecutableStableId == actionId,
            "Agent retains the Neuron fault trace before rethrowing");

        neuron.RemoveExecutable(actionId);
        var recovered = agent.Tick();
        Expect(agent.GetQValue(recovered.Environment, actionId) == 0f &&
               recovered.LearningUpdates.Count == 0,
            "A fault is observable but never treated as a learning sample");
    }

    private static KLEPAgentDesire UnitDesire(
        string stableId,
        KLEPLockExpression expression)
    {
        return new KLEPAgentDesire(
            stableId,
            "1",
            expression,
            1f);
    }

    private static KLEPAgent MakeRepeatingGoalAgent(string neuronId)
    {
        var goal = new KLEPGoal(
            Definition(
                "agent.repeat.goal",
                5f,
                kind: KLEPExecutableKind.Goal),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            });
        var neuron = new KLEPNeuron(neuronId);
        neuron.RegisterExecutable(goal);
        return new KLEPAgent(neuron);
    }

    private static KLEPExecutableDefinition Definition(
        string stableId,
        float score,
        KLEPExecutionMode mode = KLEPExecutionMode.Solo,
        KLEPExecutableKind kind = KLEPExecutableKind.Action,
        IEnumerable<KLEPKeyDefinition> declaredOutputs = null)
    {
        return new KLEPExecutableDefinition(
            stableId,
            stableId,
            kind,
            baseAttractiveness: score,
            executionMode: mode,
            declaredOutputs: declaredOutputs);
    }

    private static KLEPKeyDefinition Key(string stableId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            stableId,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static KLEPKeyPayload Payload(string field, KLEPKeyValue value)
    {
        return new KLEPKeyPayload(
            new[] { new KeyValuePair<string, KLEPKeyValue>(field, value) });
    }

    private static KLEPGuidanceEvidenceFingerprint Fingerprint(
        string neuronId,
        KLEPKeyDefinition definition,
        params KLEPKeyPayload[] occurrences)
    {
        var neuron = new KLEPNeuron(neuronId);
        foreach (KLEPKeyPayload payload in occurrences)
        {
            neuron.InitializeKey(
                definition,
                payload,
                sourceId: neuronId);
        }

        return KLEPGuidanceEvidenceFingerprint.FromSnapshot(
            neuron.TickViaAgent().KeySnapshot);
    }

    private static KLEPGuidanceEvidenceFingerprint FingerprintForScope(
        KLEPKeyScope scope,
        KLEPKeyPayload payload)
    {
        var definition = new KLEPKeyDefinition(
            new KLEPKeyId("agent.fingerprint.scope"),
            "agent.fingerprint.scope",
            scope: scope,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        KLEPKeyStore globalStore = scope == KLEPKeyScope.Global
            ? new KLEPKeyStore(
                "agent.fingerprint.scope.global-store",
                KLEPKeyScope.Global)
            : null;
        var neuron = new KLEPNeuron(
            "agent.fingerprint.scope.neuron",
            globalStore);
        neuron.InitializeKey(
            definition,
            payload,
            sourceId: "agent.fingerprint.scope.source");
        globalStore?.CommitBoundary(1);

        return KLEPGuidanceEvidenceFingerprint.FromSnapshot(
            neuron.TickViaAgent().KeySnapshot);
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

    private static bool HasRootDefinition(KLEPNeuron neuron, string stableId)
    {
        IReadOnlyList<KLEPExecutableDefinition> definitions =
            neuron.GetRootExecutableDefinitionsSnapshot();
        for (int index = 0; index < definitions.Count; index++)
        {
            if (StringComparer.Ordinal.Equals(
                    definitions[index].StableId, stableId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDiagnostic(
        KLEPExecutableStructuralMap map,
        KLEPStructuralMapDiagnosticCode code)
    {
        foreach (KLEPStructuralMapDiagnostic diagnostic in map.Diagnostics)
        {
            if (diagnostic.Code == code)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNoPublicSetters(object value)
    {
        foreach (PropertyInfo property in value.GetType().GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            MethodInfo setter = property.GetSetMethod(nonPublic: false);
            if (setter != null)
            {
                return false;
            }
        }

        return true;
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

        throw new InvalidOperationException("Expected an exception, but none was thrown.");
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
        }
    }

    private sealed class RecordingLearnedDesirePolicy :
        IKLEPLearnedDesireSelectionPolicy
    {
        private readonly Func<
            KLEPLearnedDesireSelectionRequest,
            KLEPLearnedDesireSelectionCandidate,
            float> contribution;
        private readonly KLEPLearnedDesireContributionDisposition
            zeroDisposition;
        private readonly bool omitLastResult;
        private readonly bool mutateBindingsDuringEvaluation;
        private string bindingFingerprint =
            "agent.test.learned-desire.bindings.v1";

        internal RecordingLearnedDesirePolicy(
            Func<
                KLEPLearnedDesireSelectionRequest,
                KLEPLearnedDesireSelectionCandidate,
                float> contribution,
            KLEPLearnedDesireContributionDisposition zeroDisposition =
                KLEPLearnedDesireContributionDisposition.UnknownEvidence,
            bool omitLastResult = false,
            bool mutateBindingsDuringEvaluation = false)
        {
            this.contribution = contribution ??
                throw new ArgumentNullException(nameof(contribution));
            this.zeroDisposition = zeroDisposition;
            this.omitLastResult = omitLastResult;
            this.mutateBindingsDuringEvaluation =
                mutateBindingsDuringEvaluation;
        }

        public string StableId => "agent.test.learned-desire.policy";
        public string Version => "1";
        public string BindingFingerprint => bindingFingerprint;
        public int EvaluateCount { get; private set; }
        public KLEPLearnedDesireSelectionRequest LastRequest { get; private set; }

        public KLEPLearnedDesireSelectionBatch Evaluate(
            KLEPLearnedDesireSelectionRequest request)
        {
            EvaluateCount++;
            LastRequest = request ?? throw new ArgumentNullException(
                nameof(request));
            if (mutateBindingsDuringEvaluation)
            {
                bindingFingerprint += ".changed";
            }
            var frame = new KLEPLearnedDesireSelectionEvidenceFrame(
                "agent.test.desires",
                "agent.test.desires.definitions.v1",
                $"agent.test.desires.snapshot.{request.CurrentSnapshot.Tick}",
                request.CurrentSnapshot.Tick,
                $"agent.test.moment.{request.CurrentSnapshot.Tick}",
                "agent.test.context",
                "agent.test.context.schema",
                "1",
                "agent.test.critic",
                "1",
                3,
                2);
            var evaluations = new List<
                KLEPLearnedDesireCandidateEvaluation>();
            int resultCount = omitLastResult
                ? request.Candidates.Count - 1
                : request.Candidates.Count;
            for (int index = 0; index < resultCount; index++)
            {
                KLEPLearnedDesireSelectionCandidate candidate =
                    request.Candidates[index];
                float value = contribution(request, candidate);
                string effectSource = candidate.ExecutableStableId;
                KLEPLearnedDesireContributionDisposition disposition =
                    value == 0f
                        ? zeroDisposition
                        : KLEPLearnedDesireContributionDisposition.Applied;
                float confidence = disposition ==
                    KLEPLearnedDesireContributionDisposition.UnknownEvidence
                        ? 0f
                        : 1f;
                var term = new KLEPLearnedDesireContributionTrace(
                    "agent.test.desire",
                    "1",
                    "agent.test.desire.evaluator",
                    "1",
                    effectSource,
                    "agent.test.bucket." + candidate.ExecutableStableId,
                    frame.CriticRevision,
                    confidence == 0f ? 0 : 1,
                    value,
                    0f,
                    0f,
                    confidence,
                    1f,
                    1f,
                    1f,
                    value,
                    disposition,
                    "Deterministic Agent smoke evidence.");
                evaluations.Add(new KLEPLearnedDesireCandidateEvaluation(
                    StableId,
                    Version,
                    BindingFingerprint,
                    frame,
                    candidate.ExecutableStableId,
                    candidate.RootTenureId,
                    effectSource,
                    KLEPLearnedDesireCandidateDisposition.Applied,
                    candidate.PrePolicyScore,
                    candidate.IsCurrentRunning,
                    value,
                    new[] { term },
                    "Deterministic Agent smoke candidate."));
            }

            return new KLEPLearnedDesireSelectionBatch(
                StableId,
                Version,
                BindingFingerprint,
                request.CatalogRevision,
                request.CatalogFingerprint,
                request.CurrentEvidenceFingerprint,
                frame,
                evaluations);
        }
    }

    private sealed class ConstantGoalAttractionEvaluator :
        IKLEPGoalAttractionEvaluator
    {
        private readonly float contribution;

        internal ConstantGoalAttractionEvaluator(
            string stableId,
            string version,
            float contribution)
        {
            StableId = stableId;
            Version = version;
            this.contribution = contribution;
        }

        public string StableId { get; }
        public string Version { get; }

        public KLEPGoalAttractionEvaluation Evaluate(
            KLEPGoalAttractionContext context)
        {
            return new KLEPGoalAttractionEvaluation(
                contribution,
                "Fixed integration-test intrinsic attraction.");
        }
    }

    private sealed class PolishingGuidanceObserver : IKLEPGuidanceObserver
    {
        private readonly string targetExecutableId;
        private readonly float scoreDelta;

        internal PolishingGuidanceObserver(
            string targetExecutableId,
            float scoreDelta)
        {
            this.targetExecutableId = targetExecutableId;
            this.scoreDelta = scoreDelta;
        }

        public string StableId => "agent.test.guidance.polish";
        public string Version => "1";
        public int ObserveCount { get; private set; }

        public KLEPGuidanceAdvice Observe(KLEPAgentGuidanceContext context)
        {
            ObserveCount++;
            return new KLEPGuidanceAdvice(
                StableId,
                Version,
                context.Request.CycleIndex,
                context.Request.Environment,
                targetExecutableId,
                Array.Empty<string>(),
                scoreDelta);
        }
    }

    private sealed class OneShotMutationGuidanceObserver :
        IKLEPGuidanceObserver
    {
        private readonly Action mutation;

        internal OneShotMutationGuidanceObserver(
            string stableId,
            Action mutation)
        {
            StableId = stableId;
            this.mutation = mutation ??
                throw new ArgumentNullException(nameof(mutation));
        }

        public string StableId { get; }
        public string Version => "1";
        public int ObserveCount { get; private set; }
        public bool MutationAttempted { get; private set; }

        public KLEPGuidanceAdvice Observe(KLEPAgentGuidanceContext context)
        {
            ObserveCount++;
            if (!MutationAttempted)
            {
                MutationAttempted = true;
                mutation();
            }

            return null;
        }
    }

    private sealed class CountingStructuralObserver :
        IKLEPExecutableStructuralObserver
    {
        public string StableId => "agent.test.structural.counting";
        public string Version => "1";
        public int ObserveCount { get; private set; }

        public KLEPExecutableStructuralMap ObserveStructure(
            KLEPExecutableCatalogSnapshot snapshot)
        {
            ObserveCount++;
            return KLEPExecutableStructuralMapper.Build(snapshot);
        }
    }

    private sealed class FaultingStructuralObserver :
        IKLEPExecutableStructuralObserver
    {
        private readonly Func<KLEPExecutableCatalogSnapshot,
            KLEPExecutableStructuralMap> observe;

        internal FaultingStructuralObserver(
            string stableId,
            Func<KLEPExecutableCatalogSnapshot,
                KLEPExecutableStructuralMap> observe)
        {
            StableId = stableId ?? throw new ArgumentNullException(
                nameof(stableId));
            this.observe = observe ?? throw new ArgumentNullException(
                nameof(observe));
        }

        public string StableId { get; }
        public string Version => "1";

        public KLEPExecutableStructuralMap ObserveStructure(
            KLEPExecutableCatalogSnapshot snapshot)
        {
            return observe(snapshot);
        }
    }

    private sealed class CompletingProjectionObserver :
        IKLEPCandidateStateProjectionObserver
    {
        internal const string Provenance =
            "Test-owned complete successful-run-completion model.";

        private readonly string targetExecutableId;
        private readonly string[] targetAdditions;

        internal CompletingProjectionObserver(
            string targetExecutableId,
            params string[] targetAdditions)
        {
            this.targetExecutableId = targetExecutableId ??
                throw new ArgumentNullException(nameof(targetExecutableId));
            this.targetAdditions = targetAdditions ??
                throw new ArgumentNullException(nameof(targetAdditions));
        }

        public string StableId => "agent.test.candidate-state.complete";
        public string Version => "1";

        public KLEPCandidateStateProjection ProjectCandidateState(
            KLEPCandidateStateProjectionRequest request)
        {
            var ids = new List<KLEPKeyId>();
            for (int index = 0;
                 index < request.CurrentSnapshot.Facts.Count;
                 index++)
            {
                ids.Add(request.CurrentSnapshot.Facts[index].KeyId);
            }

            if (StringComparer.Ordinal.Equals(
                    request.TargetExecutableId, targetExecutableId))
            {
                for (int index = 0; index < targetAdditions.Length; index++)
                {
                    ids.Add(new KLEPKeyId(targetAdditions[index]));
                }
            }

            return KLEPCandidateStateProjection.Complete(
                request,
                StableId,
                Version,
                new KLEPCompleteProjectedKeyState(ids),
                Provenance);
        }
    }

    private sealed class MismatchedProjectionObserver :
        IKLEPCandidateStateProjectionObserver
    {
        public string StableId => "agent.test.candidate-state.mismatched";
        public string Version => "1";

        public KLEPCandidateStateProjection ProjectCandidateState(
            KLEPCandidateStateProjectionRequest request)
        {
            return KLEPCandidateStateProjection.CreateBoundResponse(
                KLEPCandidateStateProjectionKind.Complete,
                StableId,
                Version,
                request.CatalogRevision,
                request.CatalogFingerprint,
                request.TargetExecutableId + ".wrong",
                request.TargetRootTenureId,
                request.CurrentEvidenceFingerprint,
                request.Horizon,
                new KLEPCompleteProjectedKeyState(),
                "Deliberately stale test response.");
        }
    }

    private sealed class ProbeExecutable : KLEPExecutableBase
    {
        private readonly KLEPExecutableTickStatus status;
        private readonly KLEPKeyDefinition emitDefinition;
        private readonly int emitOnTick;
        private readonly Exception tickFault;

        public ProbeExecutable(
            KLEPExecutableDefinition definition,
            KLEPExecutableTickStatus status,
            KLEPKeyDefinition emitDefinition = null,
            int emitOnTick = -1,
            Exception tickFault = null)
            : base(definition)
        {
            this.status = status;
            this.emitDefinition = emitDefinition;
            this.emitOnTick = emitOnTick;
            this.tickFault = tickFault;
        }

        public int TickCount { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            if (tickFault != null)
            {
                throw tickFault;
            }

            if (emitDefinition != null)
            {
                if (TickCount != emitOnTick)
                {
                    return KLEPExecutableTickStatus.Failed;
                }

                context.Add(emitDefinition);
            }

            return status;
        }
    }

    private sealed class CompletesOnSecondTickExecutable : KLEPExecutableBase
    {
        internal CompletesOnSecondTickExecutable(
            KLEPExecutableDefinition definition)
            : base(definition)
        {
        }

        private int tickCount;

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            tickCount++;
            return tickCount >= 2
                ? KLEPExecutableTickStatus.Succeeded
                : KLEPExecutableTickStatus.Running;
        }
    }

    private sealed class EmitsOnceCompletesOnThirdTickExecutable :
        KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition emitted;

        internal EmitsOnceCompletesOnThirdTickExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition emitted)
            : base(definition)
        {
            this.emitted = emitted ??
                throw new ArgumentNullException(nameof(emitted));
        }

        public int TickCount { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            if (TickCount == 1)
            {
                context.Add(emitted);
            }

            return TickCount >= 3
                ? KLEPExecutableTickStatus.Succeeded
                : KLEPExecutableTickStatus.Running;
        }
    }

    private sealed class CompleteKeyState : IKLEPLockKeySource
    {
        private readonly HashSet<string> present =
            new HashSet<string>(StringComparer.Ordinal);

        internal CompleteKeyState(params string[] stableKeyIds)
        {
            if (stableKeyIds == null)
            {
                throw new ArgumentNullException(nameof(stableKeyIds));
            }

            for (int index = 0; index < stableKeyIds.Length; index++)
            {
                string stableKeyId = stableKeyIds[index];
                if (string.IsNullOrWhiteSpace(stableKeyId))
                {
                    throw new ArgumentException(
                        "Complete test states require stable Key IDs.",
                        nameof(stableKeyIds));
                }

                present.Add(stableKeyId);
            }
        }

        public bool Contains(string stableKeyId)
        {
            return stableKeyId != null && present.Contains(stableKeyId);
        }
    }
}
