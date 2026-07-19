using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyDefinitionIsImmutable();
        VerifyGoalInheritance();
        VerifyBothLockGroups();
        VerifyEligibilityBeforeScoring();
        VerifyDeterministicTieAndPatientState();
        VerifyStableIdCollisionIsRejected();
        VerifyHealthRemovalUnlocksDeathBehavior();
        VerifyRepeatability();
        VerifyInitializationOutputPrecedesEligibility();
        VerifyRunningLifecycleAndTerminalTeardown();
        VerifyFailureDoesNotFallBackInSameTick();
        VerifyFailedTickDiscardsBufferedOutputs();
        VerifyCurrentSoloTieAndStrictInterruption();
        VerifyPatientStateWithTandemWork();
        VerifyActiveRemovalCleansUpOnce();
        VerifyTickFaultIsTracedCleanedAndRethrown();
        VerifyTandemCascadeSettlesBeforeSolo();
        VerifyProcessedRunningTandemWaitsUntilNextTickForLockCancellation();
        VerifyDeclaredOutputOmissionFaultsBeforePublication();
        VerifyUndeclaredOutputFaults();
        VerifyWavePeersShareSnapshot();
        VerifySoloOutputIsDeferred();
        VerifyGoalAllAndOneLayerPerTick();
        VerifyGoalAllPreservesSuccessAndRetriesFailure();
        VerifyRuntimeSnapshotsFreezeGoalProgress();
        VerifyGoalAnyRequiresActualSuccess();
        VerifyGoalAnyUsesAuthoredSerialOrder();
        VerifyGoalNoneSkipsChildren();
        VerifyGoalOwnScoreAndExclusiveOwnership();
        VerifyGoalIntrinsicAttractionUsesFinalTandemSnapshot();
        VerifyGoalIntrinsicAttractionGuardsAndFaultTrace();
        VerifyTandemGoalIsUnscoredAutomaticComposite();
        VerifyGoalActivationEmitsOncePerRun();
        VerifyGoalCanBeginAnotherRegistrationTenure();
        VerifyFailedGoalConstructionDoesNotClaimEarlierChildren();
        VerifyGoalOwnershipRaceIsRejectedAtCommit();
        VerifySoloOutputFaultCannotOrphanRunningState();
        VerifyCrossScopeOutputCannotPoisonNeuron();
        VerifyFaultedOutputBatchRollsBackAllStores();
        VerifyTandemPeerUnwindsWhenWaveFaults();
        VerifyInitializationCallbackFaultRejectsWholeRegistrationBoundary();
        VerifyInitializationOutputFaultRejectsWholeRegistrationBoundary();
        VerifyInitializationOutputFailureRejectsRegistration();
        VerifyGoalChildFaultKeepsChildIdentity();
        VerifyGoalCancellationContinuesAfterChildFault();

        Console.WriteLine($"KLEP Executable smoke passed: {assertions} assertions.");
    }

    private static void VerifyDefinitionIsImmutable()
    {
        var source = new List<KLEPLock>
        {
            MakeLock("lock.a", new KLEPKeyPresent("key.a"), 2f)
        };
        var definition = new KLEPExecutableDefinition(
            "exe.test",
            "Test",
            KLEPExecutableKind.Action,
            source,
            baseAttractiveness: 3f);

        source.Add(MakeLock("lock.b", new KLEPKeyPresent("key.b")));
        Expect(definition.ValidationLocks.Count == 1,
            "Definition defensively copies authored Validation Locks");
        Expect(definition.ExecutionLocks.Count == 0,
            "A missing Lock group is an immutable empty group");
        KLEPExecutableScoreEvaluation score =
            definition.EvaluateScore(MakeSnapshot());
        Expect(score.Total == 5f && score.Components.Count == 2,
            "Score is the explicit base plus inspectable authored Lock contributions");
        Expect(score.Components[1].Kind ==
               KLEPExecutableScoreComponentKind.ValidationLock &&
               score.Components[1].SourceId == "lock.a",
            "Score trace identifies its exact Lock source");
        Expect(Serialize(score) == Serialize(definition.EvaluateScore(MakeSnapshot())),
            "Immutable score evaluation has repeatable contents");

        var compositeScore = new KLEPExecutableDefinition(
            "exe.components",
            "Components",
            KLEPExecutableKind.Action,
            new[]
            {
                MakeLock("lock.v-positive", new KLEPAll(), 2f),
                MakeLock("lock.v-negative", new KLEPAll(), -1f)
            },
            new[]
            {
                MakeLock("lock.e-small", new KLEPAll(), 0.5f),
                MakeLock("lock.e-large", new KLEPAll(), 3f)
            },
            baseAttractiveness: 0.5f).EvaluateScore(MakeSnapshot());
        Expect(compositeScore.Total == 5f && compositeScore.Components.Count == 5,
            "Base and multiple positive/negative Lock contributions total deterministically");
        Expect(compositeScore.Components[1].Kind ==
               KLEPExecutableScoreComponentKind.ValidationLock &&
               compositeScore.Components[3].Kind ==
               KLEPExecutableScoreComponentKind.ExecutionLock,
            "Score components retain Base, Validation, then Execution order");
        ExpectThrows<ArgumentException>(() => new KLEPExecutableDefinition(
            " ", "Bad", KLEPExecutableKind.Action),
            "Blank Executable stable IDs are rejected");
        ExpectThrows<ArgumentException>(() => new KLEPExecutableDefinition(
            "exe.bad", "Bad", KLEPExecutableKind.Action,
            new KLEPLock[] { null }),
            "Null Locks are rejected at the definition boundary");
        ExpectThrows<ArgumentOutOfRangeException>(() => new KLEPExecutableDefinition(
            "exe.nan", "Bad Score", KLEPExecutableKind.Action,
            baseAttractiveness: float.NaN),
            "Non-finite authored scores are rejected");
        ExpectThrows<ArgumentOutOfRangeException>(() => new KLEPExecutableDefinition(
            "exe.overflow",
            "Overflow",
            KLEPExecutableKind.Action,
            new[] { MakeLock("lock.max", new KLEPAll(), float.MaxValue) },
            baseAttractiveness: float.MaxValue),
            "A finite component set that overflows Single is rejected");
    }

    private static void VerifyGoalInheritance()
    {
        var goal = new TestGoal(new KLEPExecutableDefinition(
            "goal.survive",
            "Survive",
            KLEPExecutableKind.Goal,
            baseAttractiveness: 1f));

        Expect(goal is KLEPExecutableBase,
            "KLEPGoal is assignable to KLEPExecutableBase");
        Expect(goal.StableId == "goal.survive",
            "Goal retains its base Executable stable identity");
        var neuron = new KLEPNeuron("neuron.goal-shape");
        neuron.RegisterExecutable(goal);
        Expect(neuron.TickViaAgent().SelectedExecutableId == "goal.survive",
            "Neuron accepts a Goal through the inherited Executable base contract");
        ExpectThrows<ArgumentException>(() => new TestGoal(
            new KLEPExecutableDefinition(
                "action.not-a-goal", "Wrong", KLEPExecutableKind.Action)),
            "Goal rejects a non-Goal definition");
    }

    private static void VerifyBothLockGroups()
    {
        KLEPKeySnapshot snapshot = MakeSnapshot("key.a");
        var executable = new TestExecutable(
            new KLEPExecutableDefinition(
                "exe.gated",
                "Gated",
                KLEPExecutableKind.Action,
                new[] { MakeLock("lock.validate-a", new KLEPKeyPresent("key.a")) },
                new[] { MakeLock("lock.execute-b", new KLEPKeyPresent("key.b")) },
                baseAttractiveness: 10f));

        KLEPEligibility eligibility = executable.EvaluateEligibility(snapshot);
        KLEPExecutableEvaluation detail = eligibility.ExecutableEvaluation;
        Expect(!eligibility.IsEligible,
            "An open Validation group cannot bypass a blocked Execution group");
        Expect(detail.Validation.IsSatisfied,
            "Validation group is reported independently");
        Expect(!detail.Execution.IsSatisfied,
            "Execution group is reported independently");
        Expect(detail.Execution.BlockedLockIds.Count == 1 &&
               detail.Execution.BlockedLockIds[0] == "lock.execute-b",
            "Blocked trace identifies the exact Execution Lock");

        string first = Serialize(detail);
        for (int run = 0; run < 100; run++)
        {
            Expect(Serialize(executable.EvaluateLocks(snapshot)) == first,
                $"Pure Lock-gate evaluation repeat {run}");
        }
        Expect(snapshot.Facts.Count == 1 && snapshot.Contains("key.a"),
            "Repeated eligibility evaluation does not mutate the snapshot");
    }

    private static void VerifyEligibilityBeforeScoring()
    {
        var neuron = new KLEPNeuron("neuron.order");
        neuron.RegisterExecutable(MakeExecutable("exe.c", 1000f,
            executionLocks: new[]
            {
                MakeLock("lock.missing", new KLEPKeyPresent("key.missing"))
            }));
        neuron.RegisterExecutable(MakeExecutable("exe.a", 2f));
        neuron.RegisterExecutable(MakeExecutable("exe.b", 3f));

        Expect(neuron.LastDecisionViaAgent().Candidates.Count == 0,
            "Registration is staged until the next Tick");
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(trace.Candidates.Count == 3 &&
               trace.Candidates[0].StableId == "exe.a" &&
               trace.Candidates[1].StableId == "exe.b" &&
               trace.Candidates[2].StableId == "exe.c",
            "Every candidate is evaluated in stable-ID order");
        Expect(trace.SelectedExecutableId == "exe.b",
            "Highest eligible score wins");
        Expect(trace.Candidates[2].ScoreEvaluation == null,
            "An ineligible candidate's enormous authored score is never evaluated");
        Expect(trace.Candidates[0].ScoreEvaluation.Components.Count == 1,
            "Eligible score trace contains the authored base component");
    }

    private static void VerifyDeterministicTieAndPatientState()
    {
        var tieNeuron = new KLEPNeuron("neuron.tie");
        tieNeuron.RegisterExecutable(MakeExecutable("exe.z", 5f));
        tieNeuron.RegisterExecutable(MakeExecutable("exe.a", 5f));
        KLEPDecisionTrace tie = tieNeuron.TickViaAgent();
        Expect(tie.SelectedExecutableId == "exe.a",
            "Ordinal-lowest stable ID wins an equal-score tie");

        var patientNeuron = new KLEPNeuron("neuron.patient");
        patientNeuron.RegisterExecutable(MakeExecutable("exe.equal", 5f));
        KLEPDecisionTrace patient = patientNeuron.TickViaAgent(5f);
        Expect(patient.IsPatient && patient.SelectedExecutableId == null,
            "A score equal to the strict threshold remains patient");
        ExpectThrows<ArgumentOutOfRangeException>(() =>
            patientNeuron.TickViaAgent(float.PositiveInfinity),
            "Non-finite certainty thresholds are rejected");
    }

    private static void VerifyStableIdCollisionIsRejected()
    {
        var neuron = new KLEPNeuron("neuron.ids");
        TestExecutable first = MakeExecutable("exe.same", 1f);
        neuron.RegisterExecutable(first);
        neuron.RegisterExecutable(first);
        ExpectThrows<InvalidOperationException>(() =>
            neuron.RegisterExecutable(MakeExecutable("exe.same", 2f)),
            "A staged Executable cannot be silently replaced by the same stable ID");

        neuron.TickViaAgent();
        TestExecutable replacement = MakeExecutable("exe.same", 2f);
        ExpectThrows<InvalidOperationException>(() =>
            neuron.RegisterExecutable(replacement),
            "A registered Executable cannot be silently replaced by the same stable ID");

        neuron.RemoveExecutable("exe.same");
        neuron.RegisterExecutable(replacement);
        Expect(neuron.TickViaAgent().Candidates[0].Score == 2f,
            "An explicit staged removal permits deterministic same-boundary replacement");
    }

    private static void VerifyHealthRemovalUnlocksDeathBehavior()
    {
        var health = MakeKey("key.health");
        var neuron = new KLEPNeuron("neuron.health");
        KLEPKeyFact healthFact = neuron.InitializeKey(health);
        var death = new TestExecutable(
            new KLEPExecutableDefinition(
                "exe.death",
                "Run Death Animation",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    MakeLock(
                        "lock.health-absent",
                        new KLEPNot(new KLEPKeyPresent("key.health")))
                },
                baseAttractiveness: 1f));
        neuron.RegisterExecutable(death);

        KLEPDecisionTrace alive = neuron.TickViaAgent();
        Expect(alive.IsPatient && !alive.Candidates[0].IsEligible,
            "Death behavior is blocked while Health exists");
        Expect(alive.Candidates[0].Eligibility.ExecutableEvaluation != null,
            "Neuron trace retains the full Executable gate evaluation");

        neuron.RemoveKey(healthFact);
        KLEPDecisionTrace dead = neuron.TickViaAgent();
        Expect(dead.SelectedExecutableId == "exe.death",
            "Death behavior becomes eligible after exact Health removal commits");
    }

    private static void VerifyRepeatability()
    {
        string expected = RunRepeatableDecision(reverseRegistration: false);
        for (int run = 0; run < 100; run++)
        {
            bool reverse = (run & 1) == 1;
            Expect(RunRepeatableDecision(reverse) == expected,
                $"Deterministic decision trace repeat {run}");
        }
    }

    private static void VerifyInitializationOutputPrecedesEligibility()
    {
        KLEPKeyDefinition ready = MakeKey("key.init-ready");
        var probe = MakeProbe(
            "solo.init-output",
            2f,
            executionLocks: new[]
            {
                MakeLock("lock.init-ready", new KLEPKeyPresent(ready.Id.Value))
            },
            declaredOutputs: new[] { ready });
        probe.InitializeAction = context => context.Add(ready);
        probe.TickAction = context =>
        {
            context.Add(ready);
            return KLEPExecutableTickStatus.Succeeded;
        };

        var neuron = new KLEPNeuron("neuron.init-output");
        neuron.RegisterExecutable(probe);
        KLEPDecisionTrace first = neuron.TickViaAgent();

        Expect(probe.InitializeCount == 1 && probe.TickCount == 1,
            "Initialization runs once and its Executable advances in the registration Tick");
        Expect(!first.InitialKeySnapshot.Contains(ready.Id) &&
               first.KeySnapshot.Contains(ready.Id),
            "Initialization output publishes before the first eligibility pass");
        Expect(probe.TickSnapshots.Count == 1 &&
               probe.TickSnapshots[0].Contains(ready.Id),
            "The first Tick callback receives the snapshot containing initialization output");
        Expect(first.Candidates.Count == 1 && first.Candidates[0].IsEligible,
            "Initialization output can satisfy the Executable's own first eligibility check");

        neuron.TickViaAgent();
        Expect(probe.InitializeCount == 1,
            "A registered Executable is never initialized again on a later run");
    }

    private static void VerifyRunningLifecycleAndTerminalTeardown()
    {
        ProbeExecutable probe = MakeProbe("solo.lifecycle", 2f);
        probe.EnqueueStatuses(
            KLEPExecutableTickStatus.Running,
            KLEPExecutableTickStatus.Running,
            KLEPExecutableTickStatus.Succeeded);

        var neuron = new KLEPNeuron("neuron.lifecycle");
        neuron.RegisterExecutable(probe);
        KLEPDecisionTrace first = neuron.TickViaAgent();
        Expect(probe.InitializeCount == 1 && probe.EnterCount == 1 &&
               probe.TickCount == 1 && probe.ExitCount == 0 &&
               probe.CleanupCount == 0,
            "A new Running run initializes, enters, and ticks without teardown");
        Expect(first.CurrentSoloExecutableId == probe.StableId,
            "A Running Solo remains current after its first Tick");

        neuron.TickViaAgent();
        Expect(probe.EnterCount == 1 && probe.TickCount == 2,
            "A Running Executable ticks again without re-entering");

        KLEPDecisionTrace terminal = neuron.TickViaAgent();
        Expect(probe.EnterCount == 1 && probe.TickCount == 3 &&
               probe.ExitCount == 1 && probe.CleanupCount == 1,
            "Succeeded performs Exit and Cleanup exactly once");
        Expect(string.Join(",", probe.Events) ==
               "initialize,enter,tick,tick,tick,exit,cleanup",
            "Running lifecycle order is Initialize, one Enter, repeated Tick, Exit, then Cleanup");
        Expect(probe.ExitContexts.Count == 1 &&
               probe.ExitContexts[0].TerminalState == KLEPExecutableState.Succeeded &&
               probe.ExitContexts[0].Reason == KLEPExecutableExitReason.Succeeded,
            "Terminal callbacks receive the exact success reason");
        KLEPExecutableStepTrace step = FindStep(
            terminal, probe.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Succeeded,
            "Success remains observable in the decision trace for its Tick");
    }

    private static void VerifyFailureDoesNotFallBackInSameTick()
    {
        ProbeExecutable winner = MakeProbe("solo.failure-winner", 10f);
        winner.DefaultStatus = KLEPExecutableTickStatus.Failed;
        ProbeExecutable fallback = MakeProbe("solo.failure-fallback", 5f);

        var neuron = new KLEPNeuron("neuron.failure-no-fallback");
        neuron.RegisterExecutable(fallback);
        neuron.RegisterExecutable(winner);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(winner.TickCount == 1 && winner.ExitCount == 1 &&
               winner.CleanupCount == 1,
            "A failed winner tears down exactly once");
        Expect(fallback.TickCount == 0,
            "A lower-ranked Solo does not run as same-Tick failure fallback");
        KLEPExecutableStepTrace failed = FindStep(
            trace, winner.StableId, KLEPExecutableStepKind.Solo);
        Expect(failed != null && failed.State == KLEPExecutableState.Failed &&
               failed.ExitReason == KLEPExecutableExitReason.Failed,
            "The selected failure remains explicit in the current trace");
    }

    private static void VerifyFailedTickDiscardsBufferedOutputs()
    {
        KLEPKeyDefinition discarded = MakeKey("key.failed-output");
        ProbeExecutable failing = MakeProbe(
            "solo.failed-output",
            2f,
            declaredOutputs: new[] { discarded });
        failing.TickAction = context =>
        {
            context.Add(discarded);
            return KLEPExecutableTickStatus.Failed;
        };
        var neuron = new KLEPNeuron("neuron.failed-output");
        neuron.RegisterExecutable(failing);

        KLEPDecisionTrace failed = neuron.TickViaAgent();
        KLEPExecutableStepTrace step = FindStep(
            failed, failing.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Failed &&
               step.Outputs.Count == 0 &&
               !failed.KeySnapshot.Contains(discarded.Id),
            "A failed Tick discards its buffered output before staging");

        neuron.RemoveExecutable(failing.StableId);
        KLEPDecisionTrace following = neuron.TickViaAgent();
        Expect(!following.InitialKeySnapshot.Contains(discarded.Id) &&
               !following.KeySnapshot.Contains(discarded.Id),
            "Discarded failure output cannot become visible at the following top-level boundary");
    }

    private static void VerifyCurrentSoloTieAndStrictInterruption()
    {
        ProbeExecutable current = MakeProbe("solo.z-current", 5f);
        current.DefaultStatus = KLEPExecutableTickStatus.Running;
        var neuron = new KLEPNeuron("neuron.switching");
        neuron.RegisterExecutable(current);
        neuron.TickViaAgent();

        ProbeExecutable equal = MakeProbe("solo.a-equal", 5f);
        equal.DefaultStatus = KLEPExecutableTickStatus.Running;
        neuron.RegisterExecutable(equal);
        KLEPDecisionTrace tie = neuron.TickViaAgent();
        Expect(current.TickCount == 2 && current.EnterCount == 1 &&
               equal.TickCount == 0,
            "An equal-score challenger does not interrupt or re-enter the current Solo");
        Expect(tie.SelectedExecutableId == current.StableId &&
               tie.CurrentSoloExecutableId == current.StableId,
            "A running current wins an equal score regardless of stable-ID ordering");

        ProbeExecutable higher = MakeProbe("solo.b-higher", 6f);
        higher.DefaultStatus = KLEPExecutableTickStatus.Running;
        neuron.RegisterExecutable(higher);
        KLEPDecisionTrace interrupted = neuron.TickViaAgent();
        Expect(current.ExitCount == 1 && current.CleanupCount == 1 &&
               current.ExitContexts[0].Reason == KLEPExecutableExitReason.Interrupted,
            "A strictly higher score interrupts and tears down the current Solo");
        Expect(higher.EnterCount == 1 && higher.TickCount == 1 &&
               interrupted.SelectedExecutableId == higher.StableId,
            "The strictly higher challenger enters and advances in the interruption Tick");
        Expect(equal.TickCount == 0,
            "An idle equal-score challenger remains unadvanced during strict interruption");
    }

    private static void VerifyPatientStateWithTandemWork()
    {
        ProbeExecutable tandem = MakeProbe(
            "tandem.patient-work",
            -100f,
            executionMode: KLEPExecutionMode.Tandem,
            kind: KLEPExecutableKind.Sensor);
        var neuron = new KLEPNeuron("neuron.tandem-patient");
        neuron.RegisterExecutable(tandem);

        KLEPDecisionTrace trace = neuron.TickViaAgent(100f);
        Expect(tandem.TickCount == 1 && trace.TandemWaves.Count == 1,
            "Eligible Tandem work advances independently of Solo scoring threshold");
        Expect(trace.IsPatient && trace.SelectedExecutableId == null &&
               trace.CurrentSoloExecutableId == null,
            "Tandem work may occur while the Neuron remains patient for Solo stimuli");
    }

    private static void VerifyActiveRemovalCleansUpOnce()
    {
        ProbeExecutable probe = MakeProbe("solo.remove-active", 2f);
        probe.DefaultStatus = KLEPExecutableTickStatus.Running;
        var neuron = new KLEPNeuron("neuron.remove-active");
        neuron.RegisterExecutable(probe);
        neuron.TickViaAgent();

        neuron.RemoveExecutable(probe.StableId);
        KLEPDecisionTrace removed = neuron.TickViaAgent();
        Expect(probe.TickCount == 1 && probe.ExitCount == 1 &&
               probe.CleanupCount == 1,
            "Removing an active Executable cancels without another Tick and tears down once");
        Expect(probe.ExitContexts[0].TerminalState == KLEPExecutableState.Cancelled &&
               probe.ExitContexts[0].Reason == KLEPExecutableExitReason.Removed,
            "Active removal reports the explicit Removed cancellation reason");
        KLEPExecutableStepTrace cancellation = FindStep(
            removed, probe.StableId, KLEPExecutableStepKind.Cancellation);
        Expect(cancellation != null &&
               cancellation.State == KLEPExecutableState.Cancelled,
            "Active removal is inspectable in the decision trace");

        neuron.RemoveExecutable(probe.StableId);
        neuron.TickViaAgent();
        Expect(probe.ExitCount == 1 && probe.CleanupCount == 1,
            "Repeated removal cannot duplicate Exit or Cleanup");
    }

    private static void VerifyTickFaultIsTracedCleanedAndRethrown()
    {
        var sentinel = new InvalidOperationException("sentinel tick fault");
        ProbeExecutable probe = MakeProbe("solo.tick-fault", 2f);
        probe.TickAction = context => throw sentinel;
        var neuron = new KLEPNeuron("neuron.tick-fault");
        neuron.RegisterExecutable(probe);

        Exception observed = null;
        try
        {
            neuron.TickViaAgent();
        }
        catch (Exception fault)
        {
            observed = fault;
        }

        Expect(ReferenceEquals(observed, sentinel),
            "The original Tick exception is rethrown without silent substitution");
        Expect(probe.ExitCount == 1 && probe.CleanupCount == 1 &&
               probe.ExitContexts[0].TerminalState == KLEPExecutableState.Faulted &&
               ReferenceEquals(probe.ExitContexts[0].Fault, sentinel),
            "A Tick fault exits and cleans the entered run exactly once with fault context");
        Expect(neuron.LastDecisionViaAgent().Fault != null &&
               neuron.LastDecisionViaAgent().Fault.ExecutableStableId == probe.StableId &&
               neuron.LastDecisionViaAgent().Fault.Stage == KLEPExecutableLifecycleStage.Tick &&
               neuron.LastDecisionViaAgent().Fault.Message == sentinel.Message,
            "The Neuron records the exact Executable, stage, and message before rethrowing");
        KLEPExecutableStepTrace step = FindStep(
            neuron.LastDecisionViaAgent(), probe.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Faulted,
            "The faulted lifecycle result remains inspectable after the throw");
    }

    private static void VerifyTandemCascadeSettlesBeforeSolo()
    {
        KLEPKeyDefinition enemy = MakeKey("key.enemy");
        KLEPKeyDefinition cover = MakeKey("key.cover");
        ProbeExecutable sensor = MakeProbe(
            "tandem.z-enemy-sensor",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { enemy },
            kind: KLEPExecutableKind.Sensor);
        sensor.TickAction = context =>
        {
            context.Add(enemy);
            return KLEPExecutableTickStatus.Succeeded;
        };
        ProbeExecutable coverSensor = MakeProbe(
            "tandem.a-cover-sensor",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            executionLocks: new[]
            {
                MakeLock("lock.enemy", new KLEPKeyPresent(enemy.Id.Value))
            },
            declaredOutputs: new[] { cover },
            kind: KLEPExecutableKind.Sensor);
        coverSensor.TickAction = context =>
        {
            Expect(context.Keys.Contains(enemy.Id),
                "Dependent Tandem receives Enemy in its later-wave snapshot");
            context.Add(cover);
            return KLEPExecutableTickStatus.Succeeded;
        };
        ProbeExecutable solo = MakeProbe(
            "solo.use-cover",
            2f,
            executionLocks: new[]
            {
                MakeLock(
                    "lock.enemy-and-cover",
                    new KLEPAll(
                        new KLEPKeyPresent(enemy.Id.Value),
                        new KLEPKeyPresent(cover.Id.Value)))
            });
        solo.TickAction = context =>
        {
            Expect(context.Keys.Contains(enemy.Id) && context.Keys.Contains(cover.Id),
                "Solo receives the fully settled Tandem snapshot");
            return KLEPExecutableTickStatus.Succeeded;
        };

        var neuron = new KLEPNeuron("neuron.tandem-cascade");
        neuron.RegisterExecutable(solo);
        neuron.RegisterExecutable(sensor);
        neuron.RegisterExecutable(coverSensor);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(trace.TandemWaves.Count == 2 &&
               trace.TandemWaves[0].WaveIndex == 0 &&
               trace.TandemWaves[1].WaveIndex == 1,
            "Enemy then Cover settle through two deterministic Tandem waves");
        Expect(sensor.TickCount == 1 && coverSensor.TickCount == 1,
            "Every Tandem advances at most once during the outer Neuron Tick");
        Expect(trace.KeySnapshot.Contains(enemy.Id) &&
               trace.KeySnapshot.Contains(cover.Id) &&
               trace.KeySnapshot.WaveIndex == 2,
            "Both Local outputs are visible in the final same-Tick snapshot");
        Expect(solo.TickCount == 1 && trace.SelectedExecutableId == solo.StableId,
            "Solo selection occurs only after the Local Tandem cascade settles");
    }

    private static void VerifyProcessedRunningTandemWaitsUntilNextTickForLockCancellation()
    {
        KLEPKeyDefinition permit = MakeKey("key.tandem-running-permit");
        KLEPKeyDefinition trigger = MakeKey("key.tandem-close-trigger");
        ProbeExecutable running = MakeProbe(
            "tandem.a-running-before-close",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            executionLocks: new[]
            {
                MakeLock(
                    "lock.tandem-running-permit",
                    new KLEPKeyPresent(permit.Id.Value))
            },
            declaredOutputs: new[] { trigger });
        running.TickAction = context =>
        {
            context.Add(trigger);
            return KLEPExecutableTickStatus.Running;
        };
        ProbeExecutable closesLock = MakeProbe(
            "tandem.b-closes-running-lock",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            executionLocks: new[]
            {
                MakeLock(
                    "lock.tandem-close-trigger",
                    new KLEPKeyPresent(trigger.Id.Value))
            });
        closesLock.TickAction = context =>
        {
            bool sawPermit = context.Keys.TryGetFirst(
                permit.Id, out KLEPKeyFact visiblePermit);
            bool sawTrigger = context.Keys.TryGetFirst(
                trigger.Id, out KLEPKeyFact visibleTrigger);
            Expect(sawPermit && sawTrigger,
                "The later Tandem wave observes both facts it will remove");
            context.Remove(visiblePermit);
            context.Remove(visibleTrigger);
            return KLEPExecutableTickStatus.Succeeded;
        };

        var neuron = new KLEPNeuron("neuron.tandem-later-lock-close");
        neuron.InitializeKey(permit);
        neuron.RegisterExecutable(running);
        neuron.RegisterExecutable(closesLock);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        KLEPExecutableStepTrace runningStep = FindStep(
            first, running.StableId, KLEPExecutableStepKind.Tandem);
        Expect(first.TandemWaves.Count == 2 &&
               runningStep != null &&
               runningStep.State == KLEPExecutableState.Running &&
               running.ExitCount == 0 &&
               !first.KeySnapshot.Contains(permit.Id),
            "A processed Running Tandem is not reconsidered when a later same-Tick barrier closes its Locks");

        KLEPDecisionTrace following = neuron.TickViaAgent();
        KLEPExecutableStepTrace cancellation = FindStep(
            following, running.StableId, KLEPExecutableStepKind.Cancellation);
        Expect(running.TickCount == 1 &&
               running.ExitCount == 1 &&
               running.CleanupCount == 1 &&
               running.ExitContexts[0].Reason == KLEPExecutableExitReason.LocksClosed &&
               cancellation != null &&
               cancellation.State == KLEPExecutableState.Cancelled,
            "The Running Tandem observes its closed Locks and cancels at the following top-level Tick");
    }

    private static void VerifyDeclaredOutputOmissionFaultsBeforePublication()
    {
        KLEPKeyDefinition signal = MakeKey("key.unemitted-signal");
        ProbeExecutable producer = MakeProbe(
            "tandem.z-declares-only",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { signal },
            kind: KLEPExecutableKind.Sensor);
        ProbeExecutable dependent = MakeProbe(
            "tandem.a-dependent",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            executionLocks: new[]
            {
                MakeLock("lock.signal", new KLEPKeyPresent(signal.Id.Value))
            });

        var neuron = new KLEPNeuron("neuron.no-phantom-wave");
        neuron.RegisterExecutable(producer);
        neuron.RegisterExecutable(dependent);
        Exception fault = Catch(() => neuron.TickViaAgent());

        Expect(fault is InvalidOperationException &&
               producer.TickCount == 1 && dependent.TickCount == 0,
            "an attempted success that omitted a declared output faults before it can unlock a dependent Tandem");
        Expect(neuron.LastDecisionViaAgent().Fault != null &&
               neuron.LastDecisionViaAgent().Fault.ExecutableStableId == producer.StableId &&
               neuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.DeclaredOutputValidation,
            "declared-output omission is attributed to the exact Executable and validation stage");
        Expect(!neuron.LastDecisionViaAgent().KeySnapshot.Contains(signal.Id),
            "a declared-output fault never manufactures or publishes the missing Key");
    }

    private static void VerifyUndeclaredOutputFaults()
    {
        KLEPKeyDefinition undeclared = MakeKey("key.undeclared-output");
        ProbeExecutable probe = MakeProbe(
            "tandem.undeclared-output",
            0f,
            executionMode: KLEPExecutionMode.Tandem);
        probe.TickAction = context =>
        {
            context.Add(undeclared);
            return KLEPExecutableTickStatus.Succeeded;
        };
        var neuron = new KLEPNeuron("neuron.undeclared-output");
        neuron.RegisterExecutable(probe);

        Exception observed = null;
        try
        {
            neuron.TickViaAgent();
        }
        catch (Exception fault)
        {
            observed = fault;
        }

        Expect(observed is InvalidOperationException &&
               observed.Message.Contains("undeclared"),
            "Emitting an undeclared Add is a visible programming fault");
        Expect(probe.ExitCount == 1 && probe.CleanupCount == 1,
            "An undeclared emission still tears down the entered Tandem exactly once");
        Expect(neuron.LastDecisionViaAgent().Fault != null &&
               neuron.LastDecisionViaAgent().Fault.ExecutableStableId == probe.StableId &&
               neuron.LastDecisionViaAgent().Fault.Stage == KLEPExecutableLifecycleStage.Tick,
            "Undeclared emission records its Executable and lifecycle stage");
    }

    private static void VerifyWavePeersShareSnapshot()
    {
        KLEPKeyDefinition emitted = MakeKey("key.peer-output");
        ProbeExecutable first = MakeProbe(
            "tandem.peer-a",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { emitted });
        first.TickAction = context =>
        {
            context.Add(emitted);
            return KLEPExecutableTickStatus.Succeeded;
        };
        ProbeExecutable second = MakeProbe(
            "tandem.peer-b",
            0f,
            executionMode: KLEPExecutionMode.Tandem);

        var neuron = new KLEPNeuron("neuron.wave-peers");
        neuron.RegisterExecutable(second);
        neuron.RegisterExecutable(first);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(first.TickSnapshots.Count == 1 && second.TickSnapshots.Count == 1 &&
               ReferenceEquals(first.TickSnapshots[0], second.TickSnapshots[0]),
            "Eligible Tandem peers receive the same immutable wave snapshot instance");
        Expect(!first.TickSnapshots[0].Contains(emitted.Id) &&
               !second.TickSnapshots[0].Contains(emitted.Id),
            "No peer observes another peer's output before the wave barrier");
        Expect(trace.TandemWaves.Count == 1 &&
               ReferenceEquals(trace.TandemWaves[0].InputSnapshot,
                   first.TickSnapshots[0]) &&
               trace.TandemWaves[0].OutputSnapshot.Contains(emitted.Id),
            "The wave trace exposes input-before and output-after barrier snapshots");
    }

    private static void VerifySoloOutputIsDeferred()
    {
        KLEPKeyDefinition command = MakeKey("key.solo-command");
        ProbeExecutable solo = MakeProbe(
            "solo.defer-output",
            2f,
            declaredOutputs: new[] { command });
        solo.TickAction = context =>
        {
            context.Add(command);
            return KLEPExecutableTickStatus.Succeeded;
        };
        var neuron = new KLEPNeuron("neuron.solo-deferred");
        neuron.RegisterExecutable(solo);

        KLEPDecisionTrace emitted = neuron.TickViaAgent();
        Expect(!emitted.KeySnapshot.Contains(command.Id),
            "Solo Local output does not alter the current Tick snapshot");
        KLEPExecutableStepTrace step = FindStep(
            emitted, solo.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.Outputs.Count == 1 &&
               step.Outputs[0].KeyId == command.Id,
            "The deferred Solo output remains inspectable in its execution result");

        neuron.RemoveExecutable(solo.StableId);
        KLEPDecisionTrace visible = neuron.TickViaAgent();
        Expect(visible.InitialKeySnapshot.Contains(command.Id) &&
               visible.KeySnapshot.Contains(command.Id) &&
               CountFacts(visible.KeySnapshot, command.Id) == 1,
            "Solo output becomes one visible occurrence at the next top-level Tick");
    }

    private static void VerifyGoalAllAndOneLayerPerTick()
    {
        ProbeExecutable firstChild = MakeProbe("goal.all.child-a", 0f);
        ProbeExecutable secondChild = MakeProbe("goal.all.child-b", 0f);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.all",
                "All Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { firstChild, secondChild }),
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            });
        var neuron = new KLEPNeuron("neuron.goal-all");
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        Expect(firstChild.TickCount == 1 && secondChild.TickCount == 1 &&
               first.ExecutableStates[0].Goal.CurrentLayerIndex == 1 &&
               !first.ExecutableStates[0].Goal.IsComplete,
            "All advances only after every child actually succeeds");
        KLEPExecutableStepTrace running = FindStep(
            first, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(running != null && running.State == KLEPExecutableState.Running,
            "Completing one Goal layer does not process the next layer in the same Tick");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        Expect(second.ExecutableStates[0].Goal.CurrentLayerIndex == 2 &&
               second.ExecutableStates[0].Goal.IsComplete &&
               firstChild.TickCount == 1 && secondChild.TickCount == 1,
            "The following Tick processes exactly the next layer without refiring completed children");
        KLEPExecutableStepTrace succeeded = FindStep(
            second, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(succeeded != null && succeeded.State == KLEPExecutableState.Succeeded,
            "Final Goal-layer completion is observable as success for that Tick");
    }

    private static void VerifyGoalAllPreservesSuccessAndRetriesFailure()
    {
        ProbeExecutable completed = MakeProbe("goal.all-retry.completed", 0f);
        ProbeExecutable retry = MakeProbe("goal.all-retry.failed", 0f);
        retry.EnqueueStatuses(
            KLEPExecutableTickStatus.Failed,
            KLEPExecutableTickStatus.Succeeded);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.all-retry",
                "All Retry Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { completed, retry })
            });
        var neuron = new KLEPNeuron("neuron.goal-all-retry");
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        KLEPExecutableStepTrace firstStep = FindStep(
            first, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(completed.TickCount == 1 && retry.TickCount == 1 &&
               firstStep != null &&
               firstStep.State == KLEPExecutableState.Running &&
               !first.ExecutableStates[0].Goal.IsComplete,
            "All preserves successful child progress while a failed sibling leaves the layer incomplete");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        KLEPExecutableStepTrace secondStep = FindStep(
            second, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(completed.TickCount == 1 &&
               retry.TickCount == 2 &&
               retry.EnterCount == 2 &&
               secondStep != null &&
               secondStep.State == KLEPExecutableState.Succeeded &&
               second.ExecutableStates[0].Goal.IsComplete,
            "All skips the preserved success and retries the failed child on a later top-level Tick");
    }

    private static void VerifyRuntimeSnapshotsFreezeGoalProgress()
    {
        ProbeExecutable running = MakeProbe("goal.trace.running", 0f);
        running.EnqueueStatuses(
            KLEPExecutableTickStatus.Running,
            KLEPExecutableTickStatus.Succeeded);
        ProbeExecutable completed = MakeProbe("goal.trace.completed", 0f);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.trace",
                "Trace Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 3f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { running, completed })
            });
        var neuron = new KLEPNeuron("neuron.goal-trace");
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        Expect(first.ExecutableStates.Count == 1 &&
               first.ExecutableStates[0].ExecutableStableId == goal.StableId &&
               first.ExecutableStates[0].State == KLEPExecutableState.Running &&
               first.ExecutableStates[0].IsCurrentSolo,
            "Decision trace freezes every root runtime including current-Solo state");
        KLEPGoalRuntimeSnapshot firstGoal = first.ExecutableStates[0].Goal;
        Expect(firstGoal != null && firstGoal.CurrentLayerIndex == 0 &&
               !firstGoal.IsComplete && firstGoal.Layers.Count == 1 &&
               firstGoal.Layers[0].Children.Count == 2,
            "Goal runtime trace freezes its layer structure and progress");
        Expect(firstGoal.Layers[0].Children[0].Runtime.State ==
               KLEPExecutableState.Running &&
               !firstGoal.Layers[0].Children[0].CompletedInCurrentLayer &&
               firstGoal.Layers[0].Children[1].CompletedInCurrentLayer,
            "Goal trace distinguishes a running child from a completed sibling");

        IReadOnlyList<KLEPExecutableDefinition> definitions =
            neuron.GetRootExecutableDefinitionsSnapshot();
        Expect(definitions.Count == 1 && definitions[0].StableId == goal.StableId,
            "Neuron exposes only immutable root definitions to diagnostics");
        ExpectThrows<NotSupportedException>(() =>
            ((IList<KLEPExecutableRuntimeSnapshot>)first.ExecutableStates).Clear(),
            "Decision runtime snapshots cannot be modified through their collection");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        Expect(second.ExecutableStates[0].State == KLEPExecutableState.Succeeded &&
               second.ExecutableStates[0].Goal.IsComplete,
            "A later trace captures the later Goal completion");
        Expect(first.ExecutableStates[0].State == KLEPExecutableState.Running &&
               firstGoal.CurrentLayerIndex == 0 && !firstGoal.IsComplete &&
               firstGoal.Layers[0].Children[0].Runtime.State ==
                   KLEPExecutableState.Running,
            "Later lifecycle changes cannot rewrite an older runtime snapshot");
    }

    private static void VerifyGoalAnyRequiresActualSuccess()
    {
        ProbeExecutable blocked = MakeProbe(
            "goal.any.blocked",
            0f,
            executionLocks: new[]
            {
                MakeLock("lock.goal-any-missing", new KLEPKeyPresent("key.missing"))
            });
        ProbeExecutable succeeds = MakeProbe("goal.any.succeeds", 0f);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.any",
                "Any Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[] { blocked, succeeds })
            });
        var neuron = new KLEPNeuron("neuron.goal-any");
        neuron.RegisterExecutable(goal);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(blocked.TickCount == 0 && succeeds.TickCount == 1,
            "A blocked, never-fired child cannot satisfy Any; an actual success can");
        KLEPExecutableStepTrace step = FindStep(
            trace, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Succeeded &&
               trace.ExecutableStates[0].Goal.IsComplete,
            "Any completes only after observing one child success");
    }

    private static void VerifyGoalAnyUsesAuthoredSerialOrder()
    {
        var order = new List<string>();
        ProbeExecutable blocked = MakeProbe(
            "goal.any-serial.blocked",
            0f,
            executionLocks: new[]
            {
                MakeLock(
                    "lock.goal-any-serial-missing",
                    new KLEPKeyPresent("key.any-serial-missing"))
            });
        ProbeExecutable failed = MakeProbe("goal.any-serial.failed", 0f);
        failed.TickAction = context =>
        {
            order.Add("failed");
            return KLEPExecutableTickStatus.Failed;
        };
        int runningTicks = 0;
        ProbeExecutable running = MakeProbe("goal.any-serial.running", 0f);
        running.TickAction = context =>
        {
            runningTicks++;
            order.Add("running-" + runningTicks);
            return runningTicks == 1
                ? KLEPExecutableTickStatus.Running
                : KLEPExecutableTickStatus.Succeeded;
        };
        ProbeExecutable later = MakeProbe("goal.any-serial.later", 0f);
        later.TickAction = context =>
        {
            order.Add("later");
            return KLEPExecutableTickStatus.Succeeded;
        };
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.any-serial",
                "Any Serial Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AnyCanFire,
                    new KLEPExecutableBase[]
                    {
                        blocked,
                        failed,
                        running,
                        later
                    })
            });
        var neuron = new KLEPNeuron("neuron.goal-any-serial");
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        KLEPExecutableStepTrace firstStep = FindStep(
            first, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(blocked.TickCount == 0 &&
               failed.TickCount == 1 &&
               running.TickCount == 1 &&
               later.TickCount == 0 &&
               string.Join(",", order) == "failed,running-1" &&
               firstStep != null &&
               firstStep.State == KLEPExecutableState.Running,
            "Any follows authored order past blocked and failed children, then stops when a child remains Running");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        KLEPExecutableStepTrace secondStep = FindStep(
            second, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(failed.TickCount == 2 &&
               running.TickCount == 2 &&
               running.EnterCount == 1 &&
               later.TickCount == 0 &&
               string.Join(",", order) ==
                   "failed,running-1,failed,running-2" &&
               secondStep != null &&
               secondStep.State == KLEPExecutableState.Succeeded,
            "A Running Any child retains its run across Ticks and a later sibling never advances after that child succeeds");
    }

    private static void VerifyGoalNoneSkipsChildren()
    {
        ProbeExecutable mustNotRun = MakeProbe("goal.none.child", 0f);
        mustNotRun.TickAction = context =>
            throw new InvalidOperationException("None layer invoked its child");
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.none",
                "None Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.NoneNeedToFire,
                    new KLEPExecutableBase[] { mustNotRun })
            });
        var neuron = new KLEPNeuron("neuron.goal-none");
        neuron.RegisterExecutable(goal);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(mustNotRun.InitializeCount == 1 && mustNotRun.TickCount == 0,
            "None initializes owned structure but never executes its children");
        KLEPExecutableStepTrace step = FindStep(
            trace, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(step != null && step.State == KLEPExecutableState.Succeeded,
            "None advances its layer without child execution");
    }

    private static void VerifyGoalOwnScoreAndExclusiveOwnership()
    {
        ProbeExecutable attractiveChild = MakeProbe("goal.score.child", 1000f);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.own-score",
                "Own Score Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 1f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.NoneNeedToFire,
                    new KLEPExecutableBase[] { attractiveChild })
            });
        ProbeExecutable competitor = MakeProbe("solo.goal-competitor", 2f);
        var neuron = new KLEPNeuron("neuron.goal-own-score");

        ExpectThrows<InvalidOperationException>(() =>
            neuron.RegisterExecutable(attractiveChild),
            "A Goal-owned child cannot also register as a Neuron root");
        neuron.RegisterExecutable(goal);
        neuron.RegisterExecutable(competitor);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(trace.SelectedExecutableId == competitor.StableId &&
               attractiveChild.TickCount == 0,
            "Goal ranking uses the Goal's own score, not a child's proxy score");
    }

    private static void VerifyGoalIntrinsicAttractionUsesFinalTandemSnapshot()
    {
        const string needField = "need";
        KLEPKeyDefinition need = MakeKey("key.goal-attraction.need");
        var sensor = MakeProbe(
            "router.goal-attraction.need",
            0f,
            KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { need },
            kind: KLEPExecutableKind.Router);
        sensor.TickAction = context =>
        {
            IReadOnlyList<KLEPKeyFact> facts = context.Keys.FindAll(need.Id);
            long value = context.CycleIndex == 1 ? 10L : 0L;
            context.Replace(
                facts[0],
                new KLEPKeyPayload(new[]
                {
                    new KeyValuePair<string, KLEPKeyValue>(needField, value)
                }));
            return KLEPExecutableTickStatus.Succeeded;
        };

        var observedNeeds = new List<long>();
        var evaluator = new ProbeGoalAttractionEvaluator(
            "policy.goal-attraction.need",
            "v1",
            context =>
            {
                IReadOnlyList<KLEPKeyFact> facts =
                    context.KeySnapshot.FindAll(need.Id);
                if (facts.Count != 1 ||
                    !facts[0].Payload.TryGetInteger(needField, out long value))
                {
                    throw new InvalidOperationException("Need payload missing.");
                }

                observedNeeds.Add(value);
                return new KLEPGoalAttractionEvaluation(
                    value,
                    "need=" + value);
            });
        ProbeExecutable child = MakeProbe(
            "goal.attraction.child",
            1000f);
        child.DefaultStatus = KLEPExecutableTickStatus.Running;
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.attraction.dynamic",
                "Dynamic Need Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 1f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new[] { child })
            },
            null,
            evaluator);
        ProbeExecutable competitor = MakeProbe(
            "solo.goal-attraction.competitor",
            5f);
        competitor.DefaultStatus = KLEPExecutableTickStatus.Running;
        var neuron = new KLEPNeuron("neuron.goal-attraction.dynamic");
        neuron.InitializeKey(
            need,
            new KLEPKeyPayload(new[]
            {
                new KeyValuePair<string, KLEPKeyValue>(needField, 0L)
            }));
        neuron.RegisterExecutable(sensor);
        neuron.RegisterExecutable(goal);
        neuron.RegisterExecutable(competitor);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        CandidateEvaluation firstGoal = FindCandidate(
            first,
            goal.StableId);
        KLEPExecutableScoreComponent intrinsic =
            firstGoal.ScoreEvaluation.Components[1];

        Expect(observedNeeds.Count == 1 &&
               observedNeeds[0] == 10L &&
               first.InitialKeySnapshot.Facts[0].Payload.TryGetInteger(
                   needField,
                   out long initialNeed) &&
               initialNeed == 0L,
            "Goal attraction reads the final post-Tandem payload, not the initial snapshot");
        Expect(first.SelectedExecutableId == goal.StableId &&
               first.CurrentSoloExecutableId == goal.StableId &&
               firstGoal.ScoreEvaluation.Total == 11f &&
               firstGoal.ScoreEvaluation.Components.Count == 2 &&
               child.TickCount == 1,
            "intrinsic attraction selects the Running Goal without using its high-scoring child as a proxy");
        Expect(intrinsic.Kind ==
                   KLEPExecutableScoreComponentKind.GoalIntrinsicAttraction &&
               intrinsic.SourceId == evaluator.StableId &&
               intrinsic.SourceVersion == evaluator.Version &&
               intrinsic.Value == 10f &&
               intrinsic.Explanation == "need=10",
            "Goal attraction trace retains evaluator identity, version, value, and explanation");

        KLEPExecutableScoreEvaluation replayA =
            goal.EvaluateScore(first.KeySnapshot);
        KLEPExecutableScoreEvaluation replayB =
            goal.EvaluateScore(first.KeySnapshot);
        Expect(Serialize(replayA) == Serialize(replayB) &&
               replayA.Components[1].SourceVersion ==
                   replayB.Components[1].SourceVersion &&
               replayA.Components[1].Explanation ==
                   replayB.Components[1].Explanation,
            "the same immutable snapshot produces the same intrinsic Goal score and provenance");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        CandidateEvaluation secondGoal = FindCandidate(
            second,
            goal.StableId);
        KLEPExecutableStepTrace interrupted = FindStep(
            second,
            goal.StableId,
            KLEPExecutableStepKind.Cancellation);
        Expect(observedNeeds[observedNeeds.Count - 1] == 0L &&
               secondGoal.ScoreEvaluation.Total == 1f &&
               second.SelectedExecutableId == competitor.StableId &&
               second.CurrentSoloExecutableId == competitor.StableId,
            "a changed need contribution lets a different root Solo take dominance on the next top-level Tick");
        Expect(interrupted != null &&
               interrupted.ExitReason == KLEPExecutableExitReason.Interrupted &&
               child.ExitCount == 1 &&
               child.CleanupCount == 1,
            "strictly higher replacement unwinds the Running Goal and its child exactly once");
    }

    private static void VerifyGoalIntrinsicAttractionGuardsAndFaultTrace()
    {
        KLEPKeyDefinition missing = MakeKey("key.goal-attraction.missing");
        var blockedEvaluator = new ProbeGoalAttractionEvaluator(
            "policy.goal-attraction.blocked",
            "1",
            context => new KLEPGoalAttractionEvaluation(100f));
        var blocked = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.attraction.blocked",
                "Blocked Goal",
                KLEPExecutableKind.Goal,
                validationLocks: new[]
                {
                    MakeLock(
                        "lock.goal-attraction.blocked",
                        new KLEPKeyPresent(missing.Id.Value))
                }),
            null,
            null,
            blockedEvaluator);
        var blockedNeuron = new KLEPNeuron("neuron.goal-attraction.blocked");
        blockedNeuron.RegisterExecutable(blocked);
        KLEPDecisionTrace blockedTrace = blockedNeuron.TickViaAgent();
        CandidateEvaluation blockedCandidate = FindCandidate(
            blockedTrace,
            blocked.StableId);
        Expect(blockedEvaluator.EvaluationCount == 0 &&
               !blockedCandidate.IsEligible &&
               blockedCandidate.ScoreEvaluation == null,
            "an ineligible Goal never invokes project attraction policy and remains unscored");

        ExpectThrows<ArgumentException>(() => new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.attraction.invalid-id",
                "Invalid Evaluator",
                KLEPExecutableKind.Goal),
            null,
            null,
            new ProbeGoalAttractionEvaluator(
                " ",
                "1",
                context => new KLEPGoalAttractionEvaluation(0f))),
            "Goal construction rejects a blank attraction evaluator identity");
        ExpectThrows<ArgumentOutOfRangeException>(() =>
            new KLEPGoalAttractionEvaluation(float.NaN),
            "Goal attraction evaluation rejects non-finite contributions");
        var negative = new KLEPGoalAttractionEvaluation(
            -2.5f,
            "avoid remembered harm");
        Expect(negative.Contribution == -2.5f &&
               negative.Explanation == "avoid remembered harm",
            "Goal attraction accepts a finite negative signed contribution without losing its explanation");

        var nullEvaluator = new ProbeGoalAttractionEvaluator(
            "policy.goal-attraction.null",
            "1",
            context => null);
        KLEPNeuron nullNeuron = MakeAttractionFaultNeuron(
            "goal.attraction.null",
            0f,
            nullEvaluator);
        Exception nullFault = Catch(() => nullNeuron.TickViaAgent());
        Expect(nullFault is InvalidOperationException &&
               nullNeuron.LastDecisionViaAgent().Fault != null &&
               nullNeuron.LastDecisionViaAgent().Fault.ExecutableStableId ==
                   "goal.attraction.null" &&
               nullNeuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.AttractionEvaluation,
            "a null attraction result faults the owning Goal at the dedicated scoring stage");

        var sentinel = new InvalidOperationException("attraction sentinel");
        var faultingEvaluator = new ProbeGoalAttractionEvaluator(
            "policy.goal-attraction.fault",
            "1",
            context => throw sentinel);
        KLEPNeuron faultNeuron = MakeAttractionFaultNeuron(
            "goal.attraction.fault",
            0f,
            faultingEvaluator);
        Exception policyFault = Catch(() => faultNeuron.TickViaAgent());
        Expect(ReferenceEquals(policyFault, sentinel) &&
               faultNeuron.LastDecisionViaAgent().Fault != null &&
               faultNeuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.AttractionEvaluation,
            "a project-policy exception is rethrown unchanged and traced as attraction evaluation");

        var overflowEvaluator = new ProbeGoalAttractionEvaluator(
            "policy.goal-attraction.overflow",
            "1",
            context => new KLEPGoalAttractionEvaluation(float.MaxValue));
        KLEPNeuron overflowNeuron = MakeAttractionFaultNeuron(
            "goal.attraction.overflow",
            float.MaxValue,
            overflowEvaluator);
        Exception overflow = Catch(() => overflowNeuron.TickViaAgent());
        Expect(overflow is InvalidOperationException &&
               overflowNeuron.LastDecisionViaAgent().Fault != null &&
               overflowNeuron.LastDecisionViaAgent().Fault.ExecutableStableId ==
                   "goal.attraction.overflow" &&
               overflowNeuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.AttractionEvaluation,
            "finite components that overflow the total fault without publishing a partial candidate score");
    }

    private static void VerifyGoalActivationEmitsOncePerRun()
    {
        KLEPKeyDefinition activation = MakeKey("key.goal-active");
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.activation",
                "Activation Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 5f,
                declaredOutputs: new[] { activation }),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire),
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            },
            activation);
        var neuron = new KLEPNeuron("neuron.goal-activation");
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace entered = neuron.TickViaAgent();
        KLEPExecutableStepTrace firstStep = FindStep(
            entered, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(firstStep != null && firstStep.State == KLEPExecutableState.Running &&
               firstStep.Outputs.Count == 1 &&
               firstStep.Outputs[0].KeyId == activation.Id,
            "Goal Enter emits its activation Key exactly once for the run");
        Expect(!entered.KeySnapshot.Contains(activation.Id),
            "Goal activation follows the Solo next-Tick output boundary");

        KLEPDecisionTrace completed = neuron.TickViaAgent();
        KLEPExecutableStepTrace secondStep = FindStep(
            completed, goal.StableId, KLEPExecutableStepKind.Solo);
        Expect(completed.KeySnapshot.Contains(activation.Id) &&
               CountFacts(completed.KeySnapshot, activation.Id) == 1,
            "One activation occurrence becomes visible on the following Tick");
        Expect(secondStep != null && secondStep.State == KLEPExecutableState.Succeeded &&
               secondStep.Outputs.Count == 0,
            "A Running Goal does not re-enter or emit activation again on its next layer");
    }

    private static void VerifyTandemGoalIsUnscoredAutomaticComposite()
    {
        var evaluator = new ProbeGoalAttractionEvaluator(
            "policy.tandem-goal.must-not-run",
            "1",
            context => new KLEPGoalAttractionEvaluation(1000f));
        var tandemGoal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.tandem.background",
                "Background Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 1000f,
                executionMode: KLEPExecutionMode.Tandem),
            new[]
            {
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire),
                new KLEPGoalLayer(KLEPGoalLayerRequirement.NoneNeedToFire)
            },
            null,
            evaluator);
        ProbeExecutable solo = MakeProbe("action.tandem-goal.solo", 1f);
        var neuron = new KLEPNeuron("neuron.tandem-goal");
        neuron.RegisterExecutable(tandemGoal);
        neuron.RegisterExecutable(solo);

        KLEPDecisionTrace first = neuron.TickViaAgent();
        KLEPExecutableStepTrace firstGoal = FindStep(
            first,
            tandemGoal.StableId,
            KLEPExecutableStepKind.Tandem);
        Expect(firstGoal != null &&
               firstGoal.State == KLEPExecutableState.Running &&
               first.Candidates.Count == 1 &&
               first.Candidates[0].StableId == solo.StableId &&
               first.SelectedExecutableId == solo.StableId,
            "a root Tandem Goal advances automatically while only root Solos enter arbitration");
        Expect(evaluator.EvaluationCount == 0,
            "a root Tandem Goal receives no intrinsic-attraction evaluation");

        KLEPDecisionTrace second = neuron.TickViaAgent();
        KLEPExecutableStepTrace secondGoal = FindStep(
            second,
            tandemGoal.StableId,
            KLEPExecutableStepKind.Tandem);
        Expect(secondGoal != null &&
               secondGoal.State == KLEPExecutableState.Succeeded &&
               evaluator.EvaluationCount == 0,
            "a Tandem Goal retains composite progress without ever becoming a scored candidate");
    }

    private static void VerifyGoalCanBeginAnotherRegistrationTenure()
    {
        ProbeExecutable child = MakeProbe("goal.retenure.child", 0f);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.retenure",
                "Goal Retenure",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 2f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.NoneNeedToFire,
                    new KLEPExecutableBase[] { child })
            });
        var neuron = new KLEPNeuron("neuron.goal-retenure");
        neuron.RegisterExecutable(goal);
        neuron.TickViaAgent();
        neuron.RemoveExecutable(goal.StableId);
        neuron.TickViaAgent();
        neuron.RegisterExecutable(goal);
        KLEPDecisionTrace restarted = neuron.TickViaAgent();

        Expect(child.InitializeCount == 2,
            "Goal children initialize once for each committed Goal registration tenure");
        Expect(restarted.SelectedExecutableId == goal.StableId,
            "The same Goal instance can begin a clean second registration tenure");
    }

    private static void VerifyGoalOwnershipRaceIsRejectedAtCommit()
    {
        ProbeExecutable child = MakeProbe("goal.race.child", 1f);
        var neuron = new KLEPNeuron("neuron.goal-race");
        neuron.RegisterExecutable(child);
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.race.root",
                "Goal Race",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 2f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.NoneNeedToFire,
                    new KLEPExecutableBase[] { child })
            });
        neuron.RegisterExecutable(goal);

        KLEPDecisionTrace rejected = neuron.TickViaAgent();
        Expect(rejected.Candidates.Count == 0 &&
               neuron.GetRootExecutableDefinitionsSnapshot().Count == 0,
            "A child claimed after staged root registration invalidates and atomically rejects the proposed catalog");
        Expect(child.InitializeCount == 0 && child.TickCount == 0,
            "The ownership race cannot initialize or double-run the Goal child as a root");
    }

    private static void VerifyFailedGoalConstructionDoesNotClaimEarlierChildren()
    {
        ProbeExecutable freeChild = MakeProbe(
            "goal.atomic.free-child", -1f);
        ProbeExecutable neuronOwnedChild = MakeProbe(
            "goal.atomic.neuron-child", -1f);
        var originalNeuron = new KLEPNeuron("neuron.goal-atomic.original");
        originalNeuron.RegisterExecutable(neuronOwnedChild);
        originalNeuron.TickViaAgent();

        ExpectThrows<InvalidOperationException>(() => new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.atomic.failed",
                "Atomic Ownership Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 1f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.NoneNeedToFire,
                    new KLEPExecutableBase[]
                    {
                        freeChild,
                        neuronOwnedChild
                    })
            }),
            "A Neuron-owned later child rejects Goal construction");

        var acceptingNeuron = new KLEPNeuron("neuron.goal-atomic.accepting");
        acceptingNeuron.RegisterExecutable(freeChild);
        acceptingNeuron.TickViaAgent();

        Expect(freeChild.IsNeuronOwned &&
               freeChild.NeuronOwnerId == acceptingNeuron.StableId &&
               !freeChild.IsGoalOwned,
            "Failed Goal construction leaves every earlier child unclaimed");
    }

    private static void VerifySoloOutputFaultCannotOrphanRunningState()
    {
        var global = new KLEPKeyDefinition(
            new KLEPKeyId("key.requires-global-store"),
            "Requires Global Store",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        ProbeExecutable faulting = MakeProbe(
            "solo.output-fault",
            1f,
            declaredOutputs: new[] { global });
        faulting.TickAction = context =>
        {
            context.Add(global);
            return KLEPExecutableTickStatus.Running;
        };
        var neuron = new KLEPNeuron("neuron.solo-output-fault");
        neuron.RegisterExecutable(faulting);

        ExpectThrows<InvalidOperationException>(() => neuron.TickViaAgent(),
            "A declared Global output still faults without an injected Global store");
        Expect(faulting.ExitCount == 1 && faulting.CleanupCount == 1 &&
               faulting.ExitContexts[0].TerminalState == KLEPExecutableState.Faulted,
            "Output application failure faults and unwinds a Running Solo exactly once");
        Expect(neuron.AgentViaTest().CurrentSoloExecutableId == null &&
               neuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.OutputApplication,
            "A faulted Solo output cannot leave an orphan current runtime");

        ProbeExecutable replacement = MakeProbe("solo.after-output-fault", 2f);
        neuron.RegisterExecutable(replacement);
        Expect(neuron.TickViaAgent().SelectedExecutableId == replacement.StableId,
            "A later Solo can run normally after the faulted runtime was unwound");
    }

    private static void VerifyCrossScopeOutputCannotPoisonNeuron()
    {
        var keyId = new KLEPKeyId("key.cross-scope-output");
        var globalDefinition = new KLEPKeyDefinition(
            keyId,
            "Global Collision",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        var localDefinition = new KLEPKeyDefinition(
            keyId,
            "Local Collision",
            defaultLifetime: KLEPKeyLifetime.Persistent);
        var world = new KLEPKeyStore("world.cross-scope-output", KLEPKeyScope.Global);
        world.CreateAndStage(globalDefinition);
        world.CommitBoundary(1);
        ProbeExecutable producer = MakeProbe(
            "tandem.cross-scope-output",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { localDefinition });
        producer.TickAction = context =>
        {
            context.Add(localDefinition);
            return KLEPExecutableTickStatus.Succeeded;
        };
        var neuron = new KLEPNeuron("neuron.cross-scope-output", world);
        neuron.RegisterExecutable(producer);

        ExpectThrows<InvalidOperationException>(() => neuron.TickViaAgent(),
            "A Local output cannot reuse a currently visible Global Key ID");
        neuron.RemoveExecutable(producer.StableId);
        world.CommitBoundary(2);
        KLEPDecisionTrace recovered = neuron.TickViaAgent();
        Expect(recovered.KeySnapshot.Contains(globalDefinition.Id) &&
               CountFacts(recovered.KeySnapshot, globalDefinition.Id) == 1,
            "Rejected cross-scope output is rolled back and cannot poison later snapshots");
    }

    private static void VerifyFaultedOutputBatchRollsBackAllStores()
    {
        var globalDefinition = new KLEPKeyDefinition(
            new KLEPKeyId("key.atomic-global"),
            "Atomic Global",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        KLEPKeyDefinition trigger = MakeKey("key.atomic-trigger");
        KLEPKeyDefinition leak = MakeKey("key.must-not-leak");
        var world = new KLEPKeyStore("world.atomic-output", KLEPKeyScope.Global);
        world.CreateAndStage(globalDefinition);
        world.CommitBoundary(1);

        ProbeExecutable firstWave = MakeProbe(
            "tandem.atomic-a",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { trigger });
        firstWave.TickAction = context =>
        {
            Expect(context.Keys.TryGetFirst(
                    globalDefinition.Id, out KLEPKeyFact visibleGlobal),
                "First atomic wave observes the activated Global fact");
            context.Remove(visibleGlobal);
            context.Add(trigger);
            return KLEPExecutableTickStatus.Succeeded;
        };
        ProbeExecutable secondWave = MakeProbe(
            "tandem.atomic-b",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            executionLocks: new[]
            {
                MakeLock("lock.atomic-trigger", new KLEPKeyPresent(trigger.Id.Value))
            },
            declaredOutputs: new[] { leak });
        secondWave.TickAction = context =>
        {
            context.Add(leak);
            Expect(context.Keys.TryGetFirst(
                    globalDefinition.Id, out KLEPKeyFact stillVisibleGlobal),
                "Deferred Global removal remains visible to the next Local wave");
            context.Remove(stillVisibleGlobal);
            return KLEPExecutableTickStatus.Succeeded;
        };
        var neuron = new KLEPNeuron("neuron.atomic-output", world);
        neuron.RegisterExecutable(firstWave);
        neuron.RegisterExecutable(secondWave);

        ExpectThrows<InvalidOperationException>(() => neuron.TickViaAgent(),
            "A second removal already pending in another wave faults the output batch");
        Expect(neuron.LastDecisionViaAgent().KeySnapshot.Contains(trigger.Id),
            "The fault trace retains the previously committed earlier-wave snapshot");
        neuron.RemoveExecutable(firstWave.StableId);
        neuron.RemoveExecutable(secondWave.StableId);
        world.CommitBoundary(2);
        KLEPDecisionTrace next = neuron.TickViaAgent();
        Expect(!next.KeySnapshot.Contains(leak.Id),
            "A later operation failure rolls back every earlier operation in that wave");
        Expect(next.KeySnapshot.Contains(trigger.Id),
            $"A previously committed wave remains intact when a later wave rolls back " +
            $"(trigger count {CountFacts(next.KeySnapshot, trigger.Id)})");
    }

    private static void VerifyTandemPeerUnwindsWhenWaveFaults()
    {
        KLEPKeyDefinition output = MakeKey("key.wave-abort-output");
        ProbeExecutable runningPeer = MakeProbe(
            "tandem.abort-a-running",
            0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { output });
        runningPeer.TickAction = context =>
        {
            context.Add(output);
            return KLEPExecutableTickStatus.Running;
        };
        var sentinel = new InvalidOperationException("later Tandem fault");
        ProbeExecutable faultingPeer = MakeProbe(
            "tandem.abort-b-fault",
            0f,
            executionMode: KLEPExecutionMode.Tandem);
        faultingPeer.TickAction = context => throw sentinel;
        var neuron = new KLEPNeuron("neuron.wave-abort");
        neuron.RegisterExecutable(runningPeer);
        neuron.RegisterExecutable(faultingPeer);

        Exception observed = null;
        try
        {
            neuron.TickViaAgent();
        }
        catch (Exception fault)
        {
            observed = fault;
        }

        Expect(ReferenceEquals(observed, sentinel),
            "A later Tandem callback still rethrows its original exception");
        Expect(runningPeer.ExitCount == 1 && runningPeer.CleanupCount == 1 &&
               runningPeer.ExitContexts[0].Reason ==
                   KLEPExecutableExitReason.WaveAborted,
            "A Running earlier peer unwinds when its uncommitted wave aborts");
        Expect(!neuron.LastDecisionViaAgent().KeySnapshot.Contains(output.Id),
            "An aborted wave publishes none of its peer outputs");

        neuron.RemoveExecutable(faultingPeer.StableId);
        KLEPDecisionTrace retried = neuron.TickViaAgent();
        Expect(runningPeer.EnterCount == 2 && retried.KeySnapshot.Contains(output.Id),
            "The unwound peer re-enters next Tick and can reproduce its output");
    }

    private static void
        VerifyInitializationCallbackFaultRejectsWholeRegistrationBoundary()
    {
        ProbeExecutable survivor = MakeProbe(
            "solo.registration-callback-survivor",
            1f);
        var neuron = new KLEPNeuron(
            "neuron.registration-callback-transaction");
        neuron.RegisterExecutable(survivor);
        KLEPAgent agent = neuron.AgentViaTest();
        agent.Tick();
        long priorRevision = neuron.CatalogRevision;

        ProbeExecutable initializedFirst = MakeProbe(
            "solo.registration-callback-a-first",
            -1f);
        var sentinel = new InvalidOperationException(
            "later registration initialization fault");
        ProbeExecutable faultingLater = MakeProbe(
            "solo.registration-callback-z-fault",
            -1f);
        faultingLater.InitializeAction = context => throw sentinel;
        neuron.RegisterExecutable(initializedFirst);
        neuron.RegisterExecutable(faultingLater);

        Exception observed = Catch(() => agent.Tick());
        Expect(ReferenceEquals(observed, sentinel),
            "A later initialization callback rethrows its original fault");
        KLEPStructuralMapDecisionTrace rollbackStructural =
            agent.LastTrace.Decision.StructuralMap;
        IReadOnlyList<KLEPExecutableDefinition> roots =
            neuron.GetRootExecutableDefinitionsSnapshot();
        Expect(roots.Count == 1 &&
               roots[0].StableId == survivor.StableId &&
               initializedFirst.InitializeCount == 1 &&
               faultingLater.InitializeCount == 1,
            "A later callback fault rejects every registration initialized in that boundary");
        Expect(!initializedFirst.IsNeuronOwned &&
               !faultingLater.IsNeuronOwned,
            "Rejected callback-fault registrations retain no Neuron ownership");

        KLEPExecutableStructuralMap recoveredMap = agent.ExecutableMap;
        Expect(neuron.CatalogRevision == priorRevision + 1 &&
               recoveredMap != null &&
               recoveredMap.IsValid &&
               recoveredMap.Snapshot.ProposedCatalogRevision ==
                   neuron.CatalogRevision.ToString(CultureInfo.InvariantCulture) &&
               recoveredMap.Snapshot.Roots.Count == 1 &&
               recoveredMap.Snapshot.Roots[0].StableExecutableId ==
                   survivor.StableId,
            "Callback-fault rollback replaces the proposed map with the exact active catalog at its revision");
        Expect(rollbackStructural.Trigger ==
                   KLEPStructuralMapTrigger.RegistrationRollbackRecovery &&
               rollbackStructural.Disposition ==
                   KLEPStructuralMapDisposition.Rejected &&
               rollbackStructural.ObserverStableId ==
                   KLEPBaselineStructuralObserver.Instance.StableId &&
               rollbackStructural.ObserverVersion ==
                   KLEPBaselineStructuralObserver.Instance.Version &&
               rollbackStructural.RejectedCatalogProposal &&
               rollbackStructural.Fault == null &&
               ReferenceEquals(
                   rollbackStructural.AttemptedAssessment,
                   agent.LastExecutableMapAttempt) &&
               ReferenceEquals(
                   rollbackStructural.ActiveAssessment,
                   recoveredMap) &&
               rollbackStructural.ProposedRevision ==
                   neuron.CatalogRevision.ToString(
                       CultureInfo.InvariantCulture) &&
               rollbackStructural.ActiveRevision ==
                   neuron.CatalogRevision.ToString(
                       CultureInfo.InvariantCulture) &&
               !rollbackStructural.ProposedFingerprint.Equals(
                   rollbackStructural.ActiveFingerprint),
            "Callback-fault rollback freezes the rejected registration map and recovered active map in one structural trace");
        Expect(agent.LastExecutableMapAttempt.Snapshot.Roots.Count == 3 &&
               !agent.LastExecutableMapAttempt.Fingerprint.Equals(
                   recoveredMap.Fingerprint),
            "The rejected proposal remains inspectable without becoming the accepted map");
        KLEPStructuralMapFingerprint rejectedProposalFingerprint =
            agent.LastExecutableMapAttempt.Fingerprint;
        KLEPStructuralMapFingerprint recoveredFingerprint =
            recoveredMap.Fingerprint;
        IReadOnlyList<KLEPExecutableRuntimeSnapshot> runtimes =
            agent.GetRootExecutableRuntimeSnapshot();
        Expect(runtimes.Count == 1 &&
               runtimes[0].ExecutableStableId == survivor.StableId,
            "Callback-fault rollback leaves no partially committed registration runtime");

        KLEPAgentTickTrace following = agent.Tick();
        Expect(initializedFirst.InitializeCount == 1 &&
               faultingLater.InitializeCount == 1 &&
               following.Decision.SelectedExecutableId == survivor.StableId &&
               agent.ExecutableMap.Snapshot.ProposedCatalogRevision ==
                   neuron.CatalogRevision.ToString(CultureInfo.InvariantCulture) &&
               agent.ExecutableMap.Fingerprint.Equals(recoveredFingerprint) &&
               !agent.ExecutableMap.Fingerprint.Equals(
                   rejectedProposalFingerprint),
            "Rejected callback-fault registrations do not initialize again or become reusable as the prior proposed map");
    }

    private static void
        VerifyInitializationOutputFaultRejectsWholeRegistrationBoundary()
    {
        ProbeExecutable survivor = MakeProbe(
            "solo.registration-output-survivor",
            1f);
        var neuron = new KLEPNeuron(
            "neuron.registration-output-transaction");
        neuron.RegisterExecutable(survivor);
        KLEPAgent agent = neuron.AgentViaTest();
        agent.Tick();
        long priorRevision = neuron.CatalogRevision;

        KLEPKeyDefinition validOutput = MakeKey(
            "key.registration-output-valid");
        ProbeExecutable initializedFirst = MakeProbe(
            "solo.registration-output-a-first",
            -1f,
            declaredOutputs: new[] { validOutput });
        initializedFirst.InitializeAction = context =>
            context.Add(validOutput);
        var invalidGlobalOutput = new KLEPKeyDefinition(
            new KLEPKeyId("key.registration-output-invalid-global"),
            "Invalid Global Initialization Output",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        ProbeExecutable invalidLater = MakeProbe(
            "solo.registration-output-z-invalid",
            -1f,
            declaredOutputs: new[] { invalidGlobalOutput });
        invalidLater.InitializeAction = context =>
            context.Add(invalidGlobalOutput);
        neuron.RegisterExecutable(initializedFirst);
        neuron.RegisterExecutable(invalidLater);

        ExpectThrows<InvalidOperationException>(() => agent.Tick(),
            "A later invalid initialization output rejects the complete registration batch");
        KLEPStructuralMapDecisionTrace rollbackStructural =
            agent.LastTrace.Decision.StructuralMap;
        IReadOnlyList<KLEPExecutableDefinition> roots =
            neuron.GetRootExecutableDefinitionsSnapshot();
        Expect(roots.Count == 1 &&
               roots[0].StableId == survivor.StableId &&
               initializedFirst.InitializeCount == 1 &&
               invalidLater.InitializeCount == 1,
            "Invalid later initialization output removes every registration from that boundary");
        Expect(!initializedFirst.IsNeuronOwned &&
               !invalidLater.IsNeuronOwned,
            "Output-fault rejection releases every new registration's ownership");

        KLEPExecutableStructuralMap recoveredMap = agent.ExecutableMap;
        Expect(neuron.CatalogRevision == priorRevision + 1 &&
               recoveredMap != null &&
               recoveredMap.IsValid &&
               recoveredMap.Snapshot.ProposedCatalogRevision ==
                   neuron.CatalogRevision.ToString(CultureInfo.InvariantCulture) &&
               recoveredMap.Snapshot.Roots.Count == 1 &&
               recoveredMap.Snapshot.Roots[0].StableExecutableId ==
                   survivor.StableId,
            "Output-fault rollback keeps CatalogRevision and accepted map aligned with the active roots");
        Expect(rollbackStructural.Trigger ==
                   KLEPStructuralMapTrigger.RegistrationRollbackRecovery &&
               rollbackStructural.Disposition ==
                   KLEPStructuralMapDisposition.Rejected &&
               rollbackStructural.ObserverStableId ==
                   KLEPBaselineStructuralObserver.Instance.StableId &&
               rollbackStructural.ObserverVersion ==
                   KLEPBaselineStructuralObserver.Instance.Version &&
               rollbackStructural.RejectedCatalogProposal &&
               rollbackStructural.Fault == null &&
               ReferenceEquals(
                   rollbackStructural.AttemptedAssessment,
                   agent.LastExecutableMapAttempt) &&
               ReferenceEquals(
                   rollbackStructural.ActiveAssessment,
                   recoveredMap) &&
               rollbackStructural.ProposedRevision ==
                   neuron.CatalogRevision.ToString(
                       CultureInfo.InvariantCulture) &&
               rollbackStructural.ActiveRevision ==
                   neuron.CatalogRevision.ToString(
                       CultureInfo.InvariantCulture) &&
               !rollbackStructural.ProposedFingerprint.Equals(
                   rollbackStructural.ActiveFingerprint),
            "Output-fault rollback freezes the rejected registration map and recovered active map in one structural trace");
        Expect(agent.LastExecutableMapAttempt.Snapshot.Roots.Count == 3 &&
               !agent.LastExecutableMapAttempt.Fingerprint.Equals(
                   recoveredMap.Fingerprint),
            "The rejected output-fault proposal remains inspectable without becoming the accepted map");
        KLEPStructuralMapFingerprint rejectedProposalFingerprint =
            agent.LastExecutableMapAttempt.Fingerprint;
        KLEPStructuralMapFingerprint recoveredFingerprint =
            recoveredMap.Fingerprint;
        IReadOnlyList<KLEPExecutableRuntimeSnapshot> runtimes =
            agent.GetRootExecutableRuntimeSnapshot();
        Expect(runtimes.Count == 1 &&
               runtimes[0].ExecutableStableId == survivor.StableId,
            "Output-fault rollback leaves no earlier or later registration runtime behind");

        KLEPAgentTickTrace following = agent.Tick();
        Expect(!following.Decision.KeySnapshot.Contains(validOutput.Id) &&
               initializedFirst.InitializeCount == 1 &&
               invalidLater.InitializeCount == 1 &&
               following.Decision.SelectedExecutableId == survivor.StableId &&
               agent.ExecutableMap.Snapshot.ProposedCatalogRevision ==
                   neuron.CatalogRevision.ToString(CultureInfo.InvariantCulture) &&
               agent.ExecutableMap.Fingerprint.Equals(recoveredFingerprint) &&
               !agent.ExecutableMap.Fingerprint.Equals(
                   rejectedProposalFingerprint),
            "Rejected initialization output neither leaks, retries, nor becomes reusable as the prior proposed map");
    }

    private static void VerifyInitializationOutputFailureRejectsRegistration()
    {
        var global = new KLEPKeyDefinition(
            new KLEPKeyId("key.bad-initialization-output"),
            "Bad Initialization Output",
            scope: KLEPKeyScope.Global,
            defaultLifetime: KLEPKeyLifetime.Persistent);
        ProbeExecutable probe = MakeProbe(
            "solo.bad-initialization-output",
            1f,
            declaredOutputs: new[] { global });
        probe.InitializeAction = context => context.Add(global);
        var neuron = new KLEPNeuron("neuron.bad-initialization-output");
        neuron.RegisterExecutable(probe);

        ExpectThrows<InvalidOperationException>(() => neuron.TickViaAgent(),
            "Invalid initialization output rejects the registration boundary");
        KLEPDecisionTrace following = neuron.TickViaAgent();
        Expect(probe.InitializeCount == 1 && following.Candidates.Count == 0 &&
               following.IsPatient,
            "A rejected registration cannot continue without its one-time initialization output");
    }

    private static void VerifyGoalChildFaultKeepsChildIdentity()
    {
        var sentinel = new InvalidOperationException("goal child sentinel");
        ProbeExecutable child = MakeProbe("goal.fault.child", 0f);
        child.TickAction = context => throw sentinel;
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.fault.root",
                "Faulting Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 2f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { child })
            });
        var neuron = new KLEPNeuron("neuron.goal-child-fault");
        neuron.RegisterExecutable(goal);

        Exception observed = null;
        try
        {
            neuron.TickViaAgent();
        }
        catch (Exception fault)
        {
            observed = fault;
        }

        Expect(ReferenceEquals(observed, sentinel),
            "A Goal child fault rethrows the original child exception");
        Expect(neuron.LastDecisionViaAgent().Fault.ExecutableStableId == child.StableId &&
               neuron.LastDecisionViaAgent().Fault.Stage == KLEPExecutableLifecycleStage.Tick,
            "Goal fault trace preserves the actual child identity and stage");
        Expect(child.ExitCount == 1 && child.CleanupCount == 1,
            "The faulting Goal child unwinds exactly once");
    }

    private static void VerifyGoalCancellationContinuesAfterChildFault()
    {
        var sentinel = new InvalidOperationException(
            "goal child cancellation sentinel");
        ProbeExecutable faultingChild = MakeProbe(
            "goal.cancel.child-a-faults", 0f);
        faultingChild.DefaultStatus = KLEPExecutableTickStatus.Running;
        faultingChild.ExitAction = context => throw sentinel;
        ProbeExecutable laterChild = MakeProbe(
            "goal.cancel.child-b-must-clean", 0f);
        laterChild.DefaultStatus = KLEPExecutableTickStatus.Running;
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                "goal.cancel.root",
                "Cancellation Goal",
                KLEPExecutableKind.Goal,
                baseAttractiveness: 2f),
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new KLEPExecutableBase[] { faultingChild, laterChild })
            });
        var neuron = new KLEPNeuron("neuron.goal-cancellation-fault");
        neuron.RegisterExecutable(goal);

        neuron.TickViaAgent();
        Expect(faultingChild.TickCount == 1 && laterChild.TickCount == 1,
            "Both Goal children are Running before cancellation");
        neuron.RemoveExecutable(goal.StableId);

        Exception observed = null;
        try
        {
            neuron.TickViaAgent();
        }
        catch (Exception fault)
        {
            observed = fault;
        }

        Expect(ReferenceEquals(observed, sentinel),
            "A single child cancellation fault rethrows its original exception");
        Expect(faultingChild.ExitCount == 1 &&
               faultingChild.CleanupCount == 1,
            "The faulting child still attempts Exit and Cleanup exactly once");
        Expect(laterChild.ExitCount == 1 && laterChild.CleanupCount == 1,
            "A child fault cannot prevent later Running siblings from cleaning up");
        Expect(neuron.LastDecisionViaAgent().Fault.ExecutableStableId ==
                   faultingChild.StableId &&
               neuron.LastDecisionViaAgent().Fault.Stage ==
                   KLEPExecutableLifecycleStage.Exit,
            "Goal cancellation fault trace preserves the failing child and stage");
    }

    private static string RunRepeatableDecision(bool reverseRegistration)
    {
        var neuron = new KLEPNeuron("neuron.repeat");
        neuron.InitializeKey(MakeKey("key.present"));
        TestExecutable a = MakeExecutable(
            "exe.a",
            1.75f,
            validationLocks: new[]
            {
                MakeLock(
                    "lock.a-present",
                    new KLEPKeyPresent("key.present"),
                    0.25f)
            });
        TestExecutable b = MakeExecutable(
            "exe.b",
            1.5f,
            executionLocks: new[]
            {
                MakeLock(
                    "lock.b-any",
                    new KLEPAny(
                        new KLEPKeyPresent("key.missing"),
                        new KLEPKeyPresent("key.present")),
                    0.5f)
            });
        if (reverseRegistration)
        {
            neuron.RegisterExecutable(b);
            neuron.RegisterExecutable(a);
        }
        else
        {
            neuron.RegisterExecutable(a);
            neuron.RegisterExecutable(b);
        }

        KLEPDecisionTrace trace = neuron.TickViaAgent();
        return Serialize(trace);
    }

    private static KLEPKeySnapshot MakeSnapshot(params string[] keyIds)
    {
        var neuron = new KLEPNeuron("neuron.snapshot");
        foreach (string keyId in keyIds)
        {
            neuron.InitializeKey(MakeKey(keyId));
        }

        return neuron.TickViaAgent().KeySnapshot;
    }

    private static KLEPKeyDefinition MakeKey(string keyId)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(keyId),
            keyId,
            defaultLifetime: KLEPKeyLifetime.Persistent);
    }

    private static TestExecutable MakeExecutable(
        string executableId,
        float baseAttractiveness,
        IEnumerable<KLEPLock> validationLocks = null,
        IEnumerable<KLEPLock> executionLocks = null)
    {
        return new TestExecutable(new KLEPExecutableDefinition(
            executableId,
            executableId,
            KLEPExecutableKind.Action,
            validationLocks,
            executionLocks,
            baseAttractiveness));
    }

    private static ProbeExecutable MakeProbe(
        string executableId,
        float baseAttractiveness,
        KLEPExecutionMode executionMode = KLEPExecutionMode.Solo,
        IEnumerable<KLEPLock> validationLocks = null,
        IEnumerable<KLEPLock> executionLocks = null,
        IEnumerable<KLEPKeyDefinition> declaredOutputs = null,
        KLEPExecutableKind kind = KLEPExecutableKind.Action)
    {
        return new ProbeExecutable(new KLEPExecutableDefinition(
            executableId,
            executableId,
            kind,
            validationLocks,
            executionLocks,
            baseAttractiveness,
            executionMode,
            declaredOutputs));
    }

    private static KLEPNeuron MakeAttractionFaultNeuron(
        string goalId,
        float baseAttractiveness,
        IKLEPGoalAttractionEvaluator evaluator)
    {
        var goal = new KLEPGoal(
            new KLEPExecutableDefinition(
                goalId,
                goalId,
                KLEPExecutableKind.Goal,
                baseAttractiveness: baseAttractiveness),
            null,
            null,
            evaluator);
        var neuron = new KLEPNeuron("neuron." + goalId);
        neuron.RegisterExecutable(goal);
        return neuron;
    }

    private static KLEPLock MakeLock(
        string lockId,
        KLEPLockExpression expression,
        float attractiveness = 0f)
    {
        return new KLEPLock(lockId, lockId, expression, attractiveness);
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string executableStableId,
        KLEPExecutableStepKind kind)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.ExecutableStableId == executableStableId &&
                step.Kind == kind)
            {
                return step;
            }
        }

        return null;
    }

    private static CandidateEvaluation FindCandidate(
        KLEPDecisionTrace trace,
        string executableStableId)
    {
        foreach (CandidateEvaluation candidate in trace.Candidates)
        {
            if (candidate.StableId == executableStableId)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Candidate was not traced.");
    }

    private static int CountFacts(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        int count = 0;
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            if (fact.KeyId == keyId)
            {
                count++;
            }
        }

        return count;
    }

    private static string Serialize(KLEPExecutableEvaluation evaluation)
    {
        var parts = new List<string>();
        AppendGroup(parts, evaluation.Validation);
        AppendGroup(parts, evaluation.Execution);
        return $"{evaluation.ExecutableId}|{evaluation.IsEligible}|" +
               string.Join(";", parts);
    }

    private static string Serialize(KLEPExecutableScoreEvaluation evaluation)
    {
        var parts = new List<string>();
        foreach (KLEPExecutableScoreComponent component in evaluation.Components)
        {
            parts.Add($"{component.Kind}:{component.SourceId}:{Format(component.Value)}");
        }

        return $"{evaluation.ExecutableId}|{Format(evaluation.Total)}|" +
               string.Join(";", parts);
    }

    private static void AppendGroup(
        List<string> parts,
        KLEPExecutableLockGroupEvaluation group)
    {
        parts.Add($"{group.Group}:{group.IsSatisfied}");
        foreach (KLEPLockEvaluation item in group.Locks)
        {
            parts.Add($"{item.LockId}:{item.IsSatisfied}");
        }
    }

    private static string Serialize(KLEPDecisionTrace trace)
    {
        var text = new StringBuilder();
        text.Append(trace.CycleIndex).Append('|')
            .Append(trace.KeySnapshot.Tick).Append('|')
            .Append(trace.SelectedExecutableId ?? "<patient>").Append('|')
            .Append(trace.IsPatient).Append('|')
            .Append(trace.LifecyclePending);

        foreach (KLEPKeyFact fact in trace.KeySnapshot.Facts)
        {
            text.Append("|fact:")
                .Append(fact.KeyId.Value).Append(':')
                .Append(fact.OccurrenceId.StoreId).Append(':')
                .Append(fact.OccurrenceId.Sequence).Append(':')
                .Append(fact.Scope).Append(':')
                .Append(fact.Lifetime).Append(':')
                .Append(fact.ActivatedTick);
        }

        foreach (CandidateEvaluation candidate in trace.Candidates)
        {
            text.Append("|candidate:")
                .Append(candidate.StableId).Append(':')
                .Append(candidate.IsEligible).Append(':')
                .Append(candidate.Reason);
            AppendGroup(text, candidate.Eligibility.ExecutableEvaluation.Validation);
            AppendGroup(text, candidate.Eligibility.ExecutableEvaluation.Execution);

            if (candidate.ScoreEvaluation == null)
            {
                text.Append(":unscored");
                continue;
            }

            text.Append(":total=")
                .Append(Format(candidate.ScoreEvaluation.Total));
            foreach (KLEPExecutableScoreComponent component in
                     candidate.ScoreEvaluation.Components)
            {
                text.Append(":score=")
                    .Append(component.Kind).Append(',')
                    .Append(component.SourceId).Append(',')
                    .Append(Format(component.Value));
            }
        }

        return text.ToString();
    }

    private static void AppendGroup(
        StringBuilder text,
        KLEPExecutableLockGroupEvaluation group)
    {
        text.Append(":group=")
            .Append(group.Group).Append(',')
            .Append(group.IsSatisfied);
        foreach (KLEPLockEvaluation item in group.Locks)
        {
            text.Append(":lock=")
                .Append(item.LockId).Append(',')
                .Append(item.IsSatisfied);
            foreach (KLEPLockExpressionResult result in item.Results)
            {
                text.Append(',')
                    .Append(result.Path).Append(',')
                    .Append(result.Kind).Append(',')
                    .Append(result.StableKeyId ?? "<operator>").Append(',')
                    .Append(result.IsSatisfied);
            }
        }
    }

    private static string Format(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
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

        throw new InvalidOperationException($"Assertion failed: {message}");
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

    private sealed class ProbeGoalAttractionEvaluator :
        IKLEPGoalAttractionEvaluator
    {
        private readonly Func<KLEPGoalAttractionContext,
            KLEPGoalAttractionEvaluation> evaluate;

        public ProbeGoalAttractionEvaluator(
            string stableId,
            string version,
            Func<KLEPGoalAttractionContext,
                KLEPGoalAttractionEvaluation> evaluate)
        {
            StableId = stableId;
            Version = version;
            this.evaluate = evaluate ??
                throw new ArgumentNullException(nameof(evaluate));
        }

        public string StableId { get; }
        public string Version { get; }
        public int EvaluationCount { get; private set; }
        public KLEPGoalAttractionContext LastContext { get; private set; }

        public KLEPGoalAttractionEvaluation Evaluate(
            KLEPGoalAttractionContext context)
        {
            EvaluationCount++;
            LastContext = context;
            return evaluate(context);
        }
    }

    private sealed class TestExecutable : KLEPExecutableBase
    {
        public TestExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }
    }

    private sealed class ProbeExecutable : KLEPExecutableBase
    {
        private readonly Queue<KLEPExecutableTickStatus> statuses =
            new Queue<KLEPExecutableTickStatus>();

        public ProbeExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }

        public int InitializeCount { get; private set; }
        public int EnterCount { get; private set; }
        public int TickCount { get; private set; }
        public int ExitCount { get; private set; }
        public int CleanupCount { get; private set; }
        public KLEPExecutableTickStatus DefaultStatus { get; set; } =
            KLEPExecutableTickStatus.Succeeded;
        public Action<KLEPExecutableInitializationContext> InitializeAction { get; set; }
        public Action<KLEPExecutionContext> EnterAction { get; set; }
        public Func<KLEPExecutionContext, KLEPExecutableTickStatus> TickAction { get; set; }
        public Action<KLEPExecutableExitContext> ExitAction { get; set; }
        public Action<KLEPExecutableExitContext> CleanupAction { get; set; }
        public List<string> Events { get; } = new List<string>();
        public List<KLEPKeySnapshot> TickSnapshots { get; } =
            new List<KLEPKeySnapshot>();
        public List<KLEPExecutableExitContext> ExitContexts { get; } =
            new List<KLEPExecutableExitContext>();

        public void EnqueueStatuses(params KLEPExecutableTickStatus[] values)
        {
            foreach (KLEPExecutableTickStatus value in values)
            {
                statuses.Enqueue(value);
            }
        }

        protected override void OnInitialize(
            KLEPExecutableInitializationContext context)
        {
            InitializeCount++;
            Events.Add("initialize");
            InitializeAction?.Invoke(context);
        }

        protected override void OnEnter(KLEPExecutionContext context)
        {
            EnterCount++;
            Events.Add("enter");
            EnterAction?.Invoke(context);
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            Events.Add("tick");
            TickSnapshots.Add(context.Keys);
            if (TickAction != null)
            {
                return TickAction(context);
            }

            return statuses.Count > 0
                ? statuses.Dequeue()
                : DefaultStatus;
        }

        protected override void OnExit(KLEPExecutableExitContext context)
        {
            ExitCount++;
            Events.Add("exit");
            ExitContexts.Add(context);
            ExitAction?.Invoke(context);
        }

        protected override void OnCleanup(KLEPExecutableExitContext context)
        {
            CleanupCount++;
            Events.Add("cleanup");
            CleanupAction?.Invoke(context);
        }
    }

    private sealed class TestGoal : KLEPGoal
    {
        public TestGoal(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }
    }
}
