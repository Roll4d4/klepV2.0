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
        VerifyEquivalentRunsAreDeterministic();
        VerifyFaultIsObservedAndRethrownUnchanged();

        Console.WriteLine($"KLEP Agent smoke passed: {assertions} assertions.");
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
        runningNeuron.Tick();
        Exception attachmentFault = Catch(() => new KLEPAgent(runningNeuron));
        Expect(attachmentFault is InvalidOperationException,
            "Agent refuses to invent a start state for a Solo already running");

        var patientNeuron = new KLEPNeuron("agent.attachment.patient");
        patientNeuron.Tick();
        var attached = new KLEPAgent(patientNeuron);
        Expect(attached.Tick().DidCompleteObservation,
            "Agent may attach after earlier patient history when no run is active");

        var guardedNeuron = new KLEPNeuron("agent.continuity.neuron");
        var guarded = new KLEPAgent(guardedNeuron);
        guarded.Tick();
        guardedNeuron.Tick();
        long externallyAdvancedCycle = guardedNeuron.CycleIndex;
        Exception continuityFault = Catch(() => guarded.Tick());
        Expect(continuityFault is InvalidOperationException &&
               guardedNeuron.CycleIndex == externallyAdvancedCycle,
            "Agent detects an outside Neuron Tick before causing another mutation");
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
        KLEPKeySnapshot firstSnapshot = firstNeuron.Tick().KeySnapshot;

        var secondNeuron = new KLEPNeuron("agent.signature.second");
        secondNeuron.InitializeKey(
            alpha,
            Payload("value", -999),
            sourceId: "second.alpha");
        secondNeuron.InitializeKey(
            beta,
            Payload("value", 777),
            sourceId: "second.beta");
        KLEPKeySnapshot secondSnapshot = secondNeuron.Tick().KeySnapshot;

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
                novelNeuron.Tick().KeySnapshot);
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
            neuron.Tick().KeySnapshot);
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
            neuron.Tick().KeySnapshot);
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

            if (emitDefinition != null && TickCount == emitOnTick)
            {
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
}
