using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roll4d4.Klep.Behaviors;
using Roll4d4.Klep.Core;

internal static class Program
{
    private static readonly KLEPKeyDefinition HumanInRange = OneCycleKey(
        "fact.human-in-range",
        "Human in range");

    private static readonly KLEPKeyDefinition WanderIntent = OneCycleKey(
        "intent.wander",
        "Wander intent");

    private static readonly KLEPKeyDefinition EatHumanIntent = OneCycleKey(
        "intent.eat-human",
        "Eat human intent");

    private static int Main()
    {
        Console.WriteLine("KLEP minimal consumer: Wander unless a human is in range");
        Console.WriteLine();

        bool[] observations = { false, true, false };
        string[] expectedSelections =
        {
            "action.wander",
            "goal.eat-human",
            "action.wander"
        };

        IReadOnlyList<string> firstRun = RunScenario(
            observations,
            expectedSelections,
            printTrace: true);
        IReadOnlyList<string> replay = RunScenario(
            observations,
            expectedSelections,
            printTrace: false);

        for (int index = 0; index < firstRun.Count; index++)
        {
            if (!StringComparer.Ordinal.Equals(firstRun[index], replay[index]))
            {
                Console.Error.WriteLine(
                    $"Determinism check failed at cycle {index + 1}.");
                return 1;
            }
        }

        Console.WriteLine("Determinism check: identical inputs produced identical traces.");
        return 0;
    }

    private static IReadOnlyList<string> RunScenario(
        IReadOnlyList<bool> observations,
        IReadOnlyList<string> expectedSelections,
        bool printTrace)
    {
        KLEPObservedKeySensorExecutable humanSensor = BuildHumanSensor();
        KLEPExecutableBase wander = BuildIntentAction(
            "action.wander",
            "Wander",
            1f,
            WanderIntent);
        KLEPGoal eatHuman = BuildEatHumanGoal();

        var neuron = new KLEPNeuron("neuron.zombie.example");

        // Registration order is deliberately not scheduling order. The Neuron
        // uses stable identities, execution modes, eligibility, and score.
        neuron.RegisterExecutable(wander);
        neuron.RegisterExecutable(eatHuman);
        neuron.RegisterExecutable(humanSensor);

        var signatures = new List<string>(observations.Count);
        for (int index = 0; index < observations.Count; index++)
        {
            humanSensor.SetObservation(observations[index]);
            KLEPDecisionTrace trace = neuron.Tick();

            if (!StringComparer.Ordinal.Equals(
                    trace.SelectedExecutableId,
                    expectedSelections[index]))
            {
                throw new InvalidOperationException(
                    $"Cycle {trace.CycleIndex} selected " +
                    $"'{trace.SelectedExecutableId ?? "<none>"}', expected " +
                    $"'{expectedSelections[index]}'.");
            }

            signatures.Add(TraceSignature(trace));
            if (printTrace)
            {
                PrintTrace(observations[index], trace);
                ApplyHostIntent(trace);
                Console.WriteLine();
            }
        }

        return signatures;
    }

    private static KLEPObservedKeySensorExecutable BuildHumanSensor()
    {
        var definition = new KLEPExecutableDefinition(
            "sensor.human-in-range",
            "Human range sensor",
            KLEPExecutableKind.Sensor,
            executionMode: KLEPExecutionMode.Tandem,
            declaredOutputs: new[] { HumanInRange });

        return new KLEPObservedKeySensorExecutable(definition);
    }

    private static KLEPGoal BuildEatHumanGoal()
    {
        KLEPLockExpression humanIsPresent =
            new KLEPKeyPresent(HumanInRange.Id.Value);

        KLEPExecutableBase bite = new IntentExecutable(
            new KLEPExecutableDefinition(
                "action.bite-human",
                "Bite human",
                KLEPExecutableKind.Action,
                executionLocks: new[]
                {
                    new KLEPLock(
                        "lock.bite-human.human-present",
                        "A human remains in range",
                        humanIsPresent)
                },
                declaredOutputs: new[] { EatHumanIntent }),
            EatHumanIntent);

        var goalDefinition = new KLEPExecutableDefinition(
            "goal.eat-human",
            "Eat human",
            KLEPExecutableKind.Goal,
            executionLocks: new[]
            {
                new KLEPLock(
                    "lock.goal.eat-human.human-present",
                    "A human is in range",
                    humanIsPresent)
            },
            baseAttractiveness: 10f,
            executionMode: KLEPExecutionMode.Solo);

        return new KLEPGoal(
            goalDefinition,
            new[]
            {
                new KLEPGoalLayer(
                    KLEPGoalLayerRequirement.AllMustFire,
                    new[] { bite })
            });
    }

    private static KLEPExecutableBase BuildIntentAction(
        string stableId,
        string displayName,
        float score,
        KLEPKeyDefinition intent)
    {
        return new IntentExecutable(
            new KLEPExecutableDefinition(
                stableId,
                displayName,
                KLEPExecutableKind.Action,
                baseAttractiveness: score,
                executionMode: KLEPExecutionMode.Solo,
                declaredOutputs: new[] { intent }),
            intent);
    }

    private static void PrintTrace(
        bool humanObserved,
        KLEPDecisionTrace trace)
    {
        Console.WriteLine(
            $"Cycle {trace.CycleIndex}: host observed humanInRange={humanObserved}");
        Console.WriteLine(
            $"  Tandem waves: {trace.TandemWaves.Count}; " +
            $"frozen Keys: {KeyIds(trace.KeySnapshot)}");

        foreach (CandidateEvaluation candidate in trace.Candidates)
        {
            string score = candidate.Score.HasValue
                ? candidate.Score.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : "not scored";
            Console.WriteLine(
                $"  Candidate {candidate.StableId}: " +
                $"eligible={candidate.IsEligible}, score={score}");
        }

        Console.WriteLine(
            $"  Selected: {trace.SelectedExecutableId ?? "<patient>"}");

        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind == KLEPExecutableStepKind.Initialization)
            {
                continue;
            }

            Console.WriteLine(
                $"  Step {step.Kind}: {step.ExecutableStableId} -> {step.State}" +
                FormatOutputs(step.Outputs));
        }
    }

    private static void ApplyHostIntent(KLEPDecisionTrace trace)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind != KLEPExecutableStepKind.Solo)
            {
                continue;
            }

            foreach (KLEPExecutableOutput output in step.Outputs)
            {
                if (output.Kind != KLEPExecutableOutputKind.Add)
                {
                    continue;
                }

                if (output.KeyId == WanderIntent.Id)
                {
                    Console.WriteLine(
                        "  Host applies intent after Tick: choose next wander heading");
                }
                else if (output.KeyId == EatHumanIntent.Id)
                {
                    Console.WriteLine(
                        "  Host applies intent after Tick: perform bite effect");
                }
            }
        }
    }

    private static string TraceSignature(KLEPDecisionTrace trace)
    {
        var text = new StringBuilder();
        text.Append(trace.CycleIndex).Append('|')
            .Append(trace.SelectedExecutableId ?? "<patient>").Append('|')
            .Append(KeyIds(trace.KeySnapshot));

        foreach (CandidateEvaluation candidate in trace.Candidates)
        {
            text.Append("|candidate:")
                .Append(candidate.StableId).Append(':')
                .Append(candidate.IsEligible).Append(':')
                .Append(candidate.Score.HasValue
                    ? candidate.Score.Value.ToString("R", CultureInfo.InvariantCulture)
                    : "unscored");
        }

        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            text.Append("|step:")
                .Append(step.Kind).Append(':')
                .Append(step.ExecutableStableId).Append(':')
                .Append(step.State)
                .Append(FormatOutputs(step.Outputs));
        }

        return text.ToString();
    }

    private static string KeyIds(KLEPKeySnapshot snapshot)
    {
        if (snapshot.Facts.Count == 0)
        {
            return "<none>";
        }

        var ids = new string[snapshot.Facts.Count];
        for (int index = 0; index < snapshot.Facts.Count; index++)
        {
            ids[index] = snapshot.Facts[index].KeyId.Value;
        }

        return string.Join(", ", ids);
    }

    private static string FormatOutputs(
        IReadOnlyList<KLEPExecutableOutput> outputs)
    {
        if (outputs.Count == 0)
        {
            return string.Empty;
        }

        var ids = new string[outputs.Count];
        for (int index = 0; index < outputs.Count; index++)
        {
            ids[index] = outputs[index].KeyId.Value +
                " from " + outputs[index].SourceExecutableId;
        }

        return "; outputs=[" + string.Join(", ", ids) + "]";
    }

    private static KLEPKeyDefinition OneCycleKey(
        string stableId,
        string displayName)
    {
        return new KLEPKeyDefinition(
            new KLEPKeyId(stableId),
            displayName,
            defaultLifetime: KLEPKeyLifetime.OneCycle);
    }

    private sealed class IntentExecutable : KLEPExecutableBase
    {
        private readonly KLEPKeyDefinition intent;

        internal IntentExecutable(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition intent)
            : base(definition)
        {
            this.intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }

        protected override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            context.Add(intent);
            return KLEPExecutableTickStatus.Succeeded;
        }
    }
}
