using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// One Goal-owned child frozen at the same boundary as its owning
    /// Neuron's decision trace. The runtime object itself is never exposed.
    /// </summary>
    public sealed class KLEPGoalChildRuntimeSnapshot
    {
        internal KLEPGoalChildRuntimeSnapshot(
            KLEPExecutableRuntimeSnapshot runtime,
            bool completedInCurrentLayer)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            CompletedInCurrentLayer = completedInCurrentLayer;
        }

        public KLEPExecutableRuntimeSnapshot Runtime { get; }
        public string ExecutableStableId => Runtime.ExecutableStableId;
        public bool CompletedInCurrentLayer { get; }
    }

    /// <summary>
    /// Authored Goal layer structure plus the frozen runtime state of every
    /// child in that layer.
    /// </summary>
    public sealed class KLEPGoalLayerRuntimeSnapshot
    {
        private readonly ReadOnlyCollection<KLEPGoalChildRuntimeSnapshot> children;

        internal KLEPGoalLayerRuntimeSnapshot(
            int layerIndex,
            KLEPGoalLayerRequirement requirement,
            IEnumerable<KLEPGoalChildRuntimeSnapshot> children)
        {
            LayerIndex = layerIndex;
            Requirement = requirement;
            this.children = Copy(children, nameof(children));
        }

        public int LayerIndex { get; }
        public KLEPGoalLayerRequirement Requirement { get; }
        public IReadOnlyList<KLEPGoalChildRuntimeSnapshot> Children => children;

        private static ReadOnlyCollection<KLEPGoalChildRuntimeSnapshot> Copy(
            IEnumerable<KLEPGoalChildRuntimeSnapshot> source,
            string parameterName)
        {
            var copy = new List<KLEPGoalChildRuntimeSnapshot>();
            if (source != null)
            {
                foreach (KLEPGoalChildRuntimeSnapshot child in source)
                {
                    copy.Add(child ?? throw new ArgumentException(
                        "Goal diagnostic layers cannot contain null children.",
                        parameterName));
                }
            }

            return new ReadOnlyCollection<KLEPGoalChildRuntimeSnapshot>(copy);
        }
    }

    /// <summary>
    /// Frozen Goal progress. This is evidence only: it cannot advance a layer
    /// or reach a child lifecycle controller.
    /// </summary>
    public sealed class KLEPGoalRuntimeSnapshot
    {
        private readonly ReadOnlyCollection<KLEPGoalLayerRuntimeSnapshot> layers;
        private readonly ReadOnlyCollection<KLEPExecutionResult> lastChildResults;

        internal KLEPGoalRuntimeSnapshot(
            int currentLayerIndex,
            bool isComplete,
            KLEPKeyId? activationKeyId,
            IEnumerable<KLEPGoalLayerRuntimeSnapshot> layers,
            IEnumerable<KLEPExecutionResult> lastChildResults)
        {
            CurrentLayerIndex = currentLayerIndex;
            IsComplete = isComplete;
            ActivationKeyId = activationKeyId;
            this.layers = CopyLayers(layers);
            this.lastChildResults = CopyResults(lastChildResults);
        }

        public int CurrentLayerIndex { get; }
        public int LayerCount => layers.Count;
        public bool IsComplete { get; }
        public KLEPKeyId? ActivationKeyId { get; }
        public IReadOnlyList<KLEPGoalLayerRuntimeSnapshot> Layers => layers;
        public IReadOnlyList<KLEPExecutionResult> LastChildResults => lastChildResults;

        private static ReadOnlyCollection<KLEPGoalLayerRuntimeSnapshot> CopyLayers(
            IEnumerable<KLEPGoalLayerRuntimeSnapshot> source)
        {
            var copy = new List<KLEPGoalLayerRuntimeSnapshot>();
            if (source != null)
            {
                foreach (KLEPGoalLayerRuntimeSnapshot layer in source)
                {
                    copy.Add(layer ?? throw new ArgumentException(
                        "Goal runtime snapshots cannot contain null layers.",
                        nameof(source)));
                }
            }

            return new ReadOnlyCollection<KLEPGoalLayerRuntimeSnapshot>(copy);
        }

        private static ReadOnlyCollection<KLEPExecutionResult> CopyResults(
            IEnumerable<KLEPExecutionResult> source)
        {
            var copy = new List<KLEPExecutionResult>();
            if (source != null)
            {
                foreach (KLEPExecutionResult result in source)
                {
                    copy.Add(result ?? throw new ArgumentException(
                        "Goal runtime snapshots cannot contain null child results.",
                        nameof(source)));
                }
            }

            return new ReadOnlyCollection<KLEPExecutionResult>(copy);
        }
    }

    /// <summary>
    /// Immutable projection of one internal Executable lifecycle controller.
    /// Exceptions are reduced to strings and no lifecycle-driving handle is
    /// retained.
    /// </summary>
    public sealed class KLEPExecutableRuntimeSnapshot
    {
        internal KLEPExecutableRuntimeSnapshot(
            KLEPExecutableDefinition definition,
            KLEPExecutableState state,
            long runIndex,
            KLEPExecutionResult lastResult,
            bool isCurrentSolo,
            string faultExecutableId,
            KLEPExecutableLifecycleStage? faultStage,
            string faultExceptionType,
            string faultMessage,
            KLEPGoalRuntimeSnapshot goal)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = state;
            RunIndex = runIndex;
            LastResult = lastResult;
            IsCurrentSolo = isCurrentSolo;
            FaultExecutableId = faultExecutableId;
            FaultStage = faultStage;
            FaultExceptionType = faultExceptionType;
            FaultMessage = faultMessage;
            Goal = goal;
        }

        public KLEPExecutableDefinition Definition { get; }
        public string ExecutableStableId => Definition.StableId;
        public KLEPExecutableKind Kind => Definition.Kind;
        public KLEPExecutionMode ExecutionMode => Definition.ExecutionMode;
        public KLEPExecutableState State { get; }
        public long RunIndex { get; }
        public KLEPExecutionResult LastResult { get; }
        public bool IsCurrentSolo { get; }
        public string FaultExecutableId { get; }
        public KLEPExecutableLifecycleStage? FaultStage { get; }
        public string FaultExceptionType { get; }
        public string FaultMessage { get; }
        public bool HasFault => !string.IsNullOrEmpty(FaultExceptionType);
        public KLEPGoalRuntimeSnapshot Goal { get; }
    }

    public readonly struct CandidateEvaluation
    {
        internal CandidateEvaluation(
            string stableId,
            KLEPEligibility eligibility,
            KLEPExecutableScoreEvaluation scoreEvaluation)
        {
            StableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
            Eligibility = eligibility;
            ScoreEvaluation = scoreEvaluation;
        }

        public string StableId { get; }
        public KLEPEligibility Eligibility { get; }
        public bool IsEligible => Eligibility.IsEligible;
        public string Reason => Eligibility.Reason;
        public KLEPExecutableScoreEvaluation ScoreEvaluation { get; }
        public float? Score => ScoreEvaluation == null
            ? (float?)null
            : ScoreEvaluation.Total;

        internal CandidateEvaluation WithScore(
            KLEPExecutableScoreEvaluation scoreEvaluation)
        {
            return new CandidateEvaluation(StableId, Eligibility, scoreEvaluation);
        }
    }

    public enum KLEPExecutableStepKind
    {
        Initialization,
        Tandem,
        Solo,
        Cancellation
    }

    public sealed class KLEPExecutableStepTrace
    {
        internal KLEPExecutableStepTrace(
            KLEPExecutableStepKind kind,
            KLEPExecutionResult result)
        {
            Kind = kind;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public KLEPExecutableStepKind Kind { get; }
        public KLEPExecutionResult Result { get; }
        public string ExecutableStableId => Result.ExecutableStableId;
        public KLEPExecutableState State => Result.State;
        public KLEPExecutableExitReason? ExitReason => Result.ExitReason;
        public IReadOnlyList<KLEPExecutableOutput> Outputs => Result.Outputs;
    }

    public enum KLEPTandemWaveTermination
    {
        LocalStateChanged,
        NoEligibleTandem,
        NoLocalStateChange,
        AllTandemsProcessed
    }

    public sealed class KLEPTandemWaveTrace
    {
        private readonly ReadOnlyCollection<CandidateEvaluation> candidates;
        private readonly ReadOnlyCollection<KLEPExecutableStepTrace> executions;

        internal KLEPTandemWaveTrace(
            KLEPKeySnapshot inputSnapshot,
            IEnumerable<CandidateEvaluation> candidates,
            IEnumerable<KLEPExecutableStepTrace> executions,
            KLEPKeySnapshot outputSnapshot,
            bool didLocalStateChange,
            KLEPTandemWaveTermination termination)
        {
            InputSnapshot = inputSnapshot ??
                throw new ArgumentNullException(nameof(inputSnapshot));
            OutputSnapshot = outputSnapshot ??
                throw new ArgumentNullException(nameof(outputSnapshot));
            this.candidates = Copy(candidates, nameof(candidates));
            this.executions = Copy(executions, nameof(executions));
            DidLocalStateChange = didLocalStateChange;
            Termination = termination;
        }

        public int WaveIndex => InputSnapshot.WaveIndex;
        public KLEPKeySnapshot InputSnapshot { get; }
        public IReadOnlyList<CandidateEvaluation> Candidates => candidates;
        public IReadOnlyList<KLEPExecutableStepTrace> Executions => executions;
        public KLEPKeySnapshot OutputSnapshot { get; }
        public bool DidLocalStateChange { get; }
        public KLEPTandemWaveTermination Termination { get; }

        private static ReadOnlyCollection<T> Copy<T>(
            IEnumerable<T> source,
            string parameterName)
        {
            var copy = new List<T>();
            if (source != null)
            {
                foreach (T item in source)
                {
                    if (ReferenceEquals(item, null))
                    {
                        throw new ArgumentException(
                            "Trace collections cannot contain null.",
                            parameterName);
                    }

                    copy.Add(item);
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class KLEPExecutionFaultTrace
    {
        internal KLEPExecutionFaultTrace(
            string executableStableId,
            KLEPExecutableLifecycleStage stage,
            Exception exception)
        {
            ExecutableStableId = executableStableId ?? string.Empty;
            Stage = stage;
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            ExceptionType = exception.GetType().FullName ??
                exception.GetType().Name;
            Message = exception.Message ?? string.Empty;
        }

        public string ExecutableStableId { get; }
        public KLEPExecutableLifecycleStage Stage { get; }
        public string ExceptionType { get; }
        public string Message { get; }
    }

    public sealed class KLEPDecisionTrace
    {
        private readonly ReadOnlyCollection<KLEPTandemWaveTrace> tandemWaves;
        private readonly ReadOnlyCollection<CandidateEvaluation> candidates;
        private readonly ReadOnlyCollection<KLEPExecutableStepTrace> executions;
        private readonly ReadOnlyCollection<KLEPExecutableRuntimeSnapshot>
            executableStates;

        internal static readonly KLEPDecisionTrace Empty = new KLEPDecisionTrace(
            0,
            KLEPKeySnapshot.Empty,
            KLEPKeySnapshot.Empty,
            Array.Empty<KLEPTandemWaveTrace>(),
            Array.Empty<CandidateEvaluation>(),
            Array.Empty<KLEPExecutableStepTrace>(),
            null,
            null,
            true,
            null,
            null,
            Array.Empty<KLEPExecutableRuntimeSnapshot>());

        internal KLEPDecisionTrace(
            long cycleIndex,
            KLEPKeySnapshot initialKeySnapshot,
            KLEPKeySnapshot keySnapshot,
            IEnumerable<KLEPTandemWaveTrace> tandemWaves,
            IEnumerable<CandidateEvaluation> candidates,
            IEnumerable<KLEPExecutableStepTrace> executions,
            string selectedExecutableId,
            string currentSoloExecutableId,
            bool isPatient,
            KLEPGuidanceAdviceApplicationTrace guidanceAdvice,
            KLEPExecutionFaultTrace fault,
            IEnumerable<KLEPExecutableRuntimeSnapshot> executableStates)
        {
            CycleIndex = cycleIndex;
            InitialKeySnapshot = initialKeySnapshot ??
                throw new ArgumentNullException(nameof(initialKeySnapshot));
            KeySnapshot = keySnapshot ?? throw new ArgumentNullException(nameof(keySnapshot));
            this.tandemWaves = Copy(tandemWaves, nameof(tandemWaves));
            this.candidates = Copy(candidates, nameof(candidates));
            this.executions = Copy(executions, nameof(executions));
            this.executableStates = Copy(
                executableStates,
                nameof(executableStates));
            SelectedExecutableId = selectedExecutableId;
            CurrentSoloExecutableId = currentSoloExecutableId;
            IsPatient = isPatient;
            GuidanceAdvice = guidanceAdvice;
            Fault = fault;
        }

        public long CycleIndex { get; }
        public KLEPKeySnapshot InitialKeySnapshot { get; }
        public KLEPKeySnapshot KeySnapshot { get; }
        public IReadOnlyList<KLEPTandemWaveTrace> TandemWaves => tandemWaves;
        public IReadOnlyList<CandidateEvaluation> Candidates => candidates;
        public IReadOnlyList<KLEPExecutableStepTrace> Executions => executions;
        public IReadOnlyList<KLEPExecutableRuntimeSnapshot> ExecutableStates =>
            executableStates;
        public string SelectedExecutableId { get; }
        public string CurrentSoloExecutableId { get; }
        public bool IsPatient { get; }
        public KLEPGuidanceAdviceApplicationTrace GuidanceAdvice { get; }
        public KLEPExecutionFaultTrace Fault { get; }

        // Retained as a source-compatible diagnostic during the rewrite. The
        // lifecycle is implemented now, so it is never pending.
        public bool LifecyclePending => false;

        private static ReadOnlyCollection<T> Copy<T>(
            IEnumerable<T> source,
            string parameterName)
        {
            var copy = new List<T>();
            if (source != null)
            {
                foreach (T item in source)
                {
                    if (ReferenceEquals(item, null))
                    {
                        throw new ArgumentException(
                            "Trace collections cannot contain null.",
                            parameterName);
                    }

                    copy.Add(item);
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }
}
