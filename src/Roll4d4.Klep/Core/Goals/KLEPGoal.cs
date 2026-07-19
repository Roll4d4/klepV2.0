using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// The complete read-only input available while one eligible Goal evaluates
    /// its intrinsic attraction for the current arbitration snapshot.
    /// </summary>
    public sealed class KLEPGoalAttractionContext
    {
        internal KLEPGoalAttractionContext(
            KLEPExecutableDefinition goalDefinition,
            KLEPKeySnapshot keySnapshot)
        {
            GoalDefinition = goalDefinition ??
                throw new ArgumentNullException(nameof(goalDefinition));
            KeySnapshot = keySnapshot ??
                throw new ArgumentNullException(nameof(keySnapshot));
        }

        public KLEPExecutableDefinition GoalDefinition { get; }
        public KLEPKeySnapshot KeySnapshot { get; }
    }

    /// <summary>
    /// One finite signed contribution produced by a Goal's project-authored,
    /// read-only attraction evaluator.
    /// </summary>
    public sealed class KLEPGoalAttractionEvaluation
    {
        public KLEPGoalAttractionEvaluation(
            float contribution,
            string explanation = "")
        {
            if (float.IsNaN(contribution) || float.IsInfinity(contribution))
            {
                throw new ArgumentOutOfRangeException(nameof(contribution));
            }

            Contribution = contribution;
            Explanation = explanation ?? string.Empty;
        }

        public float Contribution { get; }
        public string Explanation { get; }
    }

    /// <summary>
    /// Project policy for a Goal's intrinsic attraction. Implementations must be
    /// pure: evaluation may inspect only the supplied immutable context.
    /// </summary>
    public interface IKLEPGoalAttractionEvaluator
    {
        string StableId { get; }
        string Version { get; }
        KLEPGoalAttractionEvaluation Evaluate(
            KLEPGoalAttractionContext context);
    }

    public enum KLEPGoalLayerRequirement
    {
        AllMustFire,
        AnyCanFire,
        NoneNeedToFire
    }

    public sealed class KLEPGoalLayer
    {
        private readonly ReadOnlyCollection<KLEPExecutableBase> children;

        public KLEPGoalLayer(
            KLEPGoalLayerRequirement requirement,
            IEnumerable<KLEPExecutableBase> children = null)
        {
            if (!Enum.IsDefined(typeof(KLEPGoalLayerRequirement), requirement))
            {
                throw new ArgumentOutOfRangeException(nameof(requirement));
            }

            Requirement = requirement;
            var copy = new List<KLEPExecutableBase>();
            if (children != null)
            {
                foreach (KLEPExecutableBase child in children)
                {
                    copy.Add(child ?? throw new ArgumentException(
                        "A Goal layer cannot contain a null child.",
                        nameof(children)));
                }
            }

            this.children = new ReadOnlyCollection<KLEPExecutableBase>(copy);
        }

        public KLEPGoalLayerRequirement Requirement { get; }
        public IReadOnlyList<KLEPExecutableBase> Children => children;
    }

    /// <summary>
    /// An Executable that owns an ordered set of child layers. The Agent runs a
    /// root Goal in its authored Solo or Tandem lane and advances only its
    /// current layer during one top-level Tick.
    /// </summary>
    public class KLEPGoal : KLEPExecutableBase
    {
        private readonly ReadOnlyCollection<KLEPGoalLayer> layers;
        private readonly ReadOnlyCollection<KLEPExecutableBase> ownedChildren;
        private readonly IKLEPGoalAttractionEvaluator attractionEvaluator;
        private readonly string attractionEvaluatorStableId;
        private readonly string attractionEvaluatorVersion;
        private readonly KLEPKeyDefinition activationKey;

        public KLEPGoal(KLEPExecutableDefinition definition)
            : this(definition, null, null, null)
        {
        }

        public KLEPGoal(
            KLEPExecutableDefinition definition,
            IEnumerable<KLEPGoalLayer> layers,
            KLEPKeyDefinition activationKey = null)
            : this(definition, layers, activationKey, null)
        {
        }

        public KLEPGoal(
            KLEPExecutableDefinition definition,
            IEnumerable<KLEPGoalLayer> layers,
            KLEPKeyDefinition activationKey,
            IKLEPGoalAttractionEvaluator attractionEvaluator)
            : base(RequireGoalDefinition(definition))
        {
            this.layers = CopyLayers(layers);
            this.activationKey = ValidateActivationKey(definition, activationKey);
            this.attractionEvaluator = attractionEvaluator;
            if (attractionEvaluator != null)
            {
                attractionEvaluatorStableId = RequireEvaluatorIdentity(
                    attractionEvaluator.StableId,
                    nameof(attractionEvaluator));
                attractionEvaluatorVersion = RequireEvaluatorIdentity(
                    attractionEvaluator.Version,
                    nameof(attractionEvaluator));
            }

            var seenChildren = new HashSet<KLEPExecutableBase>();
            var seenChildIds = new HashSet<string>(StringComparer.Ordinal);
            var orderedChildren = new List<KLEPExecutableBase>();
            foreach (KLEPGoalLayer layer in this.layers)
            {
                foreach (KLEPExecutableBase child in layer.Children)
                {
                    if (!seenChildren.Add(child) || !seenChildIds.Add(child.StableId))
                    {
                        throw new ArgumentException(
                            $"Executable ID '{child.StableId}' appears more than once " +
                            $"in Goal '{StableId}'.",
                            nameof(layers));
                    }

                    if (child.IsGoalOwned)
                    {
                        throw new InvalidOperationException(
                            $"Executable '{child.StableId}' is already owned by Goal " +
                            $"'{child.GoalOwnerId}'.");
                    }

                    if (child.IsNeuronOwned)
                    {
                        throw new InvalidOperationException(
                            $"Executable '{child.StableId}' is already registered " +
                            $"by Neuron '{child.NeuronOwnerId}'.");
                    }

                    orderedChildren.Add(child);
                }
            }

            ownedChildren = new ReadOnlyCollection<KLEPExecutableBase>(orderedChildren);
            foreach (KLEPExecutableBase child in orderedChildren)
            {
                child.ClaimGoalOwnership(StableId);
            }
        }

        public IReadOnlyList<KLEPGoalLayer> Layers => layers;
        public KLEPKeyDefinition ActivationKey => activationKey;
        public string AttractionEvaluatorStableId => attractionEvaluatorStableId;
        public string AttractionEvaluatorVersion => attractionEvaluatorVersion;
        internal IReadOnlyList<KLEPExecutableBase> OwnedChildren => ownedChildren;

        // Goal lifecycle is deliberately sealed away from recipe subclasses.
        // KLEPExecutableRuntime routes these stages through its owned
        // KLEPGoalRuntime instead of dispatching callbacks on the recipe.
        protected sealed override void OnInitialize(
            KLEPExecutableInitializationContext context)
        {
            throw RuntimeOwnedLifecycle();
        }

        protected sealed override void OnEnter(KLEPExecutionContext context)
        {
            throw RuntimeOwnedLifecycle();
        }

        protected sealed override KLEPExecutableTickStatus OnTick(
            KLEPExecutionContext context)
        {
            throw RuntimeOwnedLifecycle();
        }

        protected sealed override void OnExit(KLEPExecutableExitContext context)
        {
            throw RuntimeOwnedLifecycle();
        }

        private protected sealed override KLEPExecutableScoreEvaluation ComposeScore(
            KLEPKeySnapshot snapshot,
            KLEPExecutableScoreEvaluation authoredScore)
        {
            if (attractionEvaluator == null)
            {
                return authoredScore;
            }

            ValidateEvaluatorIdentity();
            KLEPGoalAttractionEvaluation evaluation = attractionEvaluator.Evaluate(
                new KLEPGoalAttractionContext(Definition, snapshot));
            ValidateEvaluatorIdentity();
            if (evaluation == null)
            {
                throw new InvalidOperationException(
                    $"Goal attraction evaluator '{attractionEvaluatorStableId}' " +
                    "returned no evaluation.");
            }

            return authoredScore.WithGoalIntrinsicAttraction(
                attractionEvaluatorStableId,
                attractionEvaluatorVersion,
                evaluation.Contribution,
                evaluation.Explanation);
        }

        private static ReadOnlyCollection<KLEPGoalLayer> CopyLayers(
            IEnumerable<KLEPGoalLayer> source)
        {
            var copy = new List<KLEPGoalLayer>();
            if (source != null)
            {
                foreach (KLEPGoalLayer layer in source)
                {
                    copy.Add(layer ?? throw new ArgumentException(
                        "A Goal cannot contain a null layer.",
                        nameof(source)));
                }
            }

            return new ReadOnlyCollection<KLEPGoalLayer>(copy);
        }

        private static InvalidOperationException RuntimeOwnedLifecycle()
        {
            return new InvalidOperationException(
                "A Goal recipe cannot drive lifecycle callbacks directly. " +
                "Its owning Executable runtime must use KLEPGoalRuntime.");
        }

        private static KLEPExecutableDefinition RequireGoalDefinition(
            KLEPExecutableDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (definition.Kind != KLEPExecutableKind.Goal)
            {
                throw new ArgumentException(
                    "KLEPGoal requires an Executable definition whose Kind is Goal.",
                    nameof(definition));
            }

            return definition;
        }

        private static string RequireEvaluatorIdentity(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A Goal attraction evaluator requires a non-empty stable ID " +
                    "and version.",
                    parameterName);
            }

            return value;
        }

        private void ValidateEvaluatorIdentity()
        {
            if (!StringComparer.Ordinal.Equals(
                    attractionEvaluatorStableId,
                    attractionEvaluator.StableId) ||
                !StringComparer.Ordinal.Equals(
                    attractionEvaluatorVersion,
                    attractionEvaluator.Version))
            {
                throw new InvalidOperationException(
                    $"Goal attraction evaluator '{attractionEvaluatorStableId}' " +
                    "changed its stable ID or version after Goal construction.");
            }
        }

        private static KLEPKeyDefinition ValidateActivationKey(
            KLEPExecutableDefinition definition,
            KLEPKeyDefinition activationKey)
        {
            if (activationKey == null)
            {
                return null;
            }

            if (!definition.TryGetDeclaredOutput(
                    activationKey.Id, out KLEPKeyDefinition declared) ||
                declared.Scope != activationKey.Scope)
            {
                throw new ArgumentException(
                    $"Goal '{definition.StableId}' must declare activation Key " +
                    $"'{activationKey.Id}' as an output.",
                    nameof(activationKey));
            }

            return activationKey;
        }

    }

    /// <summary>
    /// Mutable progress for one registered Goal tenure. The authored Goal remains
    /// immutable while its owning Executable runtime advances this record.
    /// </summary>
    internal sealed class KLEPGoalRuntime
    {
        private readonly KLEPGoal recipe;
        private ReadOnlyCollection<KLEPExecutableRuntime> childRuntimes;
        private readonly HashSet<string> completedChildIds =
            new HashSet<string>(StringComparer.Ordinal);
        private ReadOnlyCollection<KLEPExecutionResult> lastChildResults =
            new ReadOnlyCollection<KLEPExecutionResult>(
                new List<KLEPExecutionResult>());

        internal KLEPGoalRuntime(KLEPGoal recipe)
        {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            childRuntimes = new ReadOnlyCollection<KLEPExecutableRuntime>(
                new List<KLEPExecutableRuntime>());
        }

        internal int CurrentLayerIndex { get; private set; }
        internal bool IsComplete => CurrentLayerIndex >= recipe.Layers.Count;

        internal KLEPGoalRuntimeSnapshot CaptureSnapshot()
        {
            var layerSnapshots = new List<KLEPGoalLayerRuntimeSnapshot>(
                recipe.Layers.Count);
            for (int layerIndex = 0;
                 layerIndex < recipe.Layers.Count;
                 layerIndex++)
            {
                KLEPGoalLayer layer = recipe.Layers[layerIndex];
                var children = new List<KLEPGoalChildRuntimeSnapshot>(
                    layer.Children.Count);
                foreach (KLEPExecutableBase child in layer.Children)
                {
                    KLEPExecutableRuntime runtime = FindRuntime(child);
                    children.Add(new KLEPGoalChildRuntimeSnapshot(
                        runtime.CaptureSnapshot(),
                        layerIndex == CurrentLayerIndex &&
                            completedChildIds.Contains(child.StableId)));
                }

                layerSnapshots.Add(new KLEPGoalLayerRuntimeSnapshot(
                    layerIndex,
                    layer.Requirement,
                    children));
            }

            return new KLEPGoalRuntimeSnapshot(
                CurrentLayerIndex,
                IsComplete,
                recipe.ActivationKey == null
                    ? (KLEPKeyId?)null
                    : recipe.ActivationKey.Id,
                layerSnapshots,
                lastChildResults);
        }

        internal void Initialize(KLEPExecutableInitializationContext context)
        {
            // A new root registration tenure gets fresh child runtime records;
            // authored ownership stays with the immutable Goal recipe.
            var freshRuntimes = new List<KLEPExecutableRuntime>(
                recipe.OwnedChildren.Count);
            foreach (KLEPExecutableBase child in recipe.OwnedChildren)
            {
                freshRuntimes.Add(new KLEPExecutableRuntime(child));
            }

            childRuntimes = new ReadOnlyCollection<KLEPExecutableRuntime>(
                freshRuntimes);
            var results = new List<KLEPExecutionResult>(childRuntimes.Count);
            foreach (KLEPExecutableRuntime child in childRuntimes)
            {
                KLEPExecutionResult result;
                try
                {
                    result = child.Initialize(
                        recipe.StableId, context.Keys, context.WaveIndex);
                }
                catch (Exception fault)
                {
                    throw WrapChildFault(child, fault);
                }

                results.Add(result);
                ForwardOutputs(context, result);
            }

            lastChildResults = new ReadOnlyCollection<KLEPExecutionResult>(results);
        }

        internal void Enter(KLEPExecutionContext context)
        {
            CurrentLayerIndex = 0;
            completedChildIds.Clear();
            lastChildResults = new ReadOnlyCollection<KLEPExecutionResult>(
                new List<KLEPExecutionResult>());

            if (recipe.ActivationKey != null)
            {
                context.Add(recipe.ActivationKey);
            }
        }

        internal KLEPExecutableTickStatus Tick(KLEPExecutionContext context)
        {
            foreach (KLEPExecutableRuntime child in childRuntimes)
            {
                child.Rearm(context.CycleIndex);
            }

            if (CurrentLayerIndex >= recipe.Layers.Count)
            {
                return KLEPExecutableTickStatus.Succeeded;
            }

            KLEPGoalLayer layer = recipe.Layers[CurrentLayerIndex];
            var results = new List<KLEPExecutionResult>();
            bool layerComplete;
            switch (layer.Requirement)
            {
                case KLEPGoalLayerRequirement.NoneNeedToFire:
                    layerComplete = true;
                    break;

                case KLEPGoalLayerRequirement.AllMustFire:
                    layerComplete = AdvanceAll(layer, context, results);
                    break;

                case KLEPGoalLayerRequirement.AnyCanFire:
                    layerComplete = AdvanceAny(layer, context, results);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Goal '{recipe.StableId}' has an invalid layer requirement.");
            }

            lastChildResults = new ReadOnlyCollection<KLEPExecutionResult>(results);
            if (layerComplete)
            {
                CurrentLayerIndex++;
                completedChildIds.Clear();
            }

            return CurrentLayerIndex >= recipe.Layers.Count
                ? KLEPExecutableTickStatus.Succeeded
                : KLEPExecutableTickStatus.Running;
        }

        internal void Exit(KLEPExecutableExitContext context)
        {
            // Successful All/Any completion leaves no running child. If the
            // Goal is interrupted, every active child is unwound exactly once.
            KLEPExecutableExitReason childReason = IsCancellationReason(context.Reason)
                ? context.Reason
                : KLEPExecutableExitReason.Interrupted;
            var results = new List<KLEPExecutionResult>();
            var faults = new List<KLEPNestedExecutableFaultException>();
            foreach (KLEPExecutableRuntime child in childRuntimes)
            {
                try
                {
                    if (child.TryCancel(
                            context.Keys,
                            context.WaveIndex,
                            childReason,
                            out KLEPExecutionResult result))
                    {
                        results.Add(result);
                    }
                }
                catch (Exception fault)
                {
                    if (child.LastResult != null)
                    {
                        results.Add(child.LastResult);
                    }

                    // One child's faulty Exit or Cleanup must not strand a
                    // later sibling in Running. Remember the fault, then keep
                    // unwinding every child before it escapes the Goal.
                    faults.Add(WrapChildFault(child, fault));
                }
            }

            if (results.Count > 0)
            {
                lastChildResults = new ReadOnlyCollection<KLEPExecutionResult>(
                    results);
            }

            RethrowChildCancellationFaults(faults);
        }

        private bool AdvanceAll(
            KLEPGoalLayer layer,
            KLEPExecutionContext context,
            List<KLEPExecutionResult> results)
        {
            foreach (KLEPExecutableBase child in layer.Children)
            {
                if (completedChildIds.Contains(child.StableId))
                {
                    continue;
                }

                KLEPExecutableRuntime runtime = FindRuntime(child);
                KLEPEligibility eligibility = child.EvaluateEligibility(context.Keys);
                if (!eligibility.IsEligible)
                {
                    CancelBlockedChild(runtime, context, results);
                    continue;
                }

                KLEPExecutionResult result;
                try
                {
                    result = runtime.Advance(
                        context.Keys, context.WaveIndex);
                }
                catch (Exception fault)
                {
                    throw WrapChildFault(runtime, fault);
                }

                results.Add(result);
                ForwardOutputs(context, result);
                if (result.State == KLEPExecutableState.Succeeded)
                {
                    completedChildIds.Add(child.StableId);
                }
            }

            return completedChildIds.Count == layer.Children.Count;
        }

        private bool AdvanceAny(
            KLEPGoalLayer layer,
            KLEPExecutionContext context,
            List<KLEPExecutionResult> results)
        {
            // Any is intentionally serial until the still-open sibling policy
            // is decided. A Running child retains the layer; a failed or
            // blocked child allows the next authored child to be considered.
            foreach (KLEPExecutableBase child in layer.Children)
            {
                KLEPExecutableRuntime runtime = FindRuntime(child);
                KLEPEligibility eligibility = child.EvaluateEligibility(context.Keys);
                if (!eligibility.IsEligible)
                {
                    CancelBlockedChild(runtime, context, results);
                    continue;
                }

                KLEPExecutionResult result;
                try
                {
                    result = runtime.Advance(
                        context.Keys, context.WaveIndex);
                }
                catch (Exception fault)
                {
                    throw WrapChildFault(runtime, fault);
                }

                results.Add(result);
                ForwardOutputs(context, result);
                if (result.State == KLEPExecutableState.Succeeded)
                {
                    return true;
                }

                if (result.State == KLEPExecutableState.Running)
                {
                    return false;
                }
            }

            return false;
        }

        private static void CancelBlockedChild(
            KLEPExecutableRuntime runtime,
            KLEPExecutionContext context,
            List<KLEPExecutionResult> results)
        {
            try
            {
                if (runtime.TryCancel(
                        context.Keys,
                        context.WaveIndex,
                        KLEPExecutableExitReason.LocksClosed,
                        out KLEPExecutionResult cancelled))
                {
                    results.Add(cancelled);
                }
            }
            catch (Exception fault)
            {
                throw WrapChildFault(runtime, fault);
            }
        }

        private KLEPExecutableRuntime FindRuntime(KLEPExecutableBase executable)
        {
            foreach (KLEPExecutableRuntime runtime in childRuntimes)
            {
                if (ReferenceEquals(runtime.Executable, executable))
                {
                    return runtime;
                }
            }

            throw new InvalidOperationException(
                $"Goal '{recipe.StableId}' does not own Executable " +
                $"'{executable.StableId}'.");
        }

        private static void ForwardOutputs(
            KLEPExecutionContext context,
            KLEPExecutionResult result)
        {
            foreach (KLEPExecutableOutput output in result.Outputs)
            {
                context.ForwardValidated(output);
            }
        }

        private static void ForwardOutputs(
            KLEPExecutableInitializationContext context,
            KLEPExecutionResult result)
        {
            foreach (KLEPExecutableOutput output in result.Outputs)
            {
                context.ForwardValidated(output);
            }
        }

        private static bool IsCancellationReason(KLEPExecutableExitReason reason)
        {
            return reason == KLEPExecutableExitReason.LocksClosed ||
                reason == KLEPExecutableExitReason.BelowThreshold ||
                reason == KLEPExecutableExitReason.Interrupted ||
                reason == KLEPExecutableExitReason.WaveAborted ||
                reason == KLEPExecutableExitReason.Removed;
        }

        private static KLEPNestedExecutableFaultException WrapChildFault(
            KLEPExecutableRuntime child,
            Exception fault)
        {
            return new KLEPNestedExecutableFaultException(
                child.LastFaultExecutableId ?? child.Executable.StableId,
                child.LastFaultStage ?? KLEPExecutableLifecycleStage.Tick,
                fault);
        }

        private static void RethrowChildCancellationFaults(
            IReadOnlyList<KLEPNestedExecutableFaultException> faults)
        {
            if (faults.Count == 0)
            {
                return;
            }

            if (faults.Count == 1)
            {
                throw faults[0];
            }

            KLEPNestedExecutableFaultException first = faults[0];
            throw new KLEPNestedExecutableFaultException(
                first.ExecutableStableId,
                first.Stage,
                new AggregateException(
                    "Several Goal children faulted while being cancelled.",
                    faults));
        }
    }
}
