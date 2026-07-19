using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace Roll4d4.Klep.Core
{
    public enum KLEPExecutableTickStatus
    {
        Running,
        Succeeded,
        Failed
    }

    public enum KLEPExecutableState
    {
        Uninitialized,
        Idle,
        Running,
        Succeeded,
        Failed,
        Cancelled,
        Faulted
    }

    public enum KLEPExecutableExitReason
    {
        Succeeded,
        Failed,
        LocksClosed,
        BelowThreshold,
        Interrupted,
        WaveAborted,
        Removed,
        Faulted
    }

    public enum KLEPExecutableLifecycleStage
    {
        Initialize,
        Enter,
        Tick,
        Exit,
        Cleanup,
        OutputApplication
    }

    // Goal runners use this internal envelope to preserve the actual child
    // identity while the public caller still receives the original exception.
    internal sealed class KLEPNestedExecutableFaultException : Exception
    {
        internal KLEPNestedExecutableFaultException(
            string executableStableId,
            KLEPExecutableLifecycleStage stage,
            Exception originalException)
            : base(originalException == null ? string.Empty : originalException.Message,
                originalException)
        {
            ExecutableStableId = executableStableId ?? string.Empty;
            Stage = stage;
            OriginalException = originalException ??
                throw new ArgumentNullException(nameof(originalException));
        }

        internal string ExecutableStableId { get; }
        internal KLEPExecutableLifecycleStage Stage { get; }
        internal Exception OriginalException { get; }
    }

    public sealed class KLEPExecutableInitializationContext
    {
        private readonly KLEPExecutionContext outputContext;

        internal KLEPExecutableInitializationContext(
            string ownerStableId,
            KLEPExecutableDefinition definition,
            KLEPKeySnapshot keys,
            int waveIndex)
        {
            if (string.IsNullOrWhiteSpace(ownerStableId))
            {
                throw new ArgumentException(
                    "A non-empty lifecycle owner ID is required.",
                    nameof(ownerStableId));
            }

            OwnerStableId = ownerStableId;
            outputContext = new KLEPExecutionContext(
                definition ?? throw new ArgumentNullException(nameof(definition)),
                keys ?? throw new ArgumentNullException(nameof(keys)),
                waveIndex,
                0);
        }

        public string OwnerStableId { get; }
        public string ExecutableStableId => outputContext.ExecutableStableId;
        public long CycleIndex => outputContext.CycleIndex;
        public int WaveIndex => outputContext.WaveIndex;
        public KLEPKeySnapshot Keys => outputContext.Keys;
        public int PendingOutputCount => outputContext.PendingOutputCount;

        public void Emit(KLEPExecutableOutput output)
        {
            outputContext.Emit(output);
        }

        public void Add(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null)
        {
            outputContext.Add(definition, payload);
        }

        public void Remove(KLEPKeyFact target)
        {
            outputContext.Remove(target);
        }

        public void Replace(KLEPKeyFact target, KLEPKeyPayload payload)
        {
            outputContext.Replace(target, payload);
        }

        internal void ForwardValidated(KLEPExecutableOutput output)
        {
            outputContext.ForwardValidated(output);
        }

        internal IReadOnlyList<KLEPExecutableOutput> Complete()
        {
            return outputContext.Complete();
        }

        internal void Discard()
        {
            outputContext.Discard();
        }
    }

    /// <summary>
    /// One bounded invocation context. Keys are immutable and emitted commands
    /// are buffered in authored order. No KeyStore or Neuron is exposed.
    /// </summary>
    public sealed class KLEPExecutionContext
    {
        private readonly KLEPExecutableDefinition definition;
        private readonly List<KLEPExecutableOutput> outputs =
            new List<KLEPExecutableOutput>();
        private bool isClosed;

        internal KLEPExecutionContext(
            KLEPExecutableDefinition definition,
            KLEPKeySnapshot keys,
            int waveIndex,
            long runIndex)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            if (runIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runIndex));
            }

            ExecutableStableId = definition.StableId;
            CycleIndex = keys.Tick;
            WaveIndex = waveIndex;
            RunIndex = runIndex;
        }

        public string ExecutableStableId { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
        public long RunIndex { get; }
        public KLEPKeySnapshot Keys { get; }
        public int PendingOutputCount => outputs.Count;

        public void Emit(KLEPExecutableOutput output)
        {
            if (isClosed)
            {
                throw new InvalidOperationException(
                    "This Executable invocation has already finished.");
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (output.Kind == KLEPExecutableOutputKind.Add ||
                output.Kind == KLEPExecutableOutputKind.Replace)
            {
                if (!definition.TryGetDeclaredOutput(
                        output.KeyId, out KLEPKeyDefinition declared))
                {
                    throw new InvalidOperationException(
                        $"Executable '{definition.StableId}' emitted undeclared " +
                        $"{output.Kind} output '{output.KeyId}'.");
                }

                if (declared.Scope != output.Scope)
                {
                    throw new InvalidOperationException(
                        $"Executable '{definition.StableId}' declared Key " +
                        $"'{output.KeyId}' as {declared.Scope}, but emitted it as " +
                        $"{output.Scope}.");
                }

                if (output.Kind == KLEPExecutableOutputKind.Add &&
                    !ReferenceEquals(declared, output.Definition))
                {
                    throw new InvalidOperationException(
                        $"Executable '{definition.StableId}' must add its exact " +
                        $"declared definition for Key '{output.KeyId}'.");
                }
            }

            if ((output.Kind == KLEPExecutableOutputKind.Remove ||
                 output.Kind == KLEPExecutableOutputKind.Replace) &&
                !ContainsExactFact(output.Target))
            {
                throw new InvalidOperationException(
                    $"Executable '{definition.StableId}' tried to {output.Kind} " +
                    $"Key occurrence '{output.Target.OccurrenceId}', which is not in " +
                    "its immutable input snapshot.");
            }

            if (!string.IsNullOrEmpty(output.SourceExecutableId))
            {
                throw new InvalidOperationException(
                    "Behavior code cannot forward an operation emitted by another Executable.");
            }

            outputs.Add(output.BindSource(definition.StableId));
        }

        public void Add(
            KLEPKeyDefinition definition,
            KLEPKeyPayload payload = null)
        {
            Emit(KLEPExecutableOutput.Add(definition, payload));
        }

        public void Remove(KLEPKeyFact target)
        {
            Emit(KLEPExecutableOutput.Remove(target));
        }

        public void Replace(
            KLEPKeyFact target,
            KLEPKeyPayload payload)
        {
            Emit(KLEPExecutableOutput.Replace(target, payload));
        }

        // Goal scheduling is the only Core path allowed to forward an already
        // validated child operation. The original child source is preserved.
        internal void ForwardValidated(KLEPExecutableOutput output)
        {
            if (isClosed)
            {
                throw new InvalidOperationException(
                    "This Executable invocation has already finished.");
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (string.IsNullOrWhiteSpace(output.SourceExecutableId))
            {
                throw new InvalidOperationException(
                    "Only a validated child output may be forwarded.");
            }

            outputs.Add(output);
        }

        internal IReadOnlyList<KLEPExecutableOutput> Complete()
        {
            if (isClosed)
            {
                throw new InvalidOperationException(
                    "This Executable invocation has already finished.");
            }

            isClosed = true;
            return new ReadOnlyCollection<KLEPExecutableOutput>(
                new List<KLEPExecutableOutput>(outputs));
        }

        internal void Discard()
        {
            if (isClosed)
            {
                return;
            }

            outputs.Clear();
            isClosed = true;
        }

        private bool ContainsExactFact(KLEPKeyFact target)
        {
            if (target == null || !target.IsActivated)
            {
                return false;
            }

            foreach (KLEPKeyFact fact in Keys.Facts)
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
    }

    public sealed class KLEPExecutableExitContext
    {
        internal KLEPExecutableExitContext(
            string executableStableId,
            KLEPKeySnapshot keys,
            int waveIndex,
            long runIndex,
            KLEPExecutableState terminalState,
            KLEPExecutableExitReason reason,
            Exception fault)
        {
            ExecutableStableId = executableStableId ??
                throw new ArgumentNullException(nameof(executableStableId));
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            CycleIndex = keys.Tick;
            WaveIndex = waveIndex;
            RunIndex = runIndex;
            TerminalState = terminalState;
            Reason = reason;
            Fault = fault;
        }

        public string ExecutableStableId { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
        public long RunIndex { get; }
        public KLEPKeySnapshot Keys { get; }
        public KLEPExecutableState TerminalState { get; }
        public KLEPExecutableExitReason Reason { get; }
        public Exception Fault { get; }
    }

    public sealed class KLEPExecutionResult
    {
        private readonly ReadOnlyCollection<KLEPExecutableOutput> outputs;

        internal KLEPExecutionResult(
            string executableStableId,
            long cycleIndex,
            int waveIndex,
            long runIndex,
            KLEPExecutableState state,
            KLEPExecutableExitReason? exitReason,
            IEnumerable<KLEPExecutableOutput> outputs)
        {
            ExecutableStableId = executableStableId ??
                throw new ArgumentNullException(nameof(executableStableId));
            CycleIndex = cycleIndex;
            WaveIndex = waveIndex;
            RunIndex = runIndex;
            State = state;
            ExitReason = exitReason;

            var copy = new List<KLEPExecutableOutput>();
            if (outputs != null)
            {
                foreach (KLEPExecutableOutput output in outputs)
                {
                    copy.Add(output ?? throw new ArgumentException(
                        "Execution results cannot contain null outputs.",
                        nameof(outputs)));
                }
            }

            this.outputs = new ReadOnlyCollection<KLEPExecutableOutput>(copy);
        }

        public string ExecutableStableId { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
        public long RunIndex { get; }
        public KLEPExecutableState State { get; }
        public KLEPExecutableExitReason? ExitReason { get; }
        public IReadOnlyList<KLEPExecutableOutput> Outputs => outputs;
        public bool IsTerminal => State == KLEPExecutableState.Succeeded ||
            State == KLEPExecutableState.Failed ||
            State == KLEPExecutableState.Cancelled ||
            State == KLEPExecutableState.Faulted;
    }

    /// <summary>
    /// Centralizes lifecycle state so a Neuron or Goal-owned child runner can
    /// enforce exact-once callback ordering. This type is intentionally
    /// internal; behavior subclasses cannot drive their own lifecycle.
    /// </summary>
    internal sealed class KLEPExecutableRuntime
    {
        private readonly KLEPExecutableBase executable;
        private bool exitAttempted;
        private bool cleanupAttempted;
        private long terminalCycleIndex = -1;

        internal KLEPExecutableRuntime(KLEPExecutableBase executable)
        {
            this.executable = executable ?? throw new ArgumentNullException(nameof(executable));
        }

        internal KLEPExecutableBase Executable => executable;
        internal KLEPExecutableState State { get; private set; } =
            KLEPExecutableState.Uninitialized;
        internal long RunIndex { get; private set; }
        internal KLEPExecutionResult LastResult { get; private set; }
        internal Exception LastFault { get; private set; }
        internal KLEPExecutableLifecycleStage? LastFaultStage { get; private set; }
        internal string LastFaultExecutableId { get; private set; }

        internal KLEPExecutableRuntimeSnapshot CaptureSnapshot(
            bool isCurrentSolo = false)
        {
            Exception fault = LastFault;
            return new KLEPExecutableRuntimeSnapshot(
                executable.Definition,
                State,
                RunIndex,
                LastResult,
                isCurrentSolo,
                LastFaultExecutableId,
                LastFaultStage,
                fault == null
                    ? null
                    : fault.GetType().FullName ?? fault.GetType().Name,
                fault == null ? null : fault.Message,
                executable is KLEPGoal goal
                    ? goal.CaptureRuntimeSnapshot()
                    : null);
        }

        internal KLEPExecutionResult Initialize(
            string ownerStableId,
            KLEPKeySnapshot keys,
            int waveIndex)
        {
            if (State != KLEPExecutableState.Uninitialized)
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' has already attempted initialization.");
            }

            var context = new KLEPExecutableInitializationContext(
                ownerStableId,
                executable.Definition,
                keys ?? throw new ArgumentNullException(nameof(keys)),
                waveIndex);

            try
            {
                executable.DispatchInitialize(context);
                IReadOnlyList<KLEPExecutableOutput> outputs = context.Complete();
                State = KLEPExecutableState.Idle;
                LastResult = CreateResult(
                    keys.Tick,
                    waveIndex,
                    KLEPExecutableState.Idle,
                    null,
                    outputs);
                return LastResult;
            }
            catch (Exception fault)
            {
                context.Discard();
                ResolveFault(
                    fault,
                    executable.StableId,
                    KLEPExecutableLifecycleStage.Initialize,
                    out Exception actualFault,
                    out string actualExecutableId,
                    out KLEPExecutableLifecycleStage actualStage);
                State = KLEPExecutableState.Faulted;
                LastFault = actualFault;
                LastFaultStage = actualStage;
                LastFaultExecutableId = actualExecutableId;
                LastResult = CreateResult(
                    keys.Tick,
                    waveIndex,
                    KLEPExecutableState.Faulted,
                    KLEPExecutableExitReason.Faulted,
                    Array.Empty<KLEPExecutableOutput>());
                return Rethrow<KLEPExecutionResult>(actualFault);
            }
        }

        internal void Rearm(long cycleIndex)
        {
            if (cycleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleIndex));
            }

            if (IsTerminal(State) && terminalCycleIndex >= 0 &&
                terminalCycleIndex < cycleIndex)
            {
                State = KLEPExecutableState.Idle;
            }
        }

        internal KLEPExecutionResult Advance(
            KLEPKeySnapshot keys,
            int waveIndex)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (State != KLEPExecutableState.Idle &&
                State != KLEPExecutableState.Running)
            {
                throw new InvalidOperationException(
                    $"Executable '{executable.StableId}' cannot advance from state {State}.");
            }

            bool entering = State == KLEPExecutableState.Idle;
            if (entering)
            {
                if (RunIndex == long.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Executable '{executable.StableId}' exhausted its run counter.");
                }

                RunIndex++;
                exitAttempted = false;
                cleanupAttempted = false;
                LastFault = null;
                LastFaultStage = null;
                LastFaultExecutableId = null;
                State = KLEPExecutableState.Running;
            }

            var context = new KLEPExecutionContext(
                executable.Definition, keys, waveIndex, RunIndex);

            KLEPExecutableTickStatus tickStatus;
            KLEPExecutableLifecycleStage activeStage = entering
                ? KLEPExecutableLifecycleStage.Enter
                : KLEPExecutableLifecycleStage.Tick;
            try
            {
                if (entering)
                {
                    executable.DispatchEnter(context);
                }

                activeStage = KLEPExecutableLifecycleStage.Tick;
                tickStatus = executable.DispatchTick(context);
                if (!Enum.IsDefined(typeof(KLEPExecutableTickStatus), tickStatus))
                {
                    throw new InvalidOperationException(
                        $"Executable '{executable.StableId}' returned invalid Tick status " +
                        $"'{tickStatus}'.");
                }
            }
            catch (Exception fault)
            {
                context.Discard();
                ResolveFault(
                    fault,
                    executable.StableId,
                    activeStage,
                    out Exception actualFault,
                    out string actualExecutableId,
                    out KLEPExecutableLifecycleStage actualStage);
                return FaultRun(
                    keys,
                    waveIndex,
                    actualFault,
                    actualStage,
                    actualExecutableId);
            }

            IReadOnlyList<KLEPExecutableOutput> outputs = context.Complete();
            if (tickStatus == KLEPExecutableTickStatus.Running)
            {
                LastResult = CreateResult(
                    keys.Tick,
                    waveIndex,
                    KLEPExecutableState.Running,
                    null,
                    outputs);
                return LastResult;
            }

            KLEPExecutableState terminalState =
                tickStatus == KLEPExecutableTickStatus.Succeeded
                    ? KLEPExecutableState.Succeeded
                    : KLEPExecutableState.Failed;
            KLEPExecutableExitReason reason =
                tickStatus == KLEPExecutableTickStatus.Succeeded
                    ? KLEPExecutableExitReason.Succeeded
                    : KLEPExecutableExitReason.Failed;

            // A failed run cannot publish completion output. This keeps a
            // failure observable without letting it change the next decision.
            if (tickStatus == KLEPExecutableTickStatus.Failed)
            {
                outputs = Array.Empty<KLEPExecutableOutput>();
            }

            return FinishRun(keys, waveIndex, terminalState, reason, outputs);
        }

        internal bool TryCancel(
            KLEPKeySnapshot keys,
            int waveIndex,
            KLEPExecutableExitReason reason,
            out KLEPExecutionResult result)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex));
            }

            if (!IsCancellationReason(reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            if (State != KLEPExecutableState.Running)
            {
                result = null;
                return false;
            }

            result = FinishRun(
                keys,
                waveIndex,
                KLEPExecutableState.Cancelled,
                reason,
                Array.Empty<KLEPExecutableOutput>());
            return true;
        }

        private KLEPExecutionResult FinishRun(
            KLEPKeySnapshot keys,
            int waveIndex,
            KLEPExecutableState terminalState,
            KLEPExecutableExitReason reason,
            IReadOnlyList<KLEPExecutableOutput> outputs)
        {
            State = terminalState;
            terminalCycleIndex = keys.Tick;
            var exitContext = new KLEPExecutableExitContext(
                executable.StableId,
                keys,
                waveIndex,
                RunIndex,
                terminalState,
                reason,
                null);

            KLEPExecutableLifecycleStage? teardownStage;
            List<Exception> teardownFaults = AttemptTeardown(
                exitContext, out teardownStage);
            if (teardownFaults.Count > 0)
            {
                State = KLEPExecutableState.Faulted;
                Exception effective = CombineFaults(teardownFaults);
                string effectiveExecutableId = executable.StableId;
                KLEPExecutableLifecycleStage effectiveStage =
                    teardownStage ?? KLEPExecutableLifecycleStage.Exit;
                if (teardownFaults.Count == 1)
                {
                    ResolveFault(
                        teardownFaults[0],
                        executable.StableId,
                        effectiveStage,
                        out effective,
                        out effectiveExecutableId,
                        out effectiveStage);
                }

                LastFault = effective;
                LastFaultStage = effectiveStage;
                LastFaultExecutableId = effectiveExecutableId;
                LastResult = CreateResult(
                    keys.Tick,
                    waveIndex,
                    KLEPExecutableState.Faulted,
                    KLEPExecutableExitReason.Faulted,
                    Array.Empty<KLEPExecutableOutput>());
                return Rethrow<KLEPExecutionResult>(effective);
            }

            LastResult = CreateResult(
                keys.Tick, waveIndex, terminalState, reason, outputs);
            return LastResult;
        }

        private KLEPExecutionResult FaultRun(
            KLEPKeySnapshot keys,
            int waveIndex,
            Exception primaryFault,
            KLEPExecutableLifecycleStage primaryStage,
            string faultExecutableId)
        {
            State = KLEPExecutableState.Faulted;
            terminalCycleIndex = keys.Tick;
            var exitContext = new KLEPExecutableExitContext(
                executable.StableId,
                keys,
                waveIndex,
                RunIndex,
                KLEPExecutableState.Faulted,
                KLEPExecutableExitReason.Faulted,
                primaryFault);

            var faults = new List<Exception> { primaryFault };
            KLEPExecutableLifecycleStage? ignoredStage;
            faults.AddRange(AttemptTeardown(exitContext, out ignoredStage));
            Exception effective = CombineFaults(faults);
            LastFault = effective;
            LastFaultStage = primaryStage;
            LastFaultExecutableId = faultExecutableId;
            LastResult = CreateResult(
                keys.Tick,
                waveIndex,
                KLEPExecutableState.Faulted,
                KLEPExecutableExitReason.Faulted,
                Array.Empty<KLEPExecutableOutput>());
            return Rethrow<KLEPExecutionResult>(effective);
        }

        internal KLEPExecutionResult FaultAfterOutputApplication(
            KLEPKeySnapshot keys,
            int waveIndex,
            Exception fault,
            string faultExecutableId)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (fault == null)
            {
                throw new ArgumentNullException(nameof(fault));
            }

            string resolvedId = string.IsNullOrWhiteSpace(faultExecutableId)
                ? executable.StableId
                : faultExecutableId;
            if (State == KLEPExecutableState.Running)
            {
                return FaultRun(
                    keys,
                    waveIndex,
                    fault,
                    KLEPExecutableLifecycleStage.OutputApplication,
                    resolvedId);
            }

            // A terminal run has already exited and cleaned up. Mark its final
            // observable state Faulted without invoking teardown a second time.
            State = KLEPExecutableState.Faulted;
            terminalCycleIndex = keys.Tick;
            LastFault = fault;
            LastFaultStage = KLEPExecutableLifecycleStage.OutputApplication;
            LastFaultExecutableId = resolvedId;
            LastResult = CreateResult(
                keys.Tick,
                waveIndex,
                KLEPExecutableState.Faulted,
                KLEPExecutableExitReason.Faulted,
                Array.Empty<KLEPExecutableOutput>());
            return Rethrow<KLEPExecutionResult>(fault);
        }

        private List<Exception> AttemptTeardown(
            KLEPExecutableExitContext context,
            out KLEPExecutableLifecycleStage? firstFaultStage)
        {
            var faults = new List<Exception>(2);
            firstFaultStage = null;

            if (!exitAttempted)
            {
                exitAttempted = true;
                try
                {
                    executable.DispatchExit(context);
                }
                catch (Exception fault)
                {
                    firstFaultStage = KLEPExecutableLifecycleStage.Exit;
                    faults.Add(fault);
                }
            }

            if (!cleanupAttempted)
            {
                cleanupAttempted = true;
                try
                {
                    executable.DispatchCleanup(context);
                }
                catch (Exception fault)
                {
                    if (!firstFaultStage.HasValue)
                    {
                        firstFaultStage = KLEPExecutableLifecycleStage.Cleanup;
                    }

                    faults.Add(fault);
                }
            }

            return faults;
        }

        private KLEPExecutionResult CreateResult(
            long cycleIndex,
            int waveIndex,
            KLEPExecutableState state,
            KLEPExecutableExitReason? reason,
            IEnumerable<KLEPExecutableOutput> outputs)
        {
            return new KLEPExecutionResult(
                executable.StableId,
                cycleIndex,
                waveIndex,
                RunIndex,
                state,
                reason,
                outputs);
        }

        private static bool IsTerminal(KLEPExecutableState state)
        {
            return state == KLEPExecutableState.Succeeded ||
                state == KLEPExecutableState.Failed ||
                state == KLEPExecutableState.Cancelled ||
                state == KLEPExecutableState.Faulted;
        }

        private static bool IsCancellationReason(KLEPExecutableExitReason reason)
        {
            return reason == KLEPExecutableExitReason.LocksClosed ||
                reason == KLEPExecutableExitReason.BelowThreshold ||
                reason == KLEPExecutableExitReason.Interrupted ||
                reason == KLEPExecutableExitReason.WaveAborted ||
                reason == KLEPExecutableExitReason.Removed;
        }

        private static Exception CombineFaults(IReadOnlyList<Exception> faults)
        {
            return faults.Count == 1
                ? faults[0]
                : new AggregateException("Executable lifecycle callbacks faulted.", faults);
        }

        private static void ResolveFault(
            Exception fault,
            string defaultExecutableId,
            KLEPExecutableLifecycleStage defaultStage,
            out Exception actualFault,
            out string actualExecutableId,
            out KLEPExecutableLifecycleStage actualStage)
        {
            if (fault is KLEPNestedExecutableFaultException nested)
            {
                actualFault = nested.OriginalException;
                actualExecutableId = nested.ExecutableStableId;
                actualStage = nested.Stage;
                return;
            }

            actualFault = fault;
            actualExecutableId = defaultExecutableId;
            actualStage = defaultStage;
        }

        private static void Rethrow(Exception fault)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
        }

        private static T Rethrow<T>(Exception fault)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
            throw new InvalidOperationException("Unreachable lifecycle fault path.");
        }
    }
}
