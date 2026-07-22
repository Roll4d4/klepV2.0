using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Imagination;

internal static class Program
{
    private const string GateId = "key.test.gate";
    private const string OutputId = "key.test.found";
    private const string CapabilityId = "test.search-arc";
    private const string CapabilityVersion = "1";
    private static int assertions;

    private static readonly KLEPKeyDefinition Gate = new KLEPKeyDefinition(
        new KLEPKeyId(GateId),
        "Gate",
        scope: KLEPKeyScope.Local,
        defaultLifetime: KLEPKeyLifetime.Persistent);

    private static readonly KLEPKeyDefinition Output = new KLEPKeyDefinition(
        new KLEPKeyId(OutputId),
        "Found",
        scope: KLEPKeyScope.Local,
        defaultLifetime: KLEPKeyLifetime.OneCycle);

    private static void Main()
    {
        VerifyCanonicalStrongManifest();
        VerifyWeakConjectureCannotRun();
        VerifyStrictRejections();
        VerifyFreshMaterializationAndStaleness();
        VerifyLocksAndTruthfulOutputs();
        VerifyRuntimeCannotBeShared();

        Console.WriteLine(
            $"KLEP Imagination smoke passed: {assertions} assertions.");
    }

    private static void VerifyCanonicalStrongManifest()
    {
        var factory = new TestFactory(Output, ResultMode.Succeed);
        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(factory);
        var compiler = new KLEPImaginationManifestCompiler();
        KLEPImaginationManifest first = compiler.CompileStrong(
            StrongJson(3, "wide", permuted: false),
            catalog);
        KLEPImaginationManifest second = compiler.CompileStrong(
            StrongJson(3, "wide", permuted: true),
            catalog);

        Expect(first.ProposalFingerprint == second.ProposalFingerprint,
            "JSON property order does not change the proposal fingerprint");
        Expect(first.CanonicalJson == second.CanonicalJson,
            "JSON property order produces identical canonical text");
        Expect(first.ExecutableStableId == second.ExecutableStableId &&
               first.ExecutableStableId.StartsWith(
                   "imagined.",
                   StringComparison.Ordinal),
            "The compiler derives one stable imagined Executable ID");
        Expect(first.Definition.Kind == KLEPExecutableKind.Action &&
               first.Definition.ExecutionMode == KLEPExecutionMode.Solo &&
               first.Definition.ExecutionLocks.Count == 1 &&
               first.Definition.DeclaredOutputs.Count == 1 &&
               ReferenceEquals(first.Definition.DeclaredOutputs[0], Output),
            "The trusted descriptor, not JSON, owns shape, Locks, and outputs");
        Expect(first.Arguments.Count == 2 &&
               first.Arguments["steps"].AsInteger() == 3 &&
               first.Arguments["pattern"].AsText() == "wide",
            "The manifest retains exact validated typed arguments");
        Expect(first.CapabilityCatalogFingerprint == catalog.Fingerprint,
            "The manifest binds the exact capability catalog fingerprint");
    }

    private static void VerifyWeakConjectureCannotRun()
    {
        var compiler = new KLEPImaginationManifestCompiler();
        string weak =
            "{" +
            "\"schema\":\"klep.imagination.conjecture.v1\"," +
            "\"requestFingerprint\":\"request.missing-verb\"," +
            "\"title\":\"Listen for frightened humans\"," +
            "\"conjecture\":\"Sound may bridge wandering to a sighting.\"," +
            "\"details\":{\"proposedCapability\":\"listen_for_fear\"," +
            "\"newKeys\":[\"human-noise\"]}}";
        KLEPImaginationConjecture conjecture =
            compiler.ParseWeakConjecture(weak);

        Expect(conjecture.Kind == KLEPImaginationProposalKind.WeakConjecture &&
               conjecture.Title == "Listen for frightened humans" &&
               conjecture.Fingerprint.StartsWith(
                   "sha256:",
                   StringComparison.Ordinal),
            "A Weak Conjecture is retained as immutable fingerprinted evidence");

        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(
            new TestFactory(Output, ResultMode.Succeed));
        ExpectReject(
            () => compiler.CompileStrong(weak, catalog),
            KLEPImaginationRejectionCode.WeakConjectureNotRunnable,
            "A Weak Conjecture cannot enter the runnable compiler");
    }

    private static void VerifyStrictRejections()
    {
        var compiler = new KLEPImaginationManifestCompiler();
        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(
            new TestFactory(Output, ResultMode.Succeed));

        ExpectReject(
            () => compiler.CompileStrong(
                StrongJson(3, "wide", false).Replace(
                    "\"hypothesis\":",
                    "\"declaredOutputs\":[\"made-up\"],\"hypothesis\":"),
                catalog),
            KLEPImaginationRejectionCode.UnknownProperty,
            "Model-authored output properties fail closed");
        ExpectReject(
            () => compiler.CompileStrong(
                StrongJson(3, "wide", false).Replace(
                    CapabilityId,
                    "unknown.capability"),
                catalog),
            KLEPImaginationRejectionCode.UnknownCapability,
            "Unknown physical capabilities are not runnable");
        ExpectReject(
            () => compiler.CompileStrong(
                StrongJson(99, "wide", false),
                catalog),
            KLEPImaginationRejectionCode.InvalidArgument,
            "Out-of-range arguments fail closed");
        ExpectReject(
            () => compiler.CompileStrong(
                StrongJson(3, "invented", false),
                catalog),
            KLEPImaginationRejectionCode.InvalidArgument,
            "Text outside the descriptor allow-list fails closed");
        ExpectReject(
            () => compiler.CompileStrong(
                StrongJson(3, "wide", false).Replace(
                    "\"pattern\":\"wide\",",
                    string.Empty),
                catalog),
            KLEPImaginationRejectionCode.InvalidArgument,
            "Missing required arguments fail closed");
        ExpectReject(
            () => compiler.CompileStrong("{broken", catalog),
            KLEPImaginationRejectionCode.MalformedJson,
            "Malformed JSON fails closed");
        ExpectReject(
            () => compiler.CompileStrong(
                new string(' ', KLEPImaginationManifestCompiler.MaximumDocumentBytes + 1),
                catalog),
            KLEPImaginationRejectionCode.DocumentTooLarge,
            "Oversize model output is rejected before parsing");
    }

    private static void VerifyFreshMaterializationAndStaleness()
    {
        var factory = new TestFactory(Output, ResultMode.Succeed);
        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(factory);
        KLEPImaginationManifest manifest =
            new KLEPImaginationManifestCompiler().CompileStrong(
                StrongJson(4, "narrow", false),
                catalog);
        var materializer = new KLEPImaginationMaterializer();
        KLEPImaginedExecutable first = materializer.Materialize(
            manifest,
            catalog);
        KLEPImaginedExecutable second = materializer.Materialize(
            manifest,
            catalog);

        Expect(!ReferenceEquals(first, second) && factory.CreatedCount == 2,
            "Two materializations create fresh Executable and runtime instances");
        Expect(ReferenceEquals(first.Definition, second.Definition),
            "Fresh runtimes may safely share one immutable compiled definition");

        KLEPImaginationCapabilityCatalog changed = CreateCatalog(
            new TestFactory(Output, ResultMode.Succeed),
            descriptorFingerprint: "descriptor.search-arc.v2");
        ExpectReject(
            () => materializer.Materialize(manifest, changed),
            KLEPImaginationRejectionCode.StaleCapabilityBinding,
            "Catalog or descriptor drift invalidates materialization");
    }

    private static void VerifyLocksAndTruthfulOutputs()
    {
        var compiler = new KLEPImaginationManifestCompiler();

        KLEPImaginationCapabilityCatalog successCatalog = CreateCatalog(
            new TestFactory(Output, ResultMode.Succeed));
        KLEPImaginationManifest successManifest = compiler.CompileStrong(
            StrongJson(2, "wide", false),
            successCatalog);
        KLEPImaginedExecutable blocked =
            new KLEPImaginationMaterializer().Materialize(
                successManifest,
                successCatalog);
        var blockedNeuron = new KLEPNeuron("neuron.imagination.blocked");
        blockedNeuron.RegisterExecutable(blocked);
        KLEPDecisionTrace blockedTrace = new KLEPAgent(blockedNeuron).Tick().Decision;
        Expect(blockedTrace.IsPatient &&
               blockedTrace.SelectedExecutableId == null &&
               blocked.LastIntent == null,
            "Materialization cannot bypass a closed descriptor-owned Lock");

        KLEPImaginedExecutable admitted =
            new KLEPImaginationMaterializer().Materialize(
                successManifest,
                successCatalog);
        var neuron = new KLEPNeuron("neuron.imagination.success");
        neuron.InitializeKey(Gate);
        neuron.RegisterExecutable(admitted);
        KLEPDecisionTrace trace = new KLEPAgent(neuron).Tick().Decision;
        KLEPExecutableStepTrace step = FindStep(
            trace,
            admitted.StableId);
        Expect(step != null &&
               step.State == KLEPExecutableState.Succeeded &&
               step.Result != null &&
               step.Result.Outputs.Count == 1 &&
               step.Result.Outputs[0].KeyId == Output.Id,
            "A grounded success emits exactly the descriptor-owned output");
        Expect(admitted.TryGetIntent(
                   trace.CycleIndex,
                   out KLEPImaginationIntent intent) &&
               intent.ExecutableStableId == admitted.StableId &&
               intent.RunIndex == step.Result.RunIndex,
            "A host intent is bound to exact Executable, cycle, and run identity");

        VerifyInvalidResultFault(ResultMode.OmitOutput,
            "A success that omits its guaranteed output faults");
        VerifyInvalidResultFault(ResultMode.ExtraOutput,
            "A success with an undeclared output faults");
    }

    private static void VerifyRuntimeCannotBeShared()
    {
        var shared = new TestRuntime(Output, ResultMode.Succeed);
        var factory = new SharedFactory(shared);
        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(factory);
        KLEPImaginationManifest manifest =
            new KLEPImaginationManifestCompiler().CompileStrong(
                StrongJson(3, "wide", false),
                catalog);
        var materializer = new KLEPImaginationMaterializer();
        materializer.Materialize(manifest, catalog);
        ExpectThrows<InvalidOperationException>(
            () => materializer.Materialize(manifest, catalog),
            "cannot be shared",
            "A faulty factory cannot share mutable runtime across Executables");
    }

    private static void VerifyInvalidResultFault(
        ResultMode mode,
        string label)
    {
        KLEPImaginationCapabilityCatalog catalog = CreateCatalog(
            new TestFactory(Output, mode));
        KLEPImaginationManifest manifest =
            new KLEPImaginationManifestCompiler().CompileStrong(
                StrongJson(2, "wide", false),
                catalog);
        KLEPImaginedExecutable executable =
            new KLEPImaginationMaterializer().Materialize(manifest, catalog);
        var neuron = new KLEPNeuron("neuron.imagination.invalid." + mode);
        neuron.InitializeKey(Gate);
        neuron.RegisterExecutable(executable);
        var agent = new KLEPAgent(neuron);
        ExpectReject(
            () => agent.Tick(),
            KLEPImaginationRejectionCode.InvalidCapabilityResult,
            label);
    }

    private static KLEPImaginationCapabilityCatalog CreateCatalog(
        IKLEPImaginationCapabilityRuntimeFactory factory,
        string descriptorFingerprint = "descriptor.search-arc.v1")
    {
        var template = new KLEPExecutableDefinition(
            "template.search-arc",
            "Search arc template",
            KLEPExecutableKind.Action,
            executionLocks: new[]
            {
                new KLEPLock(
                    "lock.search-arc.gate",
                    "Search is grounded",
                    new KLEPKeyPresent(GateId))
            },
            baseAttractiveness: 20f,
            executionMode: KLEPExecutionMode.Solo,
            declaredOutputs: new[] { Output });
        var descriptor = new KLEPImaginationCapabilityDescriptor(
            CapabilityId,
            CapabilityVersion,
            descriptorFingerprint,
            template,
            new[]
            {
                new KLEPImaginationParameterDefinition(
                    "steps",
                    KLEPImaginationValueKind.Integer,
                    minimumInteger: 1,
                    maximumInteger: 8),
                new KLEPImaginationParameterDefinition(
                    "pattern",
                    KLEPImaginationValueKind.Text,
                    maximumTextLength: 16,
                    allowedTexts: new[] { "narrow", "wide" })
            });
        return new KLEPImaginationCapabilityCatalog(
            new[]
            {
                new KLEPImaginationCapabilityRegistration(
                    descriptor,
                    factory)
            });
    }

    private static string StrongJson(
        int steps,
        string pattern,
        bool permuted)
    {
        if (!permuted)
        {
            return "{" +
                "\"schema\":\"klep.imagination.strong.v1\"," +
                "\"requestFingerprint\":\"request.no-solution.001\"," +
                "\"displayName\":\"Search a widening arc\"," +
                "\"capability\":{" +
                    "\"id\":\"" + CapabilityId + "\"," +
                    "\"version\":\"1\"," +
                    "\"arguments\":{" +
                        "\"pattern\":\"" + pattern + "\"," +
                        "\"steps\":" + steps + "}}," +
                "\"hypothesis\":{" +
                    "\"targetKey\":\"key.human-sighting\"," +
                    "\"explanation\":\"Turning may reveal a human.\"}}";
        }

        return "{ \"hypothesis\": {" +
            "\"explanation\":\"Turning may reveal a human.\"," +
            "\"targetKey\":\"key.human-sighting\"}," +
            "\"capability\":{" +
                "\"arguments\":{\"steps\":" + steps + "," +
                "\"pattern\":\"" + pattern + "\"}," +
                "\"version\":\"1\",\"id\":\"" + CapabilityId + "\"}," +
            "\"displayName\":\"Search a widening arc\"," +
            "\"requestFingerprint\":\"request.no-solution.001\"," +
            "\"schema\":\"klep.imagination.strong.v1\" }";
    }

    private static KLEPExecutableStepTrace FindStep(
        KLEPDecisionTrace trace,
        string stableId)
    {
        foreach (KLEPExecutableStepTrace step in trace.Executions)
        {
            if (step.Kind == KLEPExecutableStepKind.Solo &&
                StringComparer.Ordinal.Equals(
                    step.ExecutableStableId,
                    stableId))
            {
                return step;
            }
        }

        return null;
    }

    private static void ExpectReject(
        Action action,
        KLEPImaginationRejectionCode expectedCode,
        string label)
    {
        try
        {
            action();
        }
        catch (KLEPImaginationRejectedException exception)
        {
            Expect(exception.Code == expectedCode,
                label + " (exact rejection code)");
            return;
        }

        throw new InvalidOperationException(label + " (no rejection)");
    }

    private static void ExpectThrows<TException>(
        Action action,
        string messageFragment,
        string label)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            Expect(exception.Message.IndexOf(
                       messageFragment,
                       StringComparison.OrdinalIgnoreCase) >= 0,
                label + " (message)");
            return;
        }

        throw new InvalidOperationException(label + " (no exception)");
    }

    private static void Expect(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException("FAILED: " + label);
        }

        assertions++;
        Console.WriteLine("PASS: " + label);
    }

    private enum ResultMode
    {
        Succeed,
        OmitOutput,
        ExtraOutput
    }

    private sealed class TestFactory :
        IKLEPImaginationCapabilityRuntimeFactory
    {
        private readonly KLEPKeyDefinition output;
        private readonly ResultMode mode;

        internal TestFactory(KLEPKeyDefinition output, ResultMode mode)
        {
            this.output = output;
            this.mode = mode;
        }

        internal int CreatedCount { get; private set; }

        public KLEPImaginationCapabilityRuntime CreateRuntime(
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            CreatedCount++;
            return new TestRuntime(output, mode);
        }
    }

    private sealed class SharedFactory :
        IKLEPImaginationCapabilityRuntimeFactory
    {
        private readonly KLEPImaginationCapabilityRuntime runtime;

        internal SharedFactory(KLEPImaginationCapabilityRuntime runtime)
        {
            this.runtime = runtime;
        }

        public KLEPImaginationCapabilityRuntime CreateRuntime(
            IReadOnlyDictionary<string, KLEPImaginationValue> arguments)
        {
            return runtime;
        }
    }

    private sealed class TestRuntime : KLEPImaginationCapabilityRuntime
    {
        private readonly KLEPKeyDefinition output;
        private readonly ResultMode mode;

        internal TestRuntime(KLEPKeyDefinition output, ResultMode mode)
        {
            this.output = output;
            this.mode = mode;
        }

        public override KLEPImaginationCapabilityResult Tick(
            KLEPImaginationCapabilityContext context)
        {
            var intent = new KLEPImaginationIntentPayload(
                new[]
                {
                    new KeyValuePair<string, KLEPImaginationValue>(
                        "steps",
                        context.Arguments["steps"])
                });
            switch (mode)
            {
                case ResultMode.Succeed:
                    return KLEPImaginationCapabilityResult.Succeeded(
                        new[]
                        {
                            new KLEPImaginationSuccessfulOutput(output.Id)
                        },
                        intent);
                case ResultMode.OmitOutput:
                    return KLEPImaginationCapabilityResult.Succeeded(
                        null,
                        intent);
                case ResultMode.ExtraOutput:
                    return KLEPImaginationCapabilityResult.Succeeded(
                        new[]
                        {
                            new KLEPImaginationSuccessfulOutput(output.Id),
                            new KLEPImaginationSuccessfulOutput(
                                new KLEPKeyId("key.test.extra"))
                        },
                        intent);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
