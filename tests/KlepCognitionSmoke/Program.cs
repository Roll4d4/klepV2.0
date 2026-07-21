using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Cognition;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;
using Roll4d4.Klep.Memory;
using Roll4d4.Klep.Observer;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyCoordinatorClosesOneOrderedCausalExperience();
        VerifyFrictionOnlyAdvanceOwnsNoExperience();
        VerifyDesireEvidenceIsValidatedCopiedAndReturnedWithoutANewPhase();
        VerifyInvalidDesireEvidenceCannotAdvanceEmotionOrMemory();
        VerifyInvalidCausalityCannotAdvanceEmotionOrMemory();
        VerifyPostEmotionMemoryFaultRollsBackAndRetriesExactly();
        VerifyEvidenceAdaptersAreEligibleReadOnlyAndProvenanceBearing();
        VerifyReadOnlyGuardsRunWhenProjectPolicyThrows();
        VerifyEquivalentRunsAreDeterministic();

        Console.WriteLine($"KLEP Cognition smoke passed: {assertions} assertions.");
    }

    private static void VerifyFrictionOnlyAdvanceOwnsNoExperience()
    {
        Fixture fixture = CreateFixture();
        KLEPEmotionSnapshot idle =
            fixture.Composition.AdvanceEmotionWithoutExperience(2);

        Expect(idle.Tick == 2 &&
               idle.Influences.Count == 0 &&
               fixture.Coordinator.Emotion.Tick == 2,
            "The composition advances one explicit friction-only Emotion Tick");
        Expect(fixture.Coordinator.Memory.CurrentTick == 0 &&
               fixture.Coordinator.LastTransition == null,
            "A friction-only Tick creates no Ethics, Memory, or cognition transition");

        CaptureThrows<ArgumentOutOfRangeException>(
            () => fixture.Composition.AdvanceEmotionWithoutExperience(4),
            "A skipped friction-only Emotion Tick must be rejected");
        Expect(fixture.Coordinator.Emotion.Tick == 2 &&
               fixture.Coordinator.Memory.CurrentTick == 0,
            "A skipped friction-only Tick is rejected without partial state");
    }

    private static void VerifyCoordinatorClosesOneOrderedCausalExperience()
    {
        Fixture fixture = CreateFixture();
        long neuronTickBefore = fixture.SourceNeuron.CycleIndex;

        KLEPCognitionTransition<EventContext> transition =
            fixture.Composition.Process(fixture.Request);

        Expect(fixture.Composition.Observer.EvidenceSources.Count == 2 &&
               ContainsSource(
                   fixture.Composition.Observer,
                   fixture.Composition.MemoryEvidenceSource) &&
               ContainsSource(
                   fixture.Composition.Observer,
                   fixture.Composition.EmotionEvidenceSource),
            "The production composition owns the coordinator and injectible Observer with both cognition adapters");

        Expect(transition.Steps.Count == 3 &&
               transition.Steps[0].Phase == KLEPCognitionPhase.EthicsEvaluated &&
               transition.Steps[1].Phase == KLEPCognitionPhase.EmotionAdvanced &&
               transition.Steps[2].Phase == KLEPCognitionPhase.MemoryRecorded,
            "The coordinator exposes the exact Ethics, Emotion, then Memory order");
        Expect(transition.Steps[0].Tick == 2 &&
               transition.Steps[1].Tick == 2 &&
               transition.Steps[2].Tick == 2 &&
               transition.Steps[0].ProvenanceId == "evaluation.help.1" &&
               transition.Steps[2].ProvenanceId == "experience.help.1",
            "Each phase retains its explicit subsystem clock and provenance ID");
        Expect(transition.EthicsEvaluation.Judgment.Impulse ==
                   new KLEPEmotionVector(0.4f, 0.2f) &&
               transition.EmotionSnapshot.Influences.Count == 1 &&
               transition.EmotionSnapshot.Influences[0].SourceId ==
                   transition.EthicsEvaluation.EvaluationId,
            "Emotion integrates the exact guarded Ethics influence");
        Expect(transition.Experience.Ethics.Count == 1 &&
               transition.Experience.Ethics[0].EvaluationId ==
                   transition.EthicsEvaluation.EvaluationId &&
               transition.Experience.Emotion.ProducedTick ==
                   transition.EmotionSnapshot.Tick,
            "Memory copies the Ethics evaluation and produced Emotion consequence");
        Expect(transition.Experience.ActionOutcome.WasSuccessful &&
               transition.Experience.ActionStableId == "action.help" &&
               transition.MemorySnapshot.Clusters.Count == 1 &&
               transition.MemorySnapshot.Clusters[0].RecentEpisodes[0].ExperienceId ==
                   transition.Experience.ExperienceId,
            "The recorded experience preserves factual lifecycle success separately from its evaluation");
        Expect(fixture.Coordinator.Emotion.Tick == 2 &&
               fixture.Coordinator.Memory.CurrentTick == 2 &&
               ReferenceEquals(fixture.Coordinator.LastTransition, transition),
            "Successful composition commits one Emotion Tick and one Memory Tick");
        Expect(fixture.SourceNeuron.CycleIndex == neuronTickBefore,
            "Cognition composition cannot advance the Neuron that supplied its observations");
    }

    private static void
        VerifyDesireEvidenceIsValidatedCopiedAndReturnedWithoutANewPhase()
    {
        Fixture fixture = CreateFixture();
        KLEPMemoryMoment prior = fixture.Request.Moments[0];
        KLEPMemoryMoment consequence = fixture.Request.Moments[
            fixture.Request.Moments.Count - 1];
        KLEPDesireEffectVector desireEffects = DesireVector(
            prior.MomentId,
            consequence.MomentId,
            KLEPDesireEffectAttribution.ActionOwned,
            fixture.Request.ActionOutcome.ExecutableStableId,
            fixture.Request.ActionOutcome.RunIndex,
            satisfactionBefore: 0.1f,
            satisfactionAfter: 0.9f,
            pressureBefore: 5f,
            pressureAfter: 1f);
        KLEPCognitionExperienceRequest<EventContext> request =
            CopyRequestWithDesire(fixture.Request, desireEffects);

        KLEPCognitionTransition<EventContext> transition =
            fixture.Composition.Process(request);

        Expect(ReferenceEquals(transition.DesireEffects, desireEffects) &&
               ReferenceEquals(request.DesireEffects, desireEffects),
            "Cognition returns the same immutable evaluated Desire vector instead of reevaluating it");
        Expect(transition.Steps.Count == 3 &&
               transition.Steps[0].Phase ==
                   KLEPCognitionPhase.EthicsEvaluated &&
               transition.Steps[1].Phase ==
                   KLEPCognitionPhase.EmotionAdvanced &&
               transition.Steps[2].Phase ==
                   KLEPCognitionPhase.MemoryRecorded,
            "Archiving Desire evidence adds no state-producing cognition phase");
        Expect(transition.Experience.DesireEffects != null &&
               !ReferenceEquals(
                   transition.Experience.DesireEffects,
                   desireEffects) &&
               transition.Experience.DesireEffects.TransitionId ==
                   desireEffects.TransitionId &&
               transition.Experience.DesireEffects.Effects[0].Effect ==
                   desireEffects.Effects[0].Effect &&
               transition.Experience.DesireEffects.Effects[0]
                   .ExplanationBefore == "Before helping." &&
               transition.Experience.DesireEffects.Effects[0]
                   .ExplanationAfter == "After helping." &&
               transition.Experience.DesireEffects.Effects[0]
                   .EvidenceIdsBefore.Count == 1 &&
               transition.Experience.DesireEffects.Effects[0]
                   .EvidenceIdsAfter.Count == 1 &&
               transition.Experience.DesireEffects.Effects[0]
                   .IsEligibleForAutomaticExpectationLearning,
            "Memory receives a defensive archival copy with raw Desire effect and ActionOwned attribution");
    }

    private static void VerifyInvalidDesireEvidenceCannotAdvanceEmotionOrMemory()
    {
        Fixture wrongMomentFixture = CreateFixture();
        KLEPMemoryMoment consequence = wrongMomentFixture.Request.Moments[
            wrongMomentFixture.Request.Moments.Count - 1];
        KLEPDesireEffectVector wrongMoment = DesireVector(
            "unrelated.prior",
            consequence.MomentId,
            KLEPDesireEffectAttribution.Unknown);
        ExpectThrows<ArgumentException>(
            () => wrongMomentFixture.Composition.Process(
                CopyRequestWithDesire(
                    wrongMomentFixture.Request,
                    wrongMoment)),
            "Cognition rejects Desire evidence not bound to the exact experience boundary MomentIds");
        Expect(wrongMomentFixture.Coordinator.Emotion.Tick == 1 &&
               wrongMomentFixture.Coordinator.Memory.CurrentTick == 0 &&
               wrongMomentFixture.Coordinator.LastTransition == null,
            "Desire MomentId preflight fails before Emotion or Memory can advance");

        Fixture wrongActionFixture = CreateFixture();
        KLEPMemoryMoment prior = wrongActionFixture.Request.Moments[0];
        consequence = wrongActionFixture.Request.Moments[
            wrongActionFixture.Request.Moments.Count - 1];
        KLEPDesireEffectVector wrongAction = DesireVector(
            prior.MomentId,
            consequence.MomentId,
            KLEPDesireEffectAttribution.ActionOwned,
            "action.someone-else",
            wrongActionFixture.Request.ActionOutcome.RunIndex);
        ExpectThrows<ArgumentException>(
            () => wrongActionFixture.Composition.Process(
                CopyRequestWithDesire(
                    wrongActionFixture.Request,
                    wrongAction)),
            "Cognition rejects ActionOwned Desire evidence for another Executable run");
        Expect(wrongActionFixture.Coordinator.Emotion.Tick == 1 &&
               wrongActionFixture.Coordinator.Memory.CurrentTick == 0 &&
               wrongActionFixture.Coordinator.LastTransition == null,
            "ActionOwned attribution preflight fails before state-producing work");

        Fixture externalFixture = CreateFixture();
        prior = externalFixture.Request.Moments[0];
        consequence = externalFixture.Request.Moments[
            externalFixture.Request.Moments.Count - 1];
        KLEPDesireEffectVector external = DesireVector(
            prior.MomentId,
            consequence.MomentId,
            KLEPDesireEffectAttribution.External);
        KLEPCognitionTransition<EventContext> accepted =
            externalFixture.Composition.Process(
                CopyRequestWithDesire(externalFixture.Request, external));
        Expect(!accepted.Experience.DesireEffects.Effects[0]
                   .IsEligibleForAutomaticExpectationLearning,
            "External Desire change remains valid archived evidence without automatic-learning qualification");
    }

    private static void VerifyInvalidCausalityCannotAdvanceEmotionOrMemory()
    {
        Fixture fixture = CreateFixture();
        var skippedEmotionTick = new KLEPCognitionExperienceRequest<EventContext>(
            "experience.bad-clock",
            memoryTick: 2,
            emotionTick: 3,
            evaluationId: "evaluation.bad-clock",
            evaluationTick: 2,
            causeOrigin: KLEPEmotionInfluenceOrigin.External,
            contextIdentity: fixture.Request.ContextIdentity,
            context: fixture.Request.Context,
            moments: fixture.Request.Moments,
            actionOutcome: fixture.Request.ActionOutcome);

        ExpectThrows<ArgumentException>(
            () => fixture.Composition.Process(skippedEmotionTick),
            "A coordinator rejects a non-consecutive Emotion clock");
        Expect(fixture.Coordinator.Emotion.Tick == 1 &&
               fixture.Coordinator.Memory.CurrentTick == 0 &&
               fixture.Coordinator.LastTransition == null,
            "Rejected clock causality cannot partially advance Emotion or Memory");

        var lateEvaluation = new KLEPCognitionExperienceRequest<EventContext>(
            "experience.bad-evaluation",
            memoryTick: 2,
            emotionTick: 2,
            evaluationId: "evaluation.bad-evaluation",
            evaluationTick: 3,
            causeOrigin: KLEPEmotionInfluenceOrigin.External,
            contextIdentity: fixture.Request.ContextIdentity,
            context: fixture.Request.Context,
            moments: fixture.Request.Moments,
            actionOutcome: fixture.Request.ActionOutcome);
        ExpectThrows<ArgumentException>(
            () => fixture.Composition.Process(lateEvaluation),
            "An Ethics evaluation cannot postdate the Emotion consequence it claims to produce");
        Expect(fixture.Coordinator.Emotion.Tick == 1 &&
               fixture.Coordinator.Memory.CurrentTick == 0,
            "Timeline preflight occurs before either stateful subsystem advances");

        KLEPCognitionTransition<EventContext> recovered =
            fixture.Composition.Process(fixture.Request);
        Expect(recovered.MemorySnapshot.Tick == 2,
            "A rejected request leaves the coordinator able to process the next valid experience");
    }

    private static void VerifyEvidenceAdaptersAreEligibleReadOnlyAndProvenanceBearing()
    {
        Fixture fixture = CreateFixture();
        fixture.Composition.Process(fixture.Request);

        KLEPMemoryObserverEvidenceSource memorySource =
            fixture.Composition.MemoryEvidenceSource;
        KLEPEmotionObserverEvidenceSource emotionSource =
            fixture.Composition.EmotionEvidenceSource;
        KLEPObserver observer = fixture.Composition.Observer;

        ProbeExecutable help = Action("action.help", 1f);
        ProbeExecutable other = Action("action.other", 1f);
        ProbeExecutable blocked = new ProbeExecutable(
            new KLEPExecutableDefinition(
                "action.blocked",
                "Blocked",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "lock.action.blocked",
                        "Blocked",
                        new KLEPKeyPresent("key.never-present"))
                },
                baseAttractiveness: 50f));
        var neuron = new KLEPNeuron("cognition.observer.neuron");
        neuron.InitializeKey(Key("state.prior"));
        neuron.RegisterExecutable(blocked);
        neuron.RegisterExecutable(other);
        neuron.RegisterExecutable(help);
        var agent = new KLEPAgent(
            neuron,
            configuration: null,
            guidanceObserver: observer);

        long memoryTickBefore = fixture.Coordinator.Memory.CurrentTick;
        KLEPMemorySnapshot memorySnapshotBefore =
            fixture.Coordinator.Memory.Snapshot;
        int memoryHistoryBefore =
            fixture.Coordinator.Memory.GetSnapshotHistory().Count;
        long emotionTickBefore = fixture.Coordinator.Emotion.Tick;
        KLEPEmotionVector emotionPositionBefore =
            fixture.Coordinator.Emotion.Position;
        KLEPEmotionVector emotionVelocityBefore =
            fixture.Coordinator.Emotion.Velocity;
        KLEPEmotionSnapshot emotionSnapshotBefore =
            fixture.Coordinator.Emotion.LastSnapshot;
        int emotionHistoryBefore =
            fixture.Coordinator.Emotion.GetSnapshotHistory().Count;

        KLEPAgentTickTrace trace = agent.Tick();

        Expect(trace.WasObserverConsulted &&
               agent.PendingGuidanceAdvice != null &&
               agent.PendingGuidanceAdvice.TargetExecutableId == "action.help",
            "Project Memory and Emotion policies can direct only an already-eligible target");
        Expect(blocked.TickCount == 0 &&
               !HasEvaluation(memorySource, "action.blocked") &&
               !HasEvaluation(emotionSource, "action.blocked") &&
               observer.LastTrace.Targets.Count == 2,
            "A high project value cannot expose or unlock a target whose Locks are closed");
        Expect(memorySource.LastEvaluations.Count == 2 &&
               memorySource.LastEvaluations[0].TargetExecutableId == "action.help" &&
               memorySource.LastEvaluations[0].Recall.Matches.Count == 1 &&
               emotionSource.LastEvaluations.Count == 2 &&
               emotionSource.LastEvaluations[0].State.Tick == emotionTickBefore,
            "Adapters retain structured read-only Memory recall and Emotion-state evidence");

        KLEPObserverTargetTrace helpTrace = FindTarget(
            observer.LastTrace,
            "action.help");
        KLEPObserverTargetTrace otherTrace = FindTarget(
            observer.LastTrace,
            "action.other");
        string supportingClusterId = memorySource.LastEvaluations[0]
            .Recall.Matches[0].Cluster.ClusterId;
        Expect(helpTrace != null && helpTrace.Evidence.Count == 2 &&
               HasSource(helpTrace, "project.emotion-policy") &&
               HasSource(helpTrace, "project.memory-policy") &&
               HasExplanation(helpTrace, "project.remembered-success") &&
               HasExplanation(helpTrace, supportingClusterId),
            "Observer evidence preserves source identity, policy reason, and supporting cluster provenance");
        Expect(otherTrace != null &&
               HasEvidenceValue(
                   otherTrace,
                   "project.emotion-policy",
                   -0.5f),
            "The adapter preserves the project-authored sign instead of assigning a built-in happiness meaning");
        Expect(fixture.Coordinator.Memory.CurrentTick == memoryTickBefore &&
               ReferenceEquals(
                   fixture.Coordinator.Memory.Snapshot,
                   memorySnapshotBefore) &&
               fixture.Coordinator.Memory.GetSnapshotHistory().Count ==
                   memoryHistoryBefore,
            "Memory evidence evaluation cannot cool, record, or otherwise mutate Memory");
        Expect(fixture.Coordinator.Emotion.Tick == emotionTickBefore &&
               fixture.Coordinator.Emotion.Position == emotionPositionBefore &&
               fixture.Coordinator.Emotion.Velocity == emotionVelocityBefore &&
               ReferenceEquals(
                   fixture.Coordinator.Emotion.LastSnapshot,
                   emotionSnapshotBefore) &&
               fixture.Coordinator.Emotion.GetSnapshotHistory().Count ==
                   emotionHistoryBefore,
            "Emotion evidence evaluation cannot advance or alter the emotional body");
    }

    private static void VerifyPostEmotionMemoryFaultRollsBackAndRetriesExactly()
    {
        var faultingEvaluator = new OneShotMemoryAdvancingEthicsEvaluator();
        Fixture fixture = CreateFixture(
            faultingEvaluator,
            faultingEvaluator.AttachMemory);
        fixture.Composition.Process(fixture.Request);
        KLEPCognitionExperienceRequest<EventContext> followup =
            CreateFollowupRequest(fixture);

        KLEPEmotion emotion = fixture.Coordinator.Emotion;
        KLEPMemory memory = fixture.Coordinator.Memory;
        long emotionTickBefore = emotion.Tick;
        KLEPEmotionVector emotionPositionBefore = emotion.Position;
        KLEPEmotionVector emotionVelocityBefore = emotion.Velocity;
        long unchangedBefore = emotion.UnchangedPositionTickCount;
        KLEPEmotionSnapshot lastEmotionBefore = emotion.LastSnapshot;
        IReadOnlyList<KLEPEmotionSnapshot> emotionHistoryBefore =
            emotion.GetSnapshotHistory();
        long memoryTickBefore = memory.CurrentTick;
        KLEPMemorySnapshot memorySnapshotBefore = memory.Snapshot;
        IReadOnlyList<KLEPMemorySnapshot> memoryHistoryBefore =
            memory.GetSnapshotHistory();
        KLEPMemoryState memoryStateBefore = memory.CaptureState();
        KLEPCognitionTransition<EventContext> transitionBefore =
            fixture.Coordinator.LastTransition;

        faultingEvaluator.FailNextByAdvancingMemoryTo(followup.MemoryTick);
        ArgumentOutOfRangeException failure =
            CaptureThrows<ArgumentOutOfRangeException>(
                () => fixture.Composition.Process(followup),
                "A hostile post-Emotion Memory fault reaches the cognition transaction boundary");
        Expect(failure.ParamName == "tick" &&
               faultingEvaluator.InjectedFailureCount == 1,
            "The hostile failure is the coordinator's Memory commit after Ethics returned and Emotion advanced");
        Expect(emotion.Tick == emotionTickBefore &&
               emotion.Position == emotionPositionBefore &&
               emotion.Velocity == emotionVelocityBefore &&
               emotion.UnchangedPositionTickCount == unchangedBefore &&
               ReferenceEquals(emotion.LastSnapshot, lastEmotionBefore) &&
               SameReferences(
                   emotion.GetSnapshotHistory(),
                   emotionHistoryBefore),
            "A post-Emotion Memory fault restores the complete emotional body and bounded snapshot history");
        KLEPMemoryState memoryStateAfter = memory.CaptureState();
        Expect(memory.CurrentTick == memoryTickBefore &&
               ReferenceEquals(memory.Snapshot, memorySnapshotBefore) &&
               SameReferences(
                   memory.GetSnapshotHistory(),
                   memoryHistoryBefore) &&
               EquivalentMemoryContinuation(
                   memoryStateAfter,
                   memoryStateBefore),
            "The same transaction restores Memory Tick, current snapshot, history, clusters, seen IDs, and allocation sequence");
        Expect(ReferenceEquals(
                   fixture.Coordinator.LastTransition,
                   transitionBefore) &&
               ReferenceEquals(fixture.Composition.Memory, memory) &&
               ReferenceEquals(
                   fixture.Composition.MemoryEvidenceSource.Memory,
                   memory) &&
               ReferenceEquals(fixture.Composition.Emotion, emotion) &&
               ReferenceEquals(
                   fixture.Composition.EmotionEvidenceSource.Emotion,
                   emotion),
            "Rollback publishes no transition and preserves the subsystem instances already wired into Observer evidence");

        KLEPCognitionTransition<EventContext> retried =
            fixture.Composition.Process(followup);
        Expect(retried.EmotionSnapshot.Tick == followup.EmotionTick &&
               retried.MemorySnapshot.Tick == followup.MemoryTick &&
               retried.Experience.ExperienceId == followup.ExperienceId &&
               ReferenceEquals(fixture.Coordinator.LastTransition, retried),
            "The identical causal request can retry and commit exactly once after rollback");

        Fixture cleanFixture = CreateFixture();
        cleanFixture.Composition.Process(cleanFixture.Request);
        KLEPCognitionTransition<EventContext> clean =
            cleanFixture.Composition.Process(
                CreateFollowupRequest(cleanFixture));
        Expect(Describe(retried) == Describe(clean),
            "A successful retry has the same transition trace as an unfaulted equivalent run");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        string first = RunEquivalentScenario();
        string second = RunEquivalentScenario();
        Expect(first == second,
            "Equivalent causal inputs and project policies produce identical cognition and evidence traces");
    }

    private static void VerifyReadOnlyGuardsRunWhenProjectPolicyThrows()
    {
        Fixture memoryFixture = CreateFixture();
        memoryFixture.Composition.Process(memoryFixture.Request);
        var memorySource = new KLEPMemoryObserverEvidenceSource(
            memoryFixture.Coordinator.Memory,
            new MutatingThrowingMemoryPolicy(
                memoryFixture.Coordinator.Memory));
        var memoryObserver = new KLEPObserver(
            "observer.cognition.mutating-memory",
            "1",
            new IKLEPObserverEvidenceSource[] { memorySource });

        InvalidOperationException memoryFailure =
            CaptureThrows<InvalidOperationException>(
            () => CreateObserverAgent(
                "cognition.mutating-memory.neuron",
                memoryObserver).Tick(),
            "Memory read-only verification still runs when a mutating project policy throws");
        Expect(memoryFailure.InnerException is ApplicationException,
            "Memory mutation remains the boundary error while preserving the original project-policy failure");
        Expect(memorySource.LastEvaluations.Count == 0,
            "A failed Memory policy evaluation does not publish a partial evidence trace");

        Fixture emotionFixture = CreateFixture();
        emotionFixture.Composition.Process(emotionFixture.Request);
        var emotionSource = new KLEPEmotionObserverEvidenceSource(
            emotionFixture.Coordinator.Emotion,
            new MutatingThrowingEmotionPolicy(
                emotionFixture.Coordinator.Emotion));
        var emotionObserver = new KLEPObserver(
            "observer.cognition.mutating-emotion",
            "1",
            new IKLEPObserverEvidenceSource[] { emotionSource });

        InvalidOperationException emotionFailure =
            CaptureThrows<InvalidOperationException>(
            () => CreateObserverAgent(
                "cognition.mutating-emotion.neuron",
                emotionObserver).Tick(),
            "Emotion read-only verification still runs when a mutating project policy throws");
        Expect(emotionFailure.InnerException is ApplicationException,
            "Emotion mutation remains the boundary error while preserving the original project-policy failure");
        Expect(emotionSource.LastEvaluations.Count == 0,
            "A failed Emotion policy evaluation does not publish a partial evidence trace");
    }

    private static string RunEquivalentScenario()
    {
        Fixture fixture = CreateFixture();
        KLEPCognitionTransition<EventContext> transition =
            fixture.Composition.Process(fixture.Request);
        KLEPObserver observer = fixture.Composition.Observer;
        var neuron = new KLEPNeuron("cognition.deterministic.observer-neuron");
        neuron.InitializeKey(Key("state.prior"));
        neuron.RegisterExecutable(Action("action.other", 1f));
        neuron.RegisterExecutable(Action("action.help", 1f));
        var agent = new KLEPAgent(
            neuron,
            configuration: null,
            guidanceObserver: observer);
        agent.Tick();

        return Describe(transition) + "\n" + Describe(observer.LastTrace);
    }

    private static Fixture CreateFixture(
        IKLEPEthicsEvaluator<EventContext> evaluatorOverride = null,
        Action<KLEPMemory> configureMemory = null)
    {
        var sourceNeuron = new KLEPNeuron("cognition.source.neuron");
        sourceNeuron.InitializeKey(Key("state.prior"));
        sourceNeuron.InitializeKey(Key("state.consequence"));
        KLEPKeySnapshot prior = sourceNeuron.TickViaAgent().KeySnapshot;
        KLEPKeySnapshot consequence = sourceNeuron.TickViaAgent().KeySnapshot;
        var moments = new[]
        {
            KLEPMemoryMoment.Capture(
                "moment.help.prior",
                KLEPMemoryMomentRole.Prior,
                prior),
            KLEPMemoryMoment.Capture(
                "moment.help.consequence",
                KLEPMemoryMomentRole.Consequence,
                consequence)
        };
        var action = new KLEPMemoryActionOutcome(
            "action.help",
            runIndex: 1,
            startedTick: 1,
            completedTick: 2,
            terminalState: KLEPExecutableState.Succeeded,
            exitReason: KLEPExecutableExitReason.Succeeded);
        var emotionConfiguration = new KLEPEmotionConfiguration(
            "Valence",
            "Activation",
            frictionPerTick: 0.1f,
            maximumSpeed: 1f);
        var emotion = new KLEPEmotion(
            emotionConfiguration,
            KLEPEmotionVector.Zero,
            KLEPEmotionVector.Zero,
            initialTick: 1);
        var rule = new KLEPWeightedEthicsRule<EventContext>(
            "project.helped",
            1f,
            context => context.Apply,
            new KLEPEmotionVector(0.4f, 0.2f),
            "project.help-applied",
            evidenceIds: new[] { "observation.help-result" });
        var defaultEvaluator = new KLEPWeightedEthicsEvaluator<EventContext>(
            "project.ethics",
            "1",
            "Valence",
            "Activation",
            KLEPEmotionVector.Zero,
            new IKLEPWeightedEthicsRule<EventContext>[] { rule });
        IKLEPEthicsEvaluator<EventContext> evaluator =
            evaluatorOverride ?? defaultEvaluator;
        var ethics = new KLEPEthics<EventContext>(evaluator);
        var memory = new KLEPMemory(
            "agent.cognition",
            new KLEPMemoryConfiguration(
                axisXName: "Valence",
                axisYName: "Activation",
                initialHeat: 1f,
                repetitionHeat: 0.5f,
                coolingPerTick: 0.1f,
                archiveRepetitionThreshold: 3));
        configureMemory?.Invoke(memory);
        var composition = new KLEPCognitionComposition<EventContext>(
            "observer.cognition",
            "1",
            ethics,
            emotion,
            memory,
            new ProjectMemoryPolicy(),
            new ProjectEmotionPolicy(new Dictionary<string, float>(
                StringComparer.Ordinal)
            {
                { "action.help", 0.25f },
                { "action.other", -0.5f },
                { "action.blocked", 100f }
            }));
        var request = new KLEPCognitionExperienceRequest<EventContext>(
            "experience.help.1",
            memoryTick: 2,
            emotionTick: 2,
            evaluationId: "evaluation.help.1",
            evaluationTick: 2,
            causeOrigin: KLEPEmotionInfluenceOrigin.External,
            contextIdentity: new KLEPEthicsContextIdentity(
                "event.help.1",
                "project.event",
                "1"),
            context: new EventContext("event.help.1", apply: true),
            moments: moments,
            actionOutcome: action);
        return new Fixture(sourceNeuron, composition, request);
    }

    private static KLEPCognitionExperienceRequest<EventContext>
        CreateFollowupRequest(Fixture fixture)
    {
        KLEPMemoryMoment previousConsequence =
            fixture.Request.Moments[fixture.Request.Moments.Count - 1];
        var prior = new KLEPMemoryMoment(
            "moment.help.followup.prior",
            KLEPMemoryMomentRole.Prior,
            previousConsequence.CapturedTick,
            previousConsequence.WaveIndex,
            previousConsequence.Keys);
        KLEPKeySnapshot consequenceSnapshot =
            fixture.SourceNeuron.TickViaAgent().KeySnapshot;
        KLEPMemoryMoment consequence = KLEPMemoryMoment.Capture(
            "moment.help.followup.consequence",
            KLEPMemoryMomentRole.Consequence,
            consequenceSnapshot);
        var action = new KLEPMemoryActionOutcome(
            "action.help",
            runIndex: 2,
            startedTick: previousConsequence.CapturedTick,
            completedTick: consequence.CapturedTick,
            terminalState: KLEPExecutableState.Succeeded,
            exitReason: KLEPExecutableExitReason.Succeeded);
        return new KLEPCognitionExperienceRequest<EventContext>(
            "experience.help.2",
            memoryTick: consequence.CapturedTick,
            emotionTick: fixture.Coordinator.Emotion.Tick + 1,
            evaluationId: "evaluation.help.2",
            evaluationTick: consequence.CapturedTick,
            causeOrigin: KLEPEmotionInfluenceOrigin.External,
            contextIdentity: new KLEPEthicsContextIdentity(
                "event.help.2",
                "project.event",
                "1"),
            context: new EventContext("event.help.2", apply: true),
            moments: new[] { prior, consequence },
            actionOutcome: action);
    }

    private static KLEPCognitionExperienceRequest<EventContext>
        CopyRequestWithDesire(
            KLEPCognitionExperienceRequest<EventContext> source,
            KLEPDesireEffectVector desireEffects)
    {
        return new KLEPCognitionExperienceRequest<EventContext>(
            source.ExperienceId,
            source.MemoryTick,
            source.EmotionTick,
            source.EvaluationId,
            source.EvaluationTick,
            source.CauseOrigin,
            source.ContextIdentity,
            source.Context,
            source.Moments,
            source.ActionOutcome,
            desireEffects);
    }

    private static KLEPDesireEffectVector DesireVector(
        string priorMomentId,
        string consequenceMomentId,
        KLEPDesireEffectAttribution attributionKind,
        string actionStableId = null,
        long? actionRunIndex = null,
        float satisfactionBefore = 0f,
        float satisfactionAfter = 1f,
        float pressureBefore = 1f,
        float pressureAfter = 1f)
    {
        var system = new KLEPDesireSystem<DesireContext>(
            "desire.cognition-owner",
            new[]
            {
                new KLEPDesireDefinition<DesireContext>(
                    "desire.help",
                    "1",
                    3f,
                    new DesireEvaluator())
            });
        var identity = new KLEPDesireContextIdentity(
            "desire.cognition-context",
            "fixture.cognition-desire",
            "1");
        KLEPDesireSnapshot prior = system.Observe(
            new KLEPDesireObservationRequest<DesireContext>(
                "desire.cognition.prior",
                100,
                priorMomentId,
                identity,
                new DesireContext(
                    satisfactionBefore,
                    pressureBefore,
                    "Before helping.")));
        KLEPDesireSnapshot consequence = system.Observe(
            new KLEPDesireObservationRequest<DesireContext>(
                "desire.cognition.consequence",
                101,
                consequenceMomentId,
                identity,
                new DesireContext(
                    satisfactionAfter,
                    pressureAfter,
                    "After helping.")));
        var attribution = new KLEPDesireAttributionEvidence(
            attributionKind,
            "fixture.cognition-desire-attribution",
            actionStableId,
            actionRunIndex,
            new[] { "fixture.cognition-desire-cause" });
        return system.EvaluateTransition(new KLEPDesireTransitionRequest(
            "desire.cognition-transition",
            prior,
            consequence,
            attribution));
    }

    private static ProbeExecutable Action(string stableId, float score)
    {
        return new ProbeExecutable(new KLEPExecutableDefinition(
            stableId,
            stableId,
            KLEPExecutableKind.Action,
            baseAttractiveness: score));
    }

    private static KLEPAgent CreateObserverAgent(
        string neuronId,
        KLEPObserver observer)
    {
        var neuron = new KLEPNeuron(neuronId);
        neuron.InitializeKey(Key("state.prior"));
        neuron.RegisterExecutable(Action("action.other", 1f));
        neuron.RegisterExecutable(Action("action.help", 1f));
        return new KLEPAgent(
            neuron,
            configuration: null,
            guidanceObserver: observer);
    }

    private static KLEPKeyDefinition Key(string stableId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            stableId,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static KLEPObserverTargetTrace FindTarget(
        KLEPObserverTrace trace,
        string stableId)
    {
        for (int i = 0; i < trace.Targets.Count; i++)
        {
            if (trace.Targets[i].ExecutableStableId == stableId)
            {
                return trace.Targets[i];
            }
        }

        return null;
    }

    private static bool HasSource(KLEPObserverTargetTrace target, string sourceId)
    {
        for (int i = 0; i < target.Evidence.Count; i++)
        {
            if (target.Evidence[i].SourceId == sourceId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSource(
        KLEPObserver observer,
        IKLEPObserverEvidenceSource source)
    {
        for (int i = 0; i < observer.EvidenceSources.Count; i++)
        {
            if (ReferenceEquals(observer.EvidenceSources[i], source))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEvidenceValue(
        KLEPObserverTargetTrace target,
        string sourceId,
        float value)
    {
        for (int i = 0; i < target.Evidence.Count; i++)
        {
            KLEPObserverEvidenceTrace evidence = target.Evidence[i];
            if (evidence.SourceId == sourceId && evidence.Value == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExplanation(
        KLEPObserverTargetTrace target,
        string value)
    {
        for (int i = 0; i < target.Evidence.Count; i++)
        {
            if (target.Evidence[i].Explanation.Contains(
                    value,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEvaluation(
        KLEPMemoryObserverEvidenceSource source,
        string targetId)
    {
        for (int i = 0; i < source.LastEvaluations.Count; i++)
        {
            if (source.LastEvaluations[i].TargetExecutableId == targetId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEvaluation(
        KLEPEmotionObserverEvidenceSource source,
        string targetId)
    {
        for (int i = 0; i < source.LastEvaluations.Count; i++)
        {
            if (source.LastEvaluations[i].TargetExecutableId == targetId)
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(KLEPCognitionTransition<EventContext> transition)
    {
        var text = new StringBuilder();
        for (int i = 0; i < transition.Steps.Count; i++)
        {
            KLEPCognitionStepTrace step = transition.Steps[i];
            text.Append(step.Phase).Append(':')
                .Append(step.Tick).Append(':')
                .Append(step.ProvenanceId).Append('|');
        }

        text.Append(transition.EthicsEvaluation.EvaluatorId).Append(':')
            .Append(transition.EthicsEvaluation.EvaluatorVersion).Append(':')
            .Append(Format(transition.EthicsEvaluation.Judgment.Impulse.X))
            .Append(',')
            .Append(Format(transition.EthicsEvaluation.Judgment.Impulse.Y))
            .Append('|')
            .Append(Format(transition.EmotionSnapshot.Position.X)).Append(',')
            .Append(Format(transition.EmotionSnapshot.Position.Y)).Append('|')
            .Append(transition.Experience.CanonicalGistId).Append('|');
        for (int i = 0; i < transition.MemorySnapshot.Clusters.Count; i++)
        {
            KLEPMemoryClusterSnapshot cluster =
                transition.MemorySnapshot.Clusters[i];
            text.Append(cluster.ClusterId).Append(':')
                .Append(cluster.ActionStableId).Append(':')
                .Append(cluster.EncounterCount).Append(':')
                .Append(Format(cluster.Heat)).Append('|');
        }

        return text.ToString();
    }

    private static string Describe(KLEPObserverTrace trace)
    {
        var text = new StringBuilder();
        text.Append(trace.SelectedExecutableId ?? "<none>").Append('|')
            .Append(trace.AbstentionReason).Append('|');
        for (int i = 0; i < trace.Targets.Count; i++)
        {
            KLEPObserverTargetTrace target = trace.Targets[i];
            text.Append(target.ExecutableStableId).Append(':')
                .Append(target.HolisticValue.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
            for (int j = 0; j < target.Evidence.Count; j++)
            {
                KLEPObserverEvidenceTrace evidence = target.Evidence[j];
                text.Append(':').Append(evidence.SourceId).Append('@')
                    .Append(evidence.SourceVersion).Append('=')
                    .Append(Format(evidence.Value)).Append('[')
                    .Append(evidence.Explanation).Append(']');
            }

            text.Append('|');
        }

        return text.ToString();
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool SameReferences<T>(
        IReadOnlyList<T> left,
        IReadOnlyList<T> right)
        where T : class
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EquivalentMemoryContinuation(
        KLEPMemoryState left,
        KLEPMemoryState right)
    {
        if (left.SchemaVersion != right.SchemaVersion ||
            left.OwnerId != right.OwnerId ||
            left.Tick != right.Tick ||
            left.NextClusterSequence != right.NextClusterSequence ||
            !ReferenceEquals(left.Configuration, right.Configuration) ||
            !SameReferences(left.Clusters, right.Clusters) ||
            !SameReferences(left.LastTransitions, right.LastTransitions) ||
            left.SeenExperienceIds.Count != right.SeenExperienceIds.Count)
        {
            return false;
        }

        for (int i = 0; i < left.SeenExperienceIds.Count; i++)
        {
            if (!StringComparer.Ordinal.Equals(
                    left.SeenExperienceIds[i],
                    right.SeenExperienceIds[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    private static void ExpectThrows<TException>(Action action, string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException("Assertion failed: " + message);
    }

    private static TException CaptureThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        assertions++;
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Assertion failed: " + message);
    }

    private sealed class EventContext
    {
        public EventContext(string eventId, bool apply)
        {
            EventId = eventId;
            Apply = apply;
        }

        public string EventId { get; }
        public bool Apply { get; }
    }

    private sealed class Fixture
    {
        public Fixture(
            KLEPNeuron sourceNeuron,
            KLEPCognitionComposition<EventContext> composition,
            KLEPCognitionExperienceRequest<EventContext> request)
        {
            SourceNeuron = sourceNeuron;
            Composition = composition;
            Request = request;
        }

        public KLEPNeuron SourceNeuron { get; }
        public KLEPCognitionComposition<EventContext> Composition { get; }
        public KLEPCognitionCoordinator<EventContext> Coordinator =>
            Composition.Coordinator;
        public KLEPCognitionExperienceRequest<EventContext> Request { get; }
    }

    private sealed class ProjectMemoryPolicy :
        IKLEPMemoryObserverEvidencePolicy
    {
        public string StableId => "project.memory-policy";
        public string Version => "1";

        public KLEPMemoryCue CreateCue(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget)
        {
            return KLEPMemoryCue.Capture(
                context.KeySnapshot,
                eligibleTarget.StableId);
        }

        public KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPMemoryRecallResult recall)
        {
            if (recall.Matches.Count == 0)
            {
                return null;
            }

            KLEPMemoryRecall match = recall.Matches[0];
            float value = match.Cluster.SucceededCount >
                match.Cluster.FailedCount
                    ? match.RecallStrength
                    : -match.RecallStrength;
            return new KLEPCognitionEvidenceContribution(
                value,
                "project.remembered-success",
                "Project policy compared factual terminal outcomes.",
                new[] { match.Cluster.ClusterId });
        }
    }

    private sealed class OneShotMemoryAdvancingEthicsEvaluator :
        IKLEPEthicsEvaluator<EventContext>
    {
        private KLEPMemory memory;
        private long faultTick;
        private bool failNext;

        public string EvaluatorId => "project.ethics";
        public string EvaluatorVersion => "1";
        public string ExpectedAxisXName => "Valence";
        public string ExpectedAxisYName => "Activation";
        public int InjectedFailureCount { get; private set; }

        public void AttachMemory(KLEPMemory value)
        {
            memory = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void FailNextByAdvancingMemoryTo(long tick)
        {
            if (memory == null)
            {
                throw new InvalidOperationException(
                    "The hostile evaluator requires its fixture Memory.");
            }

            faultTick = tick;
            failNext = true;
        }

        public KLEPEthicsJudgment Evaluate(
            EventContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration)
        {
            var judgment = new KLEPEthicsJudgment(new[]
            {
                new KLEPEthicsTraceEntry(
                    "bias",
                    applied: true,
                    weight: 1f,
                    proposedImpulse: KLEPEmotionVector.Zero,
                    reasonCode: "weighted.bias"),
                new KLEPEthicsTraceEntry(
                    "rule:project.helped",
                    context.Apply,
                    weight: 1f,
                    proposedImpulse: new KLEPEmotionVector(0.4f, 0.2f),
                    reasonCode: "project.help-applied",
                    evidenceIds: new[] { "observation.help-result" })
            });

            if (failNext)
            {
                failNext = false;
                InjectedFailureCount++;
                memory.Tick(faultTick);
            }

            return judgment;
        }
    }

    private sealed class ProjectEmotionPolicy :
        IKLEPEmotionObserverEvidencePolicy
    {
        private readonly Dictionary<string, float> authoredValues;

        public ProjectEmotionPolicy(Dictionary<string, float> authoredValues)
        {
            this.authoredValues = authoredValues;
        }

        public string StableId => "project.emotion-policy";
        public string Version => "1";

        public KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPEmotionObserverEvidenceState emotionState)
        {
            if (!authoredValues.TryGetValue(
                    eligibleTarget.StableId,
                    out float value))
            {
                return null;
            }

            return new KLEPCognitionEvidenceContribution(
                value,
                "project.authored-emotion-policy",
                $"Project interpreted {emotionState.AxisXName}=" +
                Format(emotionState.Position.X),
                new[] { "emotion-tick:" + emotionState.Tick });
        }
    }

    private sealed class MutatingThrowingMemoryPolicy :
        IKLEPMemoryObserverEvidencePolicy
    {
        private readonly KLEPMemory memory;

        public MutatingThrowingMemoryPolicy(KLEPMemory memory)
        {
            this.memory = memory;
        }

        public string StableId => "test.mutating-memory-policy";
        public string Version => "1";

        public KLEPMemoryCue CreateCue(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget)
        {
            return KLEPMemoryCue.Capture(
                context.KeySnapshot,
                eligibleTarget.StableId);
        }

        public KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPMemoryRecallResult recall)
        {
            memory.Tick(memory.CurrentTick + 1);
            throw new ApplicationException("Project policy failure.");
        }
    }

    private sealed class MutatingThrowingEmotionPolicy :
        IKLEPEmotionObserverEvidencePolicy
    {
        private readonly KLEPEmotion emotion;

        public MutatingThrowingEmotionPolicy(KLEPEmotion emotion)
        {
            this.emotion = emotion;
        }

        public string StableId => "test.mutating-emotion-policy";
        public string Version => "1";

        public KLEPCognitionEvidenceContribution Evaluate(
            KLEPObserverEvidenceContext context,
            KLEPExecutableDefinition eligibleTarget,
            KLEPEmotionObserverEvidenceState emotionState)
        {
            emotion.Advance(emotion.Tick + 1);
            throw new ApplicationException("Project policy failure.");
        }
    }

    private sealed class DesireContext
    {
        internal DesireContext(
            float satisfaction,
            float pressure,
            string explanation)
        {
            Satisfaction = satisfaction;
            Pressure = pressure;
            Explanation = explanation;
        }

        internal float Satisfaction { get; }
        internal float Pressure { get; }
        internal string Explanation { get; }
    }

    private sealed class DesireEvaluator : IKLEPDesireEvaluator<DesireContext>
    {
        public string EvaluatorId => "fixture.cognition-desire-evaluator";
        public string EvaluatorVersion => "1";

        public KLEPDesireAssessment Evaluate(
            DesireContext context,
            long desireTick)
        {
            return new KLEPDesireAssessment(
                context.Satisfaction,
                context.Pressure,
                context.Explanation,
                new[]
                {
                    desireTick == 100
                        ? "fixture.desire.before"
                        : "fixture.desire.after"
                });
        }
    }

    private sealed class ProbeExecutable : KLEPExecutableBase
    {
        public ProbeExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }

        public int TickCount { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
