using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Owns one deterministic Local decision system. Tick is the only Core
    /// timing entry point: it settles Tandems, then advances at most one Solo.
    /// </summary>
    public sealed class KLEPNeuron
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly Dictionary<string, KLEPExecutableBase> executables =
            new Dictionary<string, KLEPExecutableBase>(IdComparer);
        private readonly Dictionary<string, KLEPExecutableRuntime> runtimes =
            new Dictionary<string, KLEPExecutableRuntime>(IdComparer);
        private readonly Dictionary<string, KLEPExecutableBase> pendingRegistrations =
            new Dictionary<string, KLEPExecutableBase>(IdComparer);
        private readonly HashSet<string> pendingRemovals =
            new HashSet<string>(IdComparer);
        private string currentSoloExecutableId;
        private bool isTicking;

        public KLEPNeuron(string stableId, KLEPKeyStore globalKeyStore = null)
        {
            ValidateStableId(stableId, nameof(stableId));
            if (globalKeyStore != null && globalKeyStore.Scope != KLEPKeyScope.Global)
            {
                throw new ArgumentException(
                    "An injected shared KeyStore must have Global scope.",
                    nameof(globalKeyStore));
            }

            StableId = stableId;
            LocalKeyStore = new KLEPKeyStore($"{stableId}.local", KLEPKeyScope.Local);
            GlobalKeyStore = globalKeyStore;
        }

        public string StableId { get; }
        internal KLEPKeyStore LocalKeyStore { get; }
        internal KLEPKeyStore GlobalKeyStore { get; }
        public long CycleIndex { get; private set; }
        public string CurrentSoloExecutableId => currentSoloExecutableId;
        public long NextCycleIndex
        {
            get
            {
                long next = CheckedNextCycle();
                return GlobalKeyStore != null && GlobalKeyStore.LastCommittedTick > next
                    ? GlobalKeyStore.LastCommittedTick
                    : next;
            }
        }

        public KLEPDecisionTrace LastTrace { get; private set; } =
            KLEPDecisionTrace.Empty;

        public void RegisterExecutable(KLEPExecutableBase executable)
        {
            if (executable == null)
            {
                throw new ArgumentNullException(nameof(executable));
            }

            ValidateStableId(executable.StableId, nameof(executable));
            if (executable.IsGoalOwned)
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' is owned by Goal " +
                    $"'{executable.GoalOwnerId}' and cannot also be registered " +
                    "as a Neuron root.");
            }

            if (executable.IsNeuronOwned &&
                (!executables.TryGetValue(
                    executable.StableId, out KLEPExecutableBase owned) ||
                 !ReferenceEquals(owned, executable)))
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' is already registered " +
                    $"by Neuron '{executable.NeuronOwnerId}'.");
            }

            if (pendingRegistrations.TryGetValue(
                    executable.StableId, out KLEPExecutableBase pending))
            {
                if (ReferenceEquals(pending, executable))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Executable ID '{executable.StableId}' is already staged " +
                    "for registration.");
            }

            bool explicitlyRemoved = pendingRemovals.Contains(executable.StableId);
            if (executables.TryGetValue(
                    executable.StableId, out KLEPExecutableBase registered))
            {
                if (ReferenceEquals(registered, executable) && !explicitlyRemoved)
                {
                    return;
                }

                if (!explicitlyRemoved)
                {
                    throw new InvalidOperationException(
                        $"Executable ID '{executable.StableId}' is already registered. " +
                        "Remove it before registering a replacement.");
                }
            }

            // When the ID is explicitly removed, keep both staged changes. The
            // old runtime must unwind before the replacement initializes.
            pendingRegistrations.Add(executable.StableId, executable);
        }

        public void RemoveExecutable(string stableId)
        {
            ValidateStableId(stableId, nameof(stableId));
            pendingRegistrations.Remove(stableId);
            pendingRemovals.Add(stableId);
        }

        public IReadOnlyList<KLEPExecutableDefinition>
            GetRootExecutableDefinitionsSnapshot()
        {
            var definitions = new List<KLEPExecutableDefinition>(executables.Count);
            foreach (KLEPExecutableBase executable in executables.Values)
            {
                definitions.Add(executable.Definition);
            }

            definitions.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return new ReadOnlyCollection<KLEPExecutableDefinition>(definitions);
        }

        /// <summary>
        /// Captures every committed root lifecycle controller in stable-ID
        /// order without exposing or advancing the controller itself.
        /// </summary>
        public IReadOnlyList<KLEPExecutableRuntimeSnapshot>
            GetRootExecutableRuntimeSnapshot()
        {
            var snapshots = new List<KLEPExecutableRuntimeSnapshot>(runtimes.Count);
            foreach (KLEPExecutableRuntime runtime in runtimes.Values)
            {
                snapshots.Add(runtime.CaptureSnapshot(
                    IdComparer.Equals(
                        runtime.Executable.StableId,
                        currentSoloExecutableId)));
            }

            snapshots.Sort((left, right) => IdComparer.Compare(
                left.ExecutableStableId,
                right.ExecutableStableId));
            return new ReadOnlyCollection<KLEPExecutableRuntimeSnapshot>(snapshots);
        }

        internal object GetRootExecutableRegistrationToken(string stableId)
        {
            ValidateStableId(stableId, nameof(stableId));
            return runtimes.TryGetValue(
                stableId, out KLEPExecutableRuntime runtime)
                    ? runtime
                    : null;
        }

        public KLEPKeyFact InitializeKey(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "initial")
        {
            ValidateCanInitialize(definition);
            return AddKey(definition, payload, lifetime, sourceId);
        }

        public IReadOnlyList<KLEPKeyFact> InitializeKeys(
            IEnumerable<KLEPKeyDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            if (CycleIndex != 0)
            {
                throw new InvalidOperationException(
                    "Initial keys can only be supplied before the first Tick.");
            }

            var validated = new List<KLEPKeyDefinition>();
            foreach (KLEPKeyDefinition definition in definitions)
            {
                ValidateCanInitialize(definition);
                validated.Add(definition);
            }

            var facts = new List<KLEPKeyFact>(validated.Count);
            foreach (KLEPKeyDefinition definition in validated)
            {
                facts.Add(AddKey(definition, sourceId: "initial"));
            }

            return new ReadOnlyCollection<KLEPKeyFact>(facts);
        }

        public KLEPKeyFact AddKey(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            EnsureExternalMutationAllowed();
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return GetStore(definition.Scope).CreateAndStage(
                definition, payload, lifetime, sourceId);
        }

        public bool RemoveKey(KLEPKeyFact fact)
        {
            EnsureExternalMutationAllowed();
            if (fact == null)
            {
                throw new ArgumentNullException(nameof(fact));
            }

            return GetStore(fact.Scope).StageRemove(fact);
        }

        public KLEPKeyFact ReplaceKey(
            KLEPKeyFact current,
            KLEPKeyPayload payload,
            KLEPKeyLifetime? lifetime = null,
            string sourceId = "")
        {
            EnsureExternalMutationAllowed();
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            return GetStore(current.Scope).ReplaceAndStage(
                current, payload, lifetime, sourceId);
        }

        public KLEPDecisionTrace Tick(float certaintyThreshold = 0f)
        {
            return Tick(certaintyThreshold, null);
        }

        internal KLEPDecisionTrace Tick(
            float certaintyThreshold,
            KLEPGuidanceAdvice guidanceAdvice)
        {
            if (float.IsNaN(certaintyThreshold) || float.IsInfinity(certaintyThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(certaintyThreshold));
            }

            if (isTicking)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' cannot Tick recursively.");
            }

            isTicking = true;
            var tandemWaves = new List<KLEPTandemWaveTrace>();
            var soloCandidates = new List<CandidateEvaluation>();
            var executionSteps = new List<KLEPExecutableStepTrace>();
            KLEPKeySnapshot initialSnapshot = KLEPKeySnapshot.Empty;
            KLEPKeySnapshot workingSnapshot = KLEPKeySnapshot.Empty;
            string selectedExecutableId = null;
            KLEPGuidanceAdviceApplicationTrace guidanceApplication = null;
            string faultExecutableId = string.Empty;
            KLEPExecutableLifecycleStage faultStage =
                KLEPExecutableLifecycleStage.Tick;

            try
            {
                long nextCycle = ResolveNextCycle();
                ValidateLocalBoundary();
                LocalKeyStore.CommitBoundary(nextCycle);
                CycleIndex = nextCycle;

                initialSnapshot = new KLEPKeySnapshot(
                    CycleIndex, LocalKeyStore, GlobalKeyStore, 0);
                workingSnapshot = initialSnapshot;

                ApplyRemovals(workingSnapshot, executionSteps,
                    ref faultExecutableId, ref faultStage);
                RearmRegisteredRuntimes();

                List<KLEPExecutionResult> initializationResults =
                    ApplyRegistrations(
                        workingSnapshot,
                        executionSteps,
                        ref faultExecutableId,
                        ref faultStage);
                if (initializationResults.Count > 0)
                {
                    faultStage = KLEPExecutableLifecycleStage.OutputApplication;
                    bool localChanged;
                    try
                    {
                        localChanged = ApplyOutputs(
                            initializationResults,
                            workingSnapshot);
                    }
                    catch (KLEPOutputBatchException outputFault)
                    {
                        faultExecutableId = outputFault.SourceExecutableId;
                        RejectRegistrations(initializationResults);
                        Rethrow(outputFault.OriginalException);
                        throw new InvalidOperationException(
                            "Unreachable initialization output fault path.");
                    }

                    if (localChanged)
                    {
                        workingSnapshot = new KLEPKeySnapshot(
                            CycleIndex,
                            LocalKeyStore,
                            GlobalKeyStore,
                            workingSnapshot.WaveIndex + 1);
                    }
                }

                List<KLEPExecutableRuntime> ordered = GetRuntimesInStableOrder();
                SettleTandems(
                    ordered,
                    ref workingSnapshot,
                    tandemWaves,
                    executionSteps,
                    ref faultExecutableId,
                    ref faultStage);

                bool soloAdvanced = AdvanceSolo(
                    ordered,
                    workingSnapshot,
                    certaintyThreshold,
                    guidanceAdvice,
                    soloCandidates,
                    executionSteps,
                    out selectedExecutableId,
                    out guidanceApplication,
                    ref faultExecutableId,
                    ref faultStage);

                bool isPatient = !soloAdvanced && currentSoloExecutableId == null;
                LastTrace = new KLEPDecisionTrace(
                    CycleIndex,
                    initialSnapshot,
                    workingSnapshot,
                    tandemWaves,
                    soloCandidates,
                    executionSteps,
                    selectedExecutableId,
                    currentSoloExecutableId,
                    isPatient,
                    guidanceApplication,
                    null,
                    GetRootExecutableRuntimeSnapshot());
                return LastTrace;
            }
            catch (Exception fault)
            {
                KLEPKeySnapshot traceSnapshot =
                    ReferenceEquals(workingSnapshot, KLEPKeySnapshot.Empty)
                        ? initialSnapshot
                        : workingSnapshot;
                if (guidanceAdvice != null && guidanceApplication == null)
                {
                    guidanceApplication =
                        new KLEPGuidanceAdviceApplicationTrace(
                            guidanceAdvice,
                            KLEPGuidanceAdviceApplicationKind
                                .TickFaultedBeforeApplication,
                            KLEPKeyEnvironmentSignature.FromSnapshot(
                                traceSnapshot),
                            KLEPGuidanceEvidenceFingerprint.FromSnapshot(
                                traceSnapshot),
                            null,
                            null);
                }

                LastTrace = new KLEPDecisionTrace(
                    CycleIndex,
                    initialSnapshot,
                    traceSnapshot,
                    tandemWaves,
                    soloCandidates,
                    executionSteps,
                    selectedExecutableId,
                    currentSoloExecutableId,
                    selectedExecutableId == null && currentSoloExecutableId == null,
                    guidanceApplication,
                    new KLEPExecutionFaultTrace(
                        faultExecutableId,
                        faultStage,
                        fault),
                    GetRootExecutableRuntimeSnapshot());
                throw;
            }
            finally
            {
                isTicking = false;
            }
        }

        private void ApplyRemovals(
            KLEPKeySnapshot snapshot,
            List<KLEPExecutableStepTrace> executionSteps,
            ref string faultExecutableId,
            ref KLEPExecutableLifecycleStage faultStage)
        {
            var removalIds = new List<string>(pendingRemovals);
            removalIds.Sort(IdComparer);
            pendingRemovals.Clear();

            foreach (string stableId in removalIds)
            {
                faultExecutableId = stableId;
                faultStage = KLEPExecutableLifecycleStage.Exit;
                if (runtimes.TryGetValue(
                        stableId, out KLEPExecutableRuntime runtime))
                {
                    try
                    {
                        if (runtime.TryCancel(
                                snapshot,
                                snapshot.WaveIndex,
                                KLEPExecutableExitReason.Removed,
                                out KLEPExecutionResult cancelled))
                        {
                            executionSteps.Add(new KLEPExecutableStepTrace(
                                KLEPExecutableStepKind.Cancellation,
                                cancelled));
                        }
                    }
                    catch
                    {
                        AddFaultResult(runtime, executionSteps,
                            KLEPExecutableStepKind.Cancellation);
                        faultStage = runtime.LastFaultStage ?? faultStage;
                        faultExecutableId = runtime.LastFaultExecutableId ??
                            faultExecutableId;
                        runtime.Executable.ReleaseNeuronOwnership(StableId);
                        runtimes.Remove(stableId);
                        executables.Remove(stableId);
                        if (IdComparer.Equals(currentSoloExecutableId, stableId))
                        {
                            currentSoloExecutableId = null;
                        }

                        throw;
                    }
                }

                if (executables.TryGetValue(
                        stableId, out KLEPExecutableBase removedExecutable))
                {
                    removedExecutable.ReleaseNeuronOwnership(StableId);
                }

                runtimes.Remove(stableId);
                executables.Remove(stableId);
                if (IdComparer.Equals(currentSoloExecutableId, stableId))
                {
                    currentSoloExecutableId = null;
                }
            }
        }

        private List<KLEPExecutionResult> ApplyRegistrations(
            KLEPKeySnapshot snapshot,
            List<KLEPExecutableStepTrace> executionSteps,
            ref string faultExecutableId,
            ref KLEPExecutableLifecycleStage faultStage)
        {
            var registrations = new List<KeyValuePair<string, KLEPExecutableBase>>(
                pendingRegistrations);
            registrations.Sort((left, right) =>
                IdComparer.Compare(left.Key, right.Key));
            pendingRegistrations.Clear();

            var results = new List<KLEPExecutionResult>(registrations.Count);
            foreach (KeyValuePair<string, KLEPExecutableBase> registration in registrations)
            {
                faultExecutableId = registration.Key;
                faultStage = KLEPExecutableLifecycleStage.Initialize;
                var runtime = new KLEPExecutableRuntime(registration.Value);
                registration.Value.ClaimNeuronOwnership(StableId);
                try
                {
                    executables.Add(registration.Key, registration.Value);
                    runtimes.Add(registration.Key, runtime);
                    KLEPExecutionResult result = runtime.Initialize(
                        StableId, snapshot, snapshot.WaveIndex);
                    results.Add(result);
                    executionSteps.Add(new KLEPExecutableStepTrace(
                        KLEPExecutableStepKind.Initialization,
                        result));
                }
                catch
                {
                    AddFaultResult(runtime, executionSteps,
                        KLEPExecutableStepKind.Initialization);
                    faultStage = runtime.LastFaultStage ?? faultStage;
                    faultExecutableId = runtime.LastFaultExecutableId ??
                        faultExecutableId;
                    runtimes.Remove(registration.Key);
                    executables.Remove(registration.Key);
                    registration.Value.ReleaseNeuronOwnership(StableId);
                    throw;
                }
            }

            return results;
        }

        private void SettleTandems(
            IReadOnlyList<KLEPExecutableRuntime> ordered,
            ref KLEPKeySnapshot snapshot,
            List<KLEPTandemWaveTrace> waveTraces,
            List<KLEPExecutableStepTrace> allSteps,
            ref string faultExecutableId,
            ref KLEPExecutableLifecycleStage faultStage)
        {
            var tandems = new List<KLEPExecutableRuntime>();
            foreach (KLEPExecutableRuntime runtime in ordered)
            {
                if (runtime.Executable.ExecutionMode == KLEPExecutionMode.Tandem)
                {
                    tandems.Add(runtime);
                }
            }

            if (tandems.Count == 0)
            {
                return;
            }

            var processed = new HashSet<string>(IdComparer);
            while (processed.Count < tandems.Count)
            {
                KLEPKeySnapshot inputSnapshot = snapshot;
                var evaluations = new List<CandidateEvaluation>();
                var eligible = new List<KLEPExecutableRuntime>();
                var waveSteps = new List<KLEPExecutableStepTrace>();
                var outputResults = new List<KLEPExecutionResult>();

                foreach (KLEPExecutableRuntime runtime in tandems)
                {
                    string stableId = runtime.Executable.StableId;
                    if (processed.Contains(stableId))
                    {
                        continue;
                    }

                    KLEPEligibility eligibility =
                        runtime.Executable.EvaluateEligibility(inputSnapshot);
                    evaluations.Add(new CandidateEvaluation(
                        stableId, eligibility, null));
                    if (eligibility.IsEligible)
                    {
                        eligible.Add(runtime);
                        continue;
                    }

                    if (runtime.State == KLEPExecutableState.Running)
                    {
                        faultExecutableId = stableId;
                        faultStage = KLEPExecutableLifecycleStage.Exit;
                        try
                        {
                            if (runtime.TryCancel(
                                    inputSnapshot,
                                    inputSnapshot.WaveIndex,
                                    KLEPExecutableExitReason.LocksClosed,
                                    out KLEPExecutionResult cancelled))
                            {
                                var step = new KLEPExecutableStepTrace(
                                    KLEPExecutableStepKind.Cancellation,
                                    cancelled);
                                waveSteps.Add(step);
                                allSteps.Add(step);
                            }
                        }
                        catch
                        {
                            AddFaultResult(runtime, waveSteps,
                                KLEPExecutableStepKind.Cancellation);
                            AddLastStepOnce(waveSteps, allSteps);
                            faultStage = runtime.LastFaultStage ?? faultStage;
                            faultExecutableId = runtime.LastFaultExecutableId ??
                                faultExecutableId;
                            throw;
                        }

                        // A cancelled Tandem cannot re-enter this top-level Tick.
                        processed.Add(stableId);
                    }
                }

                if (eligible.Count == 0)
                {
                    waveTraces.Add(new KLEPTandemWaveTrace(
                        inputSnapshot,
                        evaluations,
                        waveSteps,
                        inputSnapshot,
                        false,
                        KLEPTandemWaveTermination.NoEligibleTandem));
                    break;
                }

                foreach (KLEPExecutableRuntime runtime in eligible)
                {
                    string stableId = runtime.Executable.StableId;
                    faultExecutableId = stableId;
                    faultStage = runtime.State == KLEPExecutableState.Idle
                        ? KLEPExecutableLifecycleStage.Enter
                        : KLEPExecutableLifecycleStage.Tick;
                    try
                    {
                        KLEPExecutionResult result = runtime.Advance(
                            inputSnapshot, inputSnapshot.WaveIndex);
                        outputResults.Add(result);
                        var step = new KLEPExecutableStepTrace(
                            KLEPExecutableStepKind.Tandem,
                            result);
                        waveSteps.Add(step);
                        allSteps.Add(step);
                    }
                    catch (Exception fault)
                    {
                        AddFaultResult(runtime, waveSteps,
                            KLEPExecutableStepKind.Tandem);
                        AddLastStepOnce(waveSteps, allSteps);
                        faultStage = runtime.LastFaultStage ?? faultStage;
                        faultExecutableId = runtime.LastFaultExecutableId ??
                            faultExecutableId;
                        try
                        {
                            AbortRunningWavePeers(
                                outputResults,
                                inputSnapshot,
                                waveSteps,
                                allSteps,
                                exceptExecutableId: stableId);
                        }
                        catch (Exception unwindFault)
                        {
                            throw new AggregateException(
                                "A Tandem faulted and an earlier wave peer also " +
                                "faulted while unwinding.",
                                fault,
                                unwindFault);
                        }

                        throw;
                    }

                    processed.Add(stableId);
                }

                faultStage = KLEPExecutableLifecycleStage.OutputApplication;
                bool localChanged;
                try
                {
                    localChanged = ApplyOutputs(outputResults, inputSnapshot);
                }
                catch (KLEPOutputBatchException outputFault)
                {
                    faultExecutableId = outputFault.SourceExecutableId;
                    KLEPExecutableRuntime faultingRuntime = FindRuntime(
                        outputFault.RootExecutableId);
                    Exception effectiveFault = outputFault.OriginalException;
                    try
                    {
                        AbortRunningWavePeers(
                            outputResults,
                            inputSnapshot,
                            waveSteps,
                            allSteps,
                            exceptExecutableId: outputFault.RootExecutableId);
                    }
                    catch (Exception unwindFault)
                    {
                        effectiveFault = new AggregateException(
                            "An output batch faulted and a Tandem peer also " +
                            "faulted while unwinding.",
                            outputFault.OriginalException,
                            unwindFault);
                    }

                    try
                    {
                        faultingRuntime.FaultAfterOutputApplication(
                            inputSnapshot,
                            inputSnapshot.WaveIndex,
                            effectiveFault,
                            outputFault.SourceExecutableId);
                    }
                    catch
                    {
                        AddFaultResult(
                            faultingRuntime,
                            waveSteps,
                            KLEPExecutableStepKind.Tandem);
                        AddLastStepOnce(waveSteps, allSteps);
                        faultStage = faultingRuntime.LastFaultStage ?? faultStage;
                        faultExecutableId =
                            faultingRuntime.LastFaultExecutableId ?? faultExecutableId;
                        throw;
                    }

                    throw new InvalidOperationException(
                        "Unreachable Tandem output fault path.");
                }
                if (localChanged)
                {
                    snapshot = new KLEPKeySnapshot(
                        CycleIndex,
                        LocalKeyStore,
                        GlobalKeyStore,
                        inputSnapshot.WaveIndex + 1);
                }

                KLEPTandemWaveTermination termination;
                if (processed.Count == tandems.Count)
                {
                    termination = KLEPTandemWaveTermination.AllTandemsProcessed;
                }
                else if (localChanged)
                {
                    termination = KLEPTandemWaveTermination.LocalStateChanged;
                }
                else
                {
                    termination = KLEPTandemWaveTermination.NoLocalStateChange;
                }

                waveTraces.Add(new KLEPTandemWaveTrace(
                    inputSnapshot,
                    evaluations,
                    waveSteps,
                    snapshot,
                    localChanged,
                    termination));

                if (!localChanged || processed.Count == tandems.Count)
                {
                    break;
                }
            }

        }

        private bool AdvanceSolo(
            IReadOnlyList<KLEPExecutableRuntime> ordered,
            KLEPKeySnapshot snapshot,
            float certaintyThreshold,
            KLEPGuidanceAdvice guidanceAdvice,
            List<CandidateEvaluation> candidates,
            List<KLEPExecutableStepTrace> allSteps,
            out string selectedExecutableId,
            out KLEPGuidanceAdviceApplicationTrace guidanceApplication,
            ref string faultExecutableId,
            ref KLEPExecutableLifecycleStage faultStage)
        {
            selectedExecutableId = null;
            guidanceApplication = null;
            var solos = new List<KLEPExecutableRuntime>();
            var candidateById = new Dictionary<string, CandidateEvaluation>(IdComparer);
            foreach (KLEPExecutableRuntime runtime in ordered)
            {
                if (runtime.Executable.ExecutionMode != KLEPExecutionMode.Solo)
                {
                    continue;
                }

                solos.Add(runtime);
                KLEPEligibility eligibility =
                    runtime.Executable.EvaluateEligibility(snapshot);
                KLEPExecutableScoreEvaluation score = eligibility.IsEligible
                    ? runtime.Executable.EvaluateScore(snapshot)
                    : null;
                var candidate = new CandidateEvaluation(
                    runtime.Executable.StableId,
                    eligibility,
                    score);
                candidates.Add(candidate);
                candidateById.Add(candidate.StableId, candidate);
            }

            ApplyGuidanceAdvice(
                snapshot,
                guidanceAdvice,
                solos,
                candidates,
                candidateById,
                out guidanceApplication);

            KLEPExecutableRuntime current = null;
            if (currentSoloExecutableId != null)
            {
                foreach (KLEPExecutableRuntime runtime in solos)
                {
                    if (IdComparer.Equals(
                            runtime.Executable.StableId,
                            currentSoloExecutableId) &&
                        runtime.State == KLEPExecutableState.Running)
                    {
                        current = runtime;
                        break;
                    }
                }

                if (current == null)
                {
                    currentSoloExecutableId = null;
                }
            }

            if (current != null)
            {
                CandidateEvaluation currentCandidate =
                    candidateById[current.Executable.StableId];
                KLEPExecutableExitReason? cancelReason = null;
                if (!currentCandidate.IsEligible)
                {
                    cancelReason = KLEPExecutableExitReason.LocksClosed;
                }
                else if (!currentCandidate.Score.HasValue ||
                         currentCandidate.Score.Value <= certaintyThreshold)
                {
                    cancelReason = KLEPExecutableExitReason.BelowThreshold;
                }

                if (cancelReason.HasValue)
                {
                    CancelSolo(
                        current,
                        snapshot,
                        cancelReason.Value,
                        allSteps,
                        ref faultExecutableId,
                        ref faultStage);
                    current = null;
                    currentSoloExecutableId = null;
                }
            }

            KLEPExecutableRuntime selected = current;
            float selectedScore = current == null
                ? float.NegativeInfinity
                : candidateById[current.Executable.StableId].Score.Value;

            foreach (KLEPExecutableRuntime runtime in solos)
            {
                CandidateEvaluation candidate =
                    candidateById[runtime.Executable.StableId];
                if (!candidate.IsEligible || !candidate.Score.HasValue ||
                    candidate.Score.Value <= certaintyThreshold)
                {
                    continue;
                }

                // Stable-ID ordering breaks ties only when there is no current
                // Solo. A Running current is interrupted strictly above score.
                if (selected == null || candidate.Score.Value > selectedScore)
                {
                    selected = runtime;
                    selectedScore = candidate.Score.Value;
                }
            }

            if (current != null && selected != null &&
                !ReferenceEquals(current, selected))
            {
                CancelSolo(
                    current,
                    snapshot,
                    KLEPExecutableExitReason.Interrupted,
                    allSteps,
                    ref faultExecutableId,
                    ref faultStage);
                currentSoloExecutableId = null;
            }

            if (selected == null)
            {
                return false;
            }

            selectedExecutableId = selected.Executable.StableId;
            faultExecutableId = selectedExecutableId;
            faultStage = selected.State == KLEPExecutableState.Idle
                ? KLEPExecutableLifecycleStage.Enter
                : KLEPExecutableLifecycleStage.Tick;
            KLEPExecutionResult selectedResult;
            try
            {
                selectedResult = selected.Advance(snapshot, snapshot.WaveIndex);
                allSteps.Add(new KLEPExecutableStepTrace(
                    KLEPExecutableStepKind.Solo,
                    selectedResult));
            }
            catch
            {
                AddFaultResult(selected, allSteps, KLEPExecutableStepKind.Solo);
                faultStage = selected.LastFaultStage ?? faultStage;
                faultExecutableId = selected.LastFaultExecutableId ??
                    faultExecutableId;
                currentSoloExecutableId = null;
                throw;
            }

            // Solo output is deliberately left staged. It becomes visible at
            // the following top-level Local boundary, never in this snapshot.
            faultStage = KLEPExecutableLifecycleStage.OutputApplication;
            try
            {
                ApplyOutputs(
                    new[] { selectedResult },
                    snapshot,
                    publishLocalWithinBoundary: false);
            }
            catch (KLEPOutputBatchException outputFault)
            {
                faultExecutableId = outputFault.SourceExecutableId;
                currentSoloExecutableId = null;
                try
                {
                    selected.FaultAfterOutputApplication(
                        snapshot,
                        snapshot.WaveIndex,
                        outputFault.OriginalException,
                        outputFault.SourceExecutableId);
                }
                catch
                {
                    AddFaultResult(
                        selected,
                        allSteps,
                        KLEPExecutableStepKind.Solo);
                    faultStage = selected.LastFaultStage ?? faultStage;
                    faultExecutableId =
                        selected.LastFaultExecutableId ?? faultExecutableId;
                    throw;
                }

                throw new InvalidOperationException(
                    "Unreachable Solo output fault path.");
            }

            currentSoloExecutableId =
                selectedResult.State == KLEPExecutableState.Running
                    ? selectedExecutableId
                    : null;
            return true;
        }

        private static void ApplyGuidanceAdvice(
            KLEPKeySnapshot snapshot,
            KLEPGuidanceAdvice advice,
            IReadOnlyList<KLEPExecutableRuntime> solos,
            List<CandidateEvaluation> candidates,
            Dictionary<string, CandidateEvaluation> candidateById,
            out KLEPGuidanceAdviceApplicationTrace application)
        {
            application = null;
            if (advice == null)
            {
                return;
            }

            KLEPKeyEnvironmentSignature observed =
                KLEPKeyEnvironmentSignature.FromSnapshot(snapshot);
            KLEPGuidanceEvidenceFingerprint observedEvidence =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(snapshot);
            if (!advice.Environment.Equals(observed))
            {
                application = new KLEPGuidanceAdviceApplicationTrace(
                    advice,
                    KLEPGuidanceAdviceApplicationKind.StaleEnvironment,
                    observed,
                    observedEvidence,
                    null,
                    null);
                return;
            }

            if (advice.EvidenceFingerprint != null &&
                !advice.EvidenceFingerprint.Equals(observedEvidence))
            {
                application = new KLEPGuidanceAdviceApplicationTrace(
                    advice,
                    KLEPGuidanceAdviceApplicationKind.StaleEvidence,
                    observed,
                    observedEvidence,
                    null,
                    null);
                return;
            }

            if (!candidateById.TryGetValue(
                    advice.TargetExecutableId, out CandidateEvaluation target))
            {
                application = new KLEPGuidanceAdviceApplicationTrace(
                    advice,
                    KLEPGuidanceAdviceApplicationKind.TargetMissing,
                    observed,
                    observedEvidence,
                    null,
                    null);
                return;
            }

            if (!target.IsEligible || target.ScoreEvaluation == null)
            {
                application = new KLEPGuidanceAdviceApplicationTrace(
                    advice,
                    KLEPGuidanceAdviceApplicationKind.TargetIneligible,
                    observed,
                    observedEvidence,
                    target.Score,
                    null);
                return;
            }

            if (advice.TargetRegistrationToken != null)
            {
                KLEPExecutableRuntime currentRegistration = null;
                for (int index = 0; index < solos.Count; index++)
                {
                    if (IdComparer.Equals(
                            solos[index].Executable.StableId,
                            advice.TargetExecutableId))
                    {
                        currentRegistration = solos[index];
                        break;
                    }
                }

                if (!ReferenceEquals(
                        advice.TargetRegistrationToken,
                        currentRegistration))
                {
                    application = new KLEPGuidanceAdviceApplicationTrace(
                        advice,
                        KLEPGuidanceAdviceApplicationKind
                            .TargetRegistrationChanged,
                        observed,
                        observedEvidence,
                        target.Score,
                        null);
                    return;
                }
            }

            float authoredScore = target.Score.Value;
            KLEPExecutableScoreEvaluation influenced =
                target.ScoreEvaluation.WithObserverInfluence(
                    advice.ObserverStableId,
                    advice.ScoreDelta);
            CandidateEvaluation adjusted = target.WithScore(influenced);
            candidateById[advice.TargetExecutableId] = adjusted;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (IdComparer.Equals(
                        candidates[index].StableId,
                        advice.TargetExecutableId))
                {
                    candidates[index] = adjusted;
                    break;
                }
            }

            application = new KLEPGuidanceAdviceApplicationTrace(
                advice,
                KLEPGuidanceAdviceApplicationKind.Applied,
                observed,
                observedEvidence,
                authoredScore,
                influenced.Total);
        }

        private void CancelSolo(
            KLEPExecutableRuntime runtime,
            KLEPKeySnapshot snapshot,
            KLEPExecutableExitReason reason,
            List<KLEPExecutableStepTrace> allSteps,
            ref string faultExecutableId,
            ref KLEPExecutableLifecycleStage faultStage)
        {
            faultExecutableId = runtime.Executable.StableId;
            faultStage = KLEPExecutableLifecycleStage.Exit;
            try
            {
                if (runtime.TryCancel(
                        snapshot,
                        snapshot.WaveIndex,
                        reason,
                        out KLEPExecutionResult result))
                {
                    allSteps.Add(new KLEPExecutableStepTrace(
                        KLEPExecutableStepKind.Cancellation,
                        result));
                }
            }
            catch
            {
                AddFaultResult(runtime, allSteps,
                    KLEPExecutableStepKind.Cancellation);
                faultStage = runtime.LastFaultStage ?? faultStage;
                faultExecutableId = runtime.LastFaultExecutableId ??
                    faultExecutableId;
                if (IdComparer.Equals(
                        currentSoloExecutableId,
                        runtime.Executable.StableId))
                {
                    currentSoloExecutableId = null;
                }

                throw;
            }
        }

        private bool ApplyOutputs(
            IEnumerable<KLEPExecutionResult> results,
            KLEPKeySnapshot inputSnapshot,
            bool publishLocalWithinBoundary = true)
        {
            var outputs = new List<BufferedOutput>();
            foreach (KLEPExecutionResult result in results)
            {
                foreach (KLEPExecutableOutput output in result.Outputs)
                {
                    outputs.Add(new BufferedOutput(
                        result.ExecutableStableId,
                        output));
                }
            }

            if (outputs.Count == 0)
            {
                return false;
            }

            BufferedOutput current = outputs[0];
            try
            {
                var targetedFacts = new HashSet<KLEPKeyOccurrenceId>();
                var additionScopes = new Dictionary<KLEPKeyId, KLEPKeyScope>();
                var allocations = new Dictionary<KLEPKeyStore, int>();
                foreach (BufferedOutput buffered in outputs)
                {
                    current = buffered;
                    KLEPExecutableOutput output = buffered.Output;
                    KLEPKeyStore store = GetStore(output.Scope);
                    if (output.Kind == KLEPExecutableOutputKind.Remove ||
                        output.Kind == KLEPExecutableOutputKind.Replace)
                    {
                        if (!store.CanStageExact(output.Target) ||
                            !SnapshotContainsExact(inputSnapshot, output.Target))
                        {
                            throw new InvalidOperationException(
                                $"Executable '{output.SourceExecutableId}' cannot " +
                                $"{output.Kind} Key occurrence " +
                                $"'{output.Target.OccurrenceId}' because it is not " +
                                "an exact available fact owned by the target store.");
                        }

                        if (!targetedFacts.Add(output.Target.OccurrenceId))
                        {
                            throw new InvalidOperationException(
                                $"One output batch targets Key occurrence " +
                                $"'{output.Target.OccurrenceId}' more than once. " +
                                "Conflicting exact-fact operations are not applied.");
                        }
                    }

                    if (output.Kind == KLEPExecutableOutputKind.Add)
                    {
                        ValidateNoCrossScopeCollision(
                            output,
                            inputSnapshot,
                            additionScopes);
                    }

                    if (output.Kind == KLEPExecutableOutputKind.Add ||
                        output.Kind == KLEPExecutableOutputKind.Replace)
                    {
                        allocations.TryGetValue(store, out int count);
                        allocations[store] = count + 1;
                    }
                }

                foreach (KeyValuePair<KLEPKeyStore, int> allocation in allocations)
                {
                    if (!allocation.Key.CanAllocateOccurrences(allocation.Value))
                    {
                        throw new InvalidOperationException(
                            $"KeyStore '{allocation.Key.StableId}' cannot allocate " +
                            $"{allocation.Value} output occurrences.");
                    }
                }

                KLEPKeyStore.PendingCheckpoint localCheckpoint =
                    LocalKeyStore.CapturePendingCheckpoint();
                KLEPKeyStore.PendingCheckpoint globalCheckpoint =
                    GlobalKeyStore?.CapturePendingCheckpoint();
                bool hasLocalOutput = false;
                try
                {
                    foreach (BufferedOutput buffered in outputs)
                    {
                        current = buffered;
                        KLEPExecutableOutput output = buffered.Output;
                        KLEPKeyStore store = GetStore(output.Scope);
                        switch (output.Kind)
                        {
                            case KLEPExecutableOutputKind.Add:
                                store.CreateAndStage(
                                    output.Definition,
                                    output.Payload,
                                    sourceId: output.SourceExecutableId);
                                break;

                            case KLEPExecutableOutputKind.Remove:
                                if (!store.StageRemove(output.Target))
                                {
                                    throw new InvalidOperationException(
                                        $"Key occurrence " +
                                        $"'{output.Target.OccurrenceId}' could not " +
                                        "be staged for exact removal.");
                                }

                                break;

                            case KLEPExecutableOutputKind.Replace:
                                store.ReplaceAndStage(
                                    output.Target,
                                    output.Payload,
                                    sourceId: output.SourceExecutableId);
                                break;

                            default:
                                throw new InvalidOperationException(
                                    $"Unknown Executable output kind " +
                                    $"'{output.Kind}'.");
                        }

                        if (output.Scope == KLEPKeyScope.Local)
                        {
                            hasLocalOutput = true;
                        }
                    }

                    return publishLocalWithinBoundary && hasLocalOutput &&
                        LocalKeyStore.CommitWithinBoundary(CycleIndex);
                }
                catch
                {
                    LocalKeyStore.RestorePendingCheckpoint(localCheckpoint);
                    if (GlobalKeyStore != null)
                    {
                        GlobalKeyStore.RestorePendingCheckpoint(globalCheckpoint);
                    }

                    throw;
                }
            }
            catch (KLEPOutputBatchException)
            {
                throw;
            }
            catch (Exception fault)
            {
                throw new KLEPOutputBatchException(
                    current.RootExecutableId,
                    current.Output.SourceExecutableId,
                    fault);
            }
        }

        private void ValidateNoCrossScopeCollision(
            KLEPExecutableOutput output,
            KLEPKeySnapshot inputSnapshot,
            Dictionary<KLEPKeyId, KLEPKeyScope> additionScopes)
        {
            if (additionScopes.TryGetValue(
                    output.KeyId, out KLEPKeyScope batchScope) &&
                batchScope != output.Scope)
            {
                throw new InvalidOperationException(
                    $"Output batch adds Key '{output.KeyId}' in both Local and " +
                    "Global scope.");
            }

            additionScopes[output.KeyId] = output.Scope;
            foreach (KLEPKeyFact fact in inputSnapshot.Facts)
            {
                if (fact.KeyId == output.KeyId && fact.Scope != output.Scope)
                {
                    throw new InvalidOperationException(
                        $"Executable '{output.SourceExecutableId}' cannot add " +
                        $"{output.Scope} Key '{output.KeyId}' while the same stable " +
                        $"Key ID is visible as {fact.Scope}.");
                }
            }

            KLEPKeyStore oppositeStore = output.Scope == KLEPKeyScope.Local
                ? GlobalKeyStore
                : LocalKeyStore;
            if (oppositeStore != null &&
                oppositeStore.HasPendingAddition(output.KeyId))
            {
                throw new InvalidOperationException(
                    $"Executable '{output.SourceExecutableId}' cannot add " +
                    $"{output.Scope} Key '{output.KeyId}' while an opposite-scope " +
                    "occurrence is already pending.");
            }
        }

        private void RejectRegistrations(
            IEnumerable<KLEPExecutionResult> initializationResults)
        {
            var rejectedIds = new HashSet<string>(IdComparer);
            foreach (KLEPExecutionResult result in initializationResults)
            {
                if (!rejectedIds.Add(result.ExecutableStableId))
                {
                    continue;
                }

                if (executables.TryGetValue(
                        result.ExecutableStableId,
                        out KLEPExecutableBase executable))
                {
                    executable.ReleaseNeuronOwnership(StableId);
                }

                runtimes.Remove(result.ExecutableStableId);
                executables.Remove(result.ExecutableStableId);
                if (IdComparer.Equals(
                        currentSoloExecutableId,
                        result.ExecutableStableId))
                {
                    currentSoloExecutableId = null;
                }
            }
        }

        private void AbortRunningWavePeers(
            IEnumerable<KLEPExecutionResult> results,
            KLEPKeySnapshot snapshot,
            List<KLEPExecutableStepTrace> waveSteps,
            List<KLEPExecutableStepTrace> allSteps,
            string exceptExecutableId)
        {
            var unwindFaults = new List<Exception>();
            foreach (KLEPExecutionResult result in results)
            {
                if (IdComparer.Equals(
                        result.ExecutableStableId,
                        exceptExecutableId))
                {
                    continue;
                }

                KLEPExecutableRuntime runtime = FindRuntime(
                    result.ExecutableStableId);
                if (runtime.State != KLEPExecutableState.Running)
                {
                    continue;
                }

                try
                {
                    if (runtime.TryCancel(
                            snapshot,
                            snapshot.WaveIndex,
                            KLEPExecutableExitReason.WaveAborted,
                            out KLEPExecutionResult cancelled))
                    {
                        var step = new KLEPExecutableStepTrace(
                            KLEPExecutableStepKind.Cancellation,
                            cancelled);
                        waveSteps.Add(step);
                        allSteps.Add(step);
                    }
                }
                catch (Exception fault)
                {
                    if (runtime.LastResult != null)
                    {
                        var step = new KLEPExecutableStepTrace(
                            KLEPExecutableStepKind.Cancellation,
                            runtime.LastResult);
                        waveSteps.Add(step);
                        allSteps.Add(step);
                    }

                    unwindFaults.Add(fault);
                }
            }

            if (unwindFaults.Count == 1)
            {
                Rethrow(unwindFaults[0]);
            }

            if (unwindFaults.Count > 1)
            {
                throw new AggregateException(
                    "Several Tandem peers faulted while an aborted wave unwound.",
                    unwindFaults);
            }
        }

        private KLEPExecutableRuntime FindRuntime(string executableStableId)
        {
            if (string.IsNullOrWhiteSpace(executableStableId) ||
                !runtimes.TryGetValue(
                    executableStableId, out KLEPExecutableRuntime runtime))
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' does not own Executable runtime " +
                    $"'{executableStableId}'.");
            }

            return runtime;
        }

        private void RearmRegisteredRuntimes()
        {
            foreach (KLEPExecutableRuntime runtime in runtimes.Values)
            {
                runtime.Rearm(CycleIndex);
            }
        }

        private List<KLEPExecutableRuntime> GetRuntimesInStableOrder()
        {
            var ordered = new List<KLEPExecutableRuntime>(runtimes.Values);
            ordered.Sort((left, right) => IdComparer.Compare(
                left.Executable.StableId,
                right.Executable.StableId));
            return ordered;
        }

        private long ResolveNextCycle()
        {
            long minimumNextCycle = CheckedNextCycle();
            if (GlobalKeyStore == null)
            {
                return minimumNextCycle;
            }

            if (GlobalKeyStore.LastCommittedTick < minimumNextCycle)
            {
                throw new InvalidOperationException(
                    $"Global KeyStore '{GlobalKeyStore.StableId}' is at boundary " +
                    $"{GlobalKeyStore.LastCommittedTick}, but Neuron '{StableId}' needs " +
                    $"at least boundary {minimumNextCycle}. Commit the shared Global " +
                    "store in the world/coordinator before ticking this Neuron.");
            }

            return GlobalKeyStore.LastCommittedTick;
        }

        private void ValidateLocalBoundary()
        {
            long expectedLocalBoundary = CycleIndex == 0 ? -1 : CycleIndex;
            if (LocalKeyStore.LastCommittedTick != expectedLocalBoundary)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' expected its Local KeyStore at boundary " +
                    $"{expectedLocalBoundary}, but found " +
                    $"{LocalKeyStore.LastCommittedTick}. Only the Neuron may advance " +
                    "its Local KeyStore.");
            }
        }

        private static bool SnapshotContainsExact(
            KLEPKeySnapshot snapshot,
            KLEPKeyFact target)
        {
            foreach (KLEPKeyFact fact in snapshot.Facts)
            {
                if (fact.OccurrenceId == target.OccurrenceId &&
                    fact.KeyId == target.KeyId &&
                    fact.Scope == target.Scope)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFaultResult(
            KLEPExecutableRuntime runtime,
            List<KLEPExecutableStepTrace> destination,
            KLEPExecutableStepKind kind)
        {
            if (runtime.LastResult != null)
            {
                destination.Add(new KLEPExecutableStepTrace(
                    kind, runtime.LastResult));
            }
        }

        private static void AddLastStepOnce(
            List<KLEPExecutableStepTrace> source,
            List<KLEPExecutableStepTrace> destination)
        {
            if (source.Count == 0)
            {
                return;
            }

            KLEPExecutableStepTrace last = source[source.Count - 1];
            if (destination.Count == 0 ||
                !ReferenceEquals(destination[destination.Count - 1], last))
            {
                destination.Add(last);
            }
        }

        private static void ValidateStableId(string stableId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }
        }

        private void ValidateCanInitialize(KLEPKeyDefinition definition)
        {
            if (CycleIndex != 0)
            {
                throw new InvalidOperationException(
                    "Initial keys can only be supplied before the first Tick.");
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            KLEPKeyStore store = GetStore(definition.Scope);
            if (store.Scope == KLEPKeyScope.Global && store.LastCommittedTick >= 0)
            {
                throw new InvalidOperationException(
                    $"Global KeyStore '{store.StableId}' has already committed " +
                    $"boundary {store.LastCommittedTick}. Initial Global Keys must " +
                    "be staged before the world begins; use AddKey for later " +
                    "Global emissions.");
            }
        }

        private long CheckedNextCycle()
        {
            if (CycleIndex == long.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Neuron '{StableId}' exhausted its cycle counter.");
            }

            return CycleIndex + 1;
        }

        private KLEPKeyStore GetStore(KLEPKeyScope scope)
        {
            if (scope == KLEPKeyScope.Local)
            {
                return LocalKeyStore;
            }

            return GlobalKeyStore ?? throw new InvalidOperationException(
                $"Neuron '{StableId}' requires an explicitly injected Global " +
                "KeyStore before it can add, remove, or replace Global Keys.");
        }

        private void EnsureExternalMutationAllowed()
        {
            if (isTicking)
            {
                throw new InvalidOperationException(
                    "Direct Key mutation is not allowed during Neuron.Tick. " +
                    "Executable callbacks must emit buffered operations through " +
                    "their lifecycle context.");
            }
        }

        private static void Rethrow(Exception fault)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
        }

        private readonly struct BufferedOutput
        {
            internal BufferedOutput(
                string rootExecutableId,
                KLEPExecutableOutput output)
            {
                RootExecutableId = rootExecutableId ?? string.Empty;
                Output = output ?? throw new ArgumentNullException(nameof(output));
            }

            internal string RootExecutableId { get; }
            internal KLEPExecutableOutput Output { get; }
        }

        private sealed class KLEPOutputBatchException : Exception
        {
            internal KLEPOutputBatchException(
                string rootExecutableId,
                string sourceExecutableId,
                Exception originalException)
                : base(originalException == null ? string.Empty : originalException.Message,
                    originalException)
            {
                RootExecutableId = rootExecutableId ?? string.Empty;
                SourceExecutableId = sourceExecutableId ?? RootExecutableId;
                OriginalException = originalException ??
                    throw new ArgumentNullException(nameof(originalException));
            }

            internal string RootExecutableId { get; }
            internal string SourceExecutableId { get; }
            internal Exception OriginalException { get; }
        }
    }
}
