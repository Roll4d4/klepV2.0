using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private const string GroundKeyId = "key.ground";
    private const string GroundSensorId = "sensor.ground";
    private const string GroundActionId = "action.requires-ground";

    private static int assertions;

    private static void Main()
    {
        VerifyFalseObservationLeavesNeuronPatient();
        VerifyTrueObservationPublishesGroundInSameTick();
        VerifyOneCycleObservationDoesNotAccumulate();
        VerifyGroundUnlocksSoloInSameTick();
        VerifyGroundTraceIsDeterministic();
        VerifySensorDefinitionValidation();

        Console.WriteLine($"KLEP Behavior smoke passed: {assertions} assertions.");
    }

    private static void VerifyFalseObservationLeavesNeuronPatient()
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        var sensor = MakeGroundSensor(ground);
        var neuron = new KLEPNeuron("neuron.ground.false");
        neuron.RegisterExecutable(sensor);

        sensor.SetObservation(false);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(!sensor.IsPresent,
            "The sensor exposes its current false host observation");
        Expect(ReferenceEquals(sensor.OutputDefinition, ground),
            "The sensor retains the exact declared Ground definition");
        Expect(!trace.InitialKeySnapshot.Contains(GroundKeyId) &&
               !trace.KeySnapshot.Contains(GroundKeyId),
            "A false observation neither begins nor ends the Tick with Ground");
        Expect(trace.IsPatient && trace.SelectedExecutableId == null,
            "Tandem sensing alone leaves a Neuron patient when no Solo is available");

        KLEPExecutableStepTrace step = FindStep(
            trace, GroundSensorId, KLEPExecutableStepKind.Tandem);
        Expect(step != null && step.State == KLEPExecutableState.Failed,
            "a false observation is an explicit no-output failed sensor run");
        Expect(step.Outputs.Count == 0,
            "A false observation emits no Ground Key operation");
    }

    private static void VerifyTrueObservationPublishesGroundInSameTick()
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        var sensor = MakeGroundSensor(ground);
        var neuron = new KLEPNeuron("neuron.ground.true");
        neuron.RegisterExecutable(sensor);

        sensor.SetObservation(true);
        KLEPDecisionTrace trace = neuron.TickViaAgent();

        Expect(sensor.IsPresent,
            "The sensor exposes its current true host observation");
        Expect(!trace.InitialKeySnapshot.Contains(GroundKeyId),
            "Ground is absent before the Tandem sensor wave publishes");
        Expect(trace.KeySnapshot.Contains(GroundKeyId),
            "A true observation publishes Ground before the same Tick ends");
        Expect(trace.KeySnapshot.WaveIndex > trace.InitialKeySnapshot.WaveIndex,
            "A true observation creates an inspectable same-Tick wave barrier");
        Expect(HasChangedWaveContaining(trace, GroundKeyId),
            "The Tandem wave trace records the Ground state change");

        KLEPExecutableStepTrace step = FindStep(
            trace, GroundSensorId, KLEPExecutableStepKind.Tandem);
        Expect(step != null && step.State == KLEPExecutableState.Succeeded,
            "A true observation completes the sensor run");
        Expect(step.Outputs.Count == 1 &&
               step.Outputs[0].Kind == KLEPExecutableOutputKind.Add &&
               ReferenceEquals(step.Outputs[0].Definition, ground),
            "A true observation emits exactly one Add for the exact Ground definition");

        KLEPKeyFact fact = GetOnlyFact(trace.KeySnapshot, ground.Id);
        Expect(fact.Lifetime == KLEPKeyLifetime.OneCycle &&
               fact.ActivatedTick == trace.CycleIndex &&
               fact.SourceId == GroundSensorId,
            "Published Ground has OneCycle lifetime, same-Tick activation, and sensor provenance");
    }

    private static void VerifyOneCycleObservationDoesNotAccumulate()
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        var sensor = MakeGroundSensor(ground);
        var neuron = new KLEPNeuron("neuron.ground.lifetime");
        neuron.RegisterExecutable(sensor);

        sensor.SetObservation(true);
        KLEPDecisionTrace first = neuron.TickViaAgent();
        KLEPKeyFact firstFact = GetOnlyFact(first.KeySnapshot, ground.Id);

        sensor.SetObservation(true);
        KLEPDecisionTrace second = neuron.TickViaAgent();
        KLEPKeyFact secondFact = GetOnlyFact(second.KeySnapshot, ground.Id);

        Expect(CountFacts(first.KeySnapshot, ground.Id) == 1 &&
               CountFacts(second.KeySnapshot, ground.Id) == 1,
            "Repeated true observations retain exactly one current Ground occurrence");
        Expect(firstFact.OccurrenceId != secondFact.OccurrenceId,
            "A repeated true observation replaces the expired cycle fact with a new occurrence");
        Expect(secondFact.ActivatedTick == second.CycleIndex,
            "The repeated Ground occurrence belongs to the current top-level Tick");

        sensor.SetObservation(false);
        KLEPDecisionTrace third = neuron.TickViaAgent();
        Expect(!third.InitialKeySnapshot.Contains(GroundKeyId) &&
               !third.KeySnapshot.Contains(GroundKeyId),
            "Ground expires before the next Tick and remains absent after a false observation");
        KLEPExecutableStepTrace absentStep = FindStep(
            third, GroundSensorId, KLEPExecutableStepKind.Tandem);
        Expect(absentStep.State == KLEPExecutableState.Failed &&
               absentStep.Outputs.Count == 0,
            "the false follow-up fails without replacing the expired Ground fact");
    }

    private static void VerifyGroundUnlocksSoloInSameTick()
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        var sensor = MakeGroundSensor(ground);
        var action = new SnapshotProbeExecutable(
            new KLEPExecutableDefinition(
                GroundActionId,
                "Action requiring Ground",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "lock.ground-present",
                        "Ground present",
                        new KLEPKeyPresent(GroundKeyId))
                },
                baseAttractiveness: 1f));
        var neuron = new KLEPNeuron("neuron.ground.cascade");
        neuron.RegisterExecutable(action);
        neuron.RegisterExecutable(sensor);

        sensor.SetObservation(false);
        KLEPDecisionTrace blocked = neuron.TickViaAgent();
        Expect(blocked.IsPatient && action.TickCount == 0,
            "The Ground-gated Solo stays blocked while the observation is false");
        CandidateEvaluation blockedCandidate = FindCandidate(blocked, GroundActionId);
        Expect(!blockedCandidate.IsEligible && blockedCandidate.ScoreEvaluation == null,
            "The blocked Solo is filtered before scoring");

        sensor.SetObservation(true);
        KLEPDecisionTrace unlocked = neuron.TickViaAgent();
        Expect(unlocked.SelectedExecutableId == GroundActionId && !unlocked.IsPatient,
            "Ground produced by the Tandem selects the Solo later in the same Tick");
        Expect(action.TickCount == 1 &&
               action.LastTickSnapshot != null &&
               action.LastTickSnapshot.Contains(GroundKeyId),
            "The selected Solo executes against the final post-Tandem Ground snapshot");
        KLEPExecutableStepTrace actionStep = FindStep(
            unlocked, GroundActionId, KLEPExecutableStepKind.Solo);
        Expect(actionStep != null && actionStep.State == KLEPExecutableState.Succeeded,
            "The newly unlocked Solo completes through the normal lifecycle");
    }

    private static void VerifyGroundTraceIsDeterministic()
    {
        string forward = RunDeterministicScenario(false);
        string reverse = RunDeterministicScenario(true);
        Expect(forward == reverse,
            "Identical observations produce identical traces regardless of registration order");

        for (int repeat = 0; repeat < 20; repeat++)
        {
            Expect(RunDeterministicScenario((repeat & 1) != 0) == forward,
                $"Ground behavior trace remains deterministic on repeat {repeat}");
        }
    }

    private static void VerifySensorDefinitionValidation()
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        KLEPExecutableDefinition valid = MakeSensorDefinition(ground);
        var sensor = new KLEPObservedKeySensorExecutable(valid);
        Expect(sensor.Kind == KLEPExecutableKind.Sensor &&
               sensor.ExecutionMode == KLEPExecutionMode.Tandem &&
               sensor.DeclaredOutputs.Count == 1,
            "The accepted sensor shape is explicit and inspectable");

        ExpectThrows<ArgumentNullException>(
            () => new KLEPObservedKeySensorExecutable(null),
            "A null observed sensor definition is rejected");

        ExpectDefinitionError(
            new KLEPExecutableDefinition(
                GroundSensorId,
                "Wrong Kind",
                KLEPExecutableKind.Action,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[] { ground }),
            "An observed Key sensor requires Executable Kind Sensor.",
            "An observed sensor rejects the wrong Executable kind");

        ExpectDefinitionError(
            new KLEPExecutableDefinition(
                GroundSensorId,
                "Wrong Mode",
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Solo,
                declaredOutputs: new[] { ground }),
            "An observed Key sensor requires Tandem execution mode.",
            "An observed sensor rejects Solo execution mode");

        ExpectDefinitionError(
            new KLEPExecutableDefinition(
                GroundSensorId,
                "No Output",
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Tandem),
            "An observed Key sensor requires exactly one declared output.",
            "An observed sensor rejects an empty output shape");

        ExpectDefinitionError(
            new KLEPExecutableDefinition(
                GroundSensorId,
                "Two Outputs",
                KLEPExecutableKind.Sensor,
                executionMode: KLEPExecutionMode.Tandem,
                declaredOutputs: new[]
                {
                    ground,
                    new KLEPKeyDefinition(
                        new KLEPKeyId("key.other"),
                        "Other",
                        defaultLifetime: KLEPKeyLifetime.OneCycle)
                }),
            "An observed Key sensor requires exactly one declared output.",
            "An observed sensor rejects a multi-output shape");

        KLEPKeyDefinition persistentGround = new KLEPKeyDefinition(
            new KLEPKeyId(GroundKeyId),
            "Persistent Ground",
            defaultLifetime: KLEPKeyLifetime.Persistent);
        ExpectDefinitionError(
            MakeSensorDefinition(persistentGround),
            "An observed Key sensor output must use OneCycle lifetime.",
            "An observed sensor rejects a Persistent output definition");
    }

    private static string RunDeterministicScenario(bool reverseRegistration)
    {
        KLEPKeyDefinition ground = MakeGroundKey();
        var sensor = MakeGroundSensor(ground);
        var action = new SnapshotProbeExecutable(
            new KLEPExecutableDefinition(
                GroundActionId,
                "Ground Action",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "lock.ground-present",
                        "Ground present",
                        new KLEPKeyPresent(GroundKeyId))
                },
                baseAttractiveness: 1f));
        var neuron = new KLEPNeuron("neuron.ground.determinism");

        if (reverseRegistration)
        {
            neuron.RegisterExecutable(sensor);
            neuron.RegisterExecutable(action);
        }
        else
        {
            neuron.RegisterExecutable(action);
            neuron.RegisterExecutable(sensor);
        }

        sensor.SetObservation(true);
        return Serialize(neuron.TickViaAgent());
    }

    private static KLEPKeyDefinition MakeGroundKey()
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(GroundKeyId),
            "Ground",
            "The host ground probe observed supporting terrain this Tick.",
            KLEPKeyScope.Local,
            KLEPKeyLifetime.OneCycle);
    }

    private static KLEPObservedKeySensorExecutable MakeGroundSensor(
        KLEPKeyDefinition ground)
    {
        return new KLEPObservedKeySensorExecutable(MakeSensorDefinition(ground));
    }

    private static KLEPExecutableDefinition MakeSensorDefinition(
        KLEPKeyDefinition output)
    {
        return new KLEPExecutableDefinition(
            GroundSensorId,
            "Ground Sensor",
            KLEPExecutableKind.Sensor,
            baseAttractiveness: 0f,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { output });
    }

    private static bool HasChangedWaveContaining(
        KLEPDecisionTrace trace,
        string keyId)
    {
        foreach (KLEPTandemWaveTrace wave in trace.TandemWaves)
        {
            if (wave.DidLocalStateChange && wave.OutputSnapshot.Contains(keyId))
            {
                return true;
            }
        }

        return false;
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

        throw new InvalidOperationException(
            $"Candidate '{executableStableId}' was not present in the trace.");
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string executableStableId,
        KLEPExecutableStepKind kind)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.ExecutableStableId == executableStableId && step.Kind == kind)
            {
                return step;
            }
        }

        return null;
    }

    private static KLEPKeyFact GetOnlyFact(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        IReadOnlyList<KLEPKeyFact> facts = snapshot.FindAll(keyId);
        if (facts.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one '{keyId}' fact, but found {facts.Count}.");
        }

        return facts[0];
    }

    private static int CountFacts(
        KLEPKeySnapshot snapshot,
        KLEPKeyId keyId)
    {
        return snapshot.FindAll(keyId).Count;
    }

    private static string Serialize(KLEPDecisionTrace trace)
    {
        var text = new StringBuilder();
        text.Append("cycle=").Append(trace.CycleIndex)
            .Append("|selected=").Append(trace.SelectedExecutableId ?? "<patient>")
            .Append("|current=").Append(trace.CurrentSoloExecutableId ?? "<none>")
            .Append("|patient=").Append(trace.IsPatient);
        AppendSnapshot(text, "initial", trace.InitialKeySnapshot);
        AppendSnapshot(text, "final", trace.KeySnapshot);

        foreach (KLEPTandemWaveTrace wave in trace.TandemWaves)
        {
            text.Append("|wave=").Append(wave.WaveIndex)
                .Append(',').Append(wave.DidLocalStateChange)
                .Append(',').Append(wave.Termination);
            AppendSnapshot(text, "wave-in", wave.InputSnapshot);
            AppendCandidates(text, wave.Candidates);
            AppendExecutions(text, wave.Executions);
            AppendSnapshot(text, "wave-out", wave.OutputSnapshot);
        }

        AppendCandidates(text, trace.Candidates);
        AppendExecutions(text, trace.Executions);
        return text.ToString();
    }

    private static void AppendSnapshot(
        StringBuilder text,
        string label,
        KLEPKeySnapshot snapshot)
    {
        text.Append('|').Append(label).Append('=')
            .Append(snapshot.Tick).Append(',').Append(snapshot.WaveIndex);
        foreach (KLEPKeyFact fact in snapshot.Facts)
        {
            text.Append(";fact:")
                .Append(fact.KeyId.Value).Append(',')
                .Append(fact.OccurrenceId.StoreId).Append(',')
                .Append(fact.OccurrenceId.Sequence).Append(',')
                .Append(fact.Scope).Append(',')
                .Append(fact.Lifetime).Append(',')
                .Append(fact.ActivatedTick).Append(',')
                .Append(fact.SourceId);
        }
    }

    private static void AppendCandidates(
        StringBuilder text,
        IReadOnlyList<CandidateEvaluation> candidates)
    {
        foreach (CandidateEvaluation candidate in candidates)
        {
            text.Append("|candidate=")
                .Append(candidate.StableId).Append(',')
                .Append(candidate.IsEligible).Append(',')
                .Append(candidate.Score.HasValue
                    ? candidate.Score.Value.ToString("R", CultureInfo.InvariantCulture)
                    : "<none>");
        }
    }

    private static void AppendExecutions(
        StringBuilder text,
        IReadOnlyList<KLEPExecutableStepTrace> executions)
    {
        foreach (KLEPExecutableStepTrace step in executions)
        {
            text.Append("|step=")
                .Append(step.Kind).Append(',')
                .Append(step.ExecutableStableId).Append(',')
                .Append(step.Result.RunIndex).Append(',')
                .Append(step.State).Append(',')
                .Append(step.ExitReason.HasValue
                    ? step.ExitReason.Value.ToString()
                    : "<none>");
            foreach (KLEPExecutableOutput output in step.Outputs)
            {
                text.Append(";output:")
                    .Append(output.Kind).Append(',')
                    .Append(output.KeyId.Value).Append(',')
                    .Append(output.Scope).Append(',')
                    .Append(output.SourceExecutableId);
            }
        }
    }

    private static void ExpectDefinitionError(
        KLEPExecutableDefinition definition,
        string expectedMessage,
        string assertionMessage)
    {
        assertions++;
        try
        {
            _ = new KLEPObservedKeySensorExecutable(definition);
        }
        catch (ArgumentException exception)
        {
            if (exception.ParamName == "definition" &&
                exception.Message.IndexOf(expectedMessage, StringComparison.Ordinal) >= 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Assertion failed: {assertionMessage}. Unexpected error: {exception.Message}");
        }

        throw new InvalidOperationException($"Assertion failed: {assertionMessage}");
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

    private sealed class SnapshotProbeExecutable : KLEPExecutableBase
    {
        public SnapshotProbeExecutable(KLEPExecutableDefinition definition)
            : base(definition)
        {
        }

        public int TickCount { get; private set; }
        public KLEPKeySnapshot LastTickSnapshot { get; private set; }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            TickCount++;
            LastTickSnapshot = context.Keys;
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
