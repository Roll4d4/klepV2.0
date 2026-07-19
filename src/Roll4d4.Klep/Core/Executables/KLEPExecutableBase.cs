using System;
using System.Collections.Generic;

namespace Roll4d4.Klep.Core
{
    public readonly struct KLEPEligibility
    {
        internal KLEPEligibility(KLEPExecutableEvaluation executableEvaluation)
        {
            ExecutableEvaluation = executableEvaluation ??
                throw new ArgumentNullException(nameof(executableEvaluation));
        }

        public bool IsEligible => ExecutableEvaluation.IsEligible;
        public string Reason => ExecutableEvaluation.Explanation;
        public KLEPExecutableEvaluation ExecutableEvaluation { get; }
    }

    // Authored evaluation stays sealed and pure. Runtime behavior is exposed
    // only through protected callbacks; the internal lifecycle controller is
    // the sole caller and owns all state transitions.
    public abstract class KLEPExecutableBase
    {
        private string goalOwnerId;
        private string neuronOwnerId;

        protected KLEPExecutableBase(KLEPExecutableDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public KLEPExecutableDefinition Definition { get; }
        public string StableId => Definition.StableId;
        public string DisplayName => Definition.DisplayName;
        public KLEPExecutableKind Kind => Definition.Kind;
        public KLEPExecutionMode ExecutionMode => Definition.ExecutionMode;
        public IReadOnlyList<KLEPLock> ValidationLocks => Definition.ValidationLocks;
        public IReadOnlyList<KLEPLock> ExecutionLocks => Definition.ExecutionLocks;
        public IReadOnlyList<KLEPKeyDefinition> DeclaredOutputs =>
            Definition.DeclaredOutputs;
        internal bool IsGoalOwned => goalOwnerId != null;
        internal string GoalOwnerId => goalOwnerId;
        internal bool IsNeuronOwned => neuronOwnerId != null;
        internal string NeuronOwnerId => neuronOwnerId;

        public KLEPExecutableEvaluation EvaluateLocks(KLEPKeySnapshot snapshot)
        {
            return Definition.Evaluate(snapshot);
        }

        // Both named Lock groups are enforced here so a subclass cannot bypass
        // one group while customizing later lifecycle behavior.
        public KLEPEligibility EvaluateEligibility(KLEPKeySnapshot snapshot)
        {
            KLEPExecutableEvaluation evaluation = EvaluateLocks(snapshot);
            return new KLEPEligibility(evaluation);
        }

        // Scoring is sealed definition work, just like Lock evaluation. A
        // behavior subclass cannot mutate runtime state while being ranked.
        internal KLEPExecutableScoreEvaluation EvaluateScore(KLEPKeySnapshot snapshot)
        {
            return Definition.EvaluateScore(snapshot);
        }

        protected virtual void OnInitialize(
            KLEPExecutableInitializationContext context)
        {
        }

        protected virtual void OnEnter(KLEPExecutionContext context)
        {
        }

        // An empty placeholder behavior completes in one advancement. Concrete
        // behaviors override this single timing hook; there is no Unity Update
        // or FixedUpdate lifecycle in Core.
        protected virtual KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            return KLEPExecutableTickStatus.Succeeded;
        }

        protected virtual void OnExit(KLEPExecutableExitContext context)
        {
        }

        protected virtual void OnCleanup(KLEPExecutableExitContext context)
        {
        }

        internal void DispatchInitialize(
            KLEPExecutableInitializationContext context)
        {
            OnInitialize(context);
        }

        internal void DispatchEnter(KLEPExecutionContext context)
        {
            OnEnter(context);
        }

        internal KLEPExecutableTickStatus DispatchTick(
            KLEPExecutionContext context)
        {
            return OnTick(context);
        }

        internal void DispatchExit(KLEPExecutableExitContext context)
        {
            OnExit(context);
        }

        internal void DispatchCleanup(KLEPExecutableExitContext context)
        {
            OnCleanup(context);
        }

        internal void ClaimGoalOwnership(string ownerStableId)
        {
            if (string.IsNullOrWhiteSpace(ownerStableId))
            {
                throw new ArgumentException(
                    "A non-empty Goal owner ID is required.",
                    nameof(ownerStableId));
            }

            if (goalOwnerId != null)
            {
                throw new InvalidOperationException(
                    $"Executable '{StableId}' is already owned by Goal " +
                    $"'{goalOwnerId}'.");
            }

            if (neuronOwnerId != null)
            {
                throw new InvalidOperationException(
                    $"Executable '{StableId}' is already registered by Neuron " +
                    $"'{neuronOwnerId}'.");
            }

            goalOwnerId = ownerStableId;
        }

        internal void ClaimNeuronOwnership(string ownerStableId)
        {
            if (string.IsNullOrWhiteSpace(ownerStableId))
            {
                throw new ArgumentException(
                    "A non-empty Neuron owner ID is required.",
                    nameof(ownerStableId));
            }

            if (goalOwnerId != null)
            {
                throw new InvalidOperationException(
                    $"Executable '{StableId}' is owned by Goal '{goalOwnerId}'.");
            }

            if (neuronOwnerId != null)
            {
                throw new InvalidOperationException(
                    $"Executable '{StableId}' is already registered by Neuron " +
                    $"'{neuronOwnerId}'.");
            }

            neuronOwnerId = ownerStableId;
        }

        internal void ReleaseNeuronOwnership(string ownerStableId)
        {
            if (StringComparer.Ordinal.Equals(neuronOwnerId, ownerStableId))
            {
                neuronOwnerId = null;
            }
        }
    }
}
