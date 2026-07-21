using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Observer
{
    /// <summary>
    /// The KLEP Observer is a non-authoritative introspective reasoning service.
    /// It constructs and maintains evidence-bound models of a Neuron, answers
    /// higher-level questions over those models, and may propose plans or
    /// influence eligible behavior. It never owns live execution or changes
    /// what reality permits.
    /// </summary>
    public sealed class KLEPObserver :
        IKLEPGuidanceObserver,
        IKLEPExecutableStructuralObserver
    {
        private static readonly StringComparer IdComparer = StringComparer.Ordinal;
        private readonly ReadOnlyCollection<IKLEPObserverEvidenceSource> sources;
        private readonly ReadOnlyCollection<EvidenceSourceRegistration>
            sourceRegistrations;
        private bool isObserving;
        private bool isReasoning;

        public KLEPObserver(
            string stableId,
            string version,
            IEnumerable<IKLEPObserverEvidenceSource> evidenceSources = null,
            KLEPObserverConfiguration configuration = null,
            IKLEPLearnedExpectationsView learnedExpectations = null)
        {
            StableId = KLEPObserverValidation.RequireId(stableId, nameof(stableId));
            Version = KLEPObserverValidation.RequireId(version, nameof(version));
            Configuration = configuration ?? KLEPObserverConfiguration.Default;
            ValidateLearnedExpectations(learnedExpectations);
            LearnedExpectations = learnedExpectations;
            sourceRegistrations = CopySources(evidenceSources);
            var visibleSources = new List<IKLEPObserverEvidenceSource>(
                sourceRegistrations.Count);
            for (int index = 0; index < sourceRegistrations.Count; index++)
            {
                visibleSources.Add(sourceRegistrations[index].Source);
            }

            sources = new ReadOnlyCollection<IKLEPObserverEvidenceSource>(
                visibleSources);
        }

        public string StableId { get; }
        public string Version { get; }
        public KLEPObserverConfiguration Configuration { get; }

        /// <summary>
        /// Optional read-only learned evidence. The independent
        /// KLEPLearnedExpectations subsystem owns all mutation; this Observer
        /// can only query the supplied view while reasoning.
        /// </summary>
        public IKLEPLearnedExpectationsView LearnedExpectations { get; }
        public IReadOnlyList<IKLEPObserverEvidenceSource> EvidenceSources => sources;
        public KLEPObserverTrace LastTrace { get; private set; }
        public KLEPObserverSelfModel LastSelfModel { get; private set; }
        public KLEPObserverReasoningTrace LastReasoningTrace { get; private set; }

        private void ValidateLearnedExpectations(
            IKLEPLearnedExpectationsView learnedExpectations)
        {
            if (learnedExpectations == null)
            {
                return;
            }

            if (!StringComparer.Ordinal.Equals(
                    StableId, learnedExpectations.OwnerStableId) ||
                !StringComparer.Ordinal.Equals(
                    Version, learnedExpectations.OwnerVersion))
            {
                throw new ArgumentException(
                    "A learned-expectation view must be bound to this " +
                    "Observer identity and version.",
                    nameof(learnedExpectations));
            }
        }

        /// <summary>
        /// Queries the injected learned authority without acquiring mutation
        /// rights. This is evidence for higher-level comparison only; it does
        /// not produce Observer polish, eligibility, or execution by itself.
        /// </summary>
        public KLEPObserverExpectationQueryResult QueryLearnedExpectation(
            KLEPObserverSelfModel acceptedSelfModel,
            string sourceExecutableId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon)
        {
            if (LearnedExpectations == null)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' has no learned-expectation view.");
            }

            return LearnedExpectations.Query(
                acceptedSelfModel,
                sourceExecutableId,
                outcomeKeyId,
                observationMeaning,
                context,
                horizon);
        }

        public KLEPGuidanceAdvice Observe(KLEPAgentGuidanceContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (isObserving)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' cannot Observe recursively.");
            }

            LastTrace = null;
            isObserving = true;
            try
            {
                List<KLEPExecutableDefinition> targets =
                    CollectEligibleTargets(context);
                if (targets.Count == 0)
                {
                    LastTrace = new KLEPObserverTrace(
                        StableId,
                        Version,
                        context.Request,
                        Array.Empty<KLEPObserverTargetTrace>(),
                        null,
                        KLEPObserverAbstentionReason.NoEligibleGoalOrAction,
                        null);
                    return null;
                }

                var evidenceContext = new KLEPObserverEvidenceContext(
                    context, targets);
                var evidenceByTarget = new Dictionary<
                    string, List<KLEPObserverEvidenceTrace>>(IdComparer);
                var totals = new Dictionary<string, double>(IdComparer);
                foreach (KLEPExecutableDefinition target in targets)
                {
                    evidenceByTarget.Add(
                        target.StableId,
                        new List<KLEPObserverEvidenceTrace>());
                    totals.Add(target.StableId, 0d);
                }

                int evidenceCount = 0;
                foreach (EvidenceSourceRegistration registration in
                         sourceRegistrations)
                {
                    IKLEPObserverEvidenceSource source = registration.Source;
                    ValidateSourceIdentity(registration);
                    var suppliedTargetIds = new HashSet<string>(IdComparer);
                    IReadOnlyList<KLEPObserverEvidence> supplied =
                        source.Evaluate(evidenceContext);
                    ValidateSourceIdentity(registration);
                    if (supplied == null)
                    {
                        throw new InvalidOperationException(
                            $"Evidence source '{source.StableId}' returned null.");
                    }

                    for (int index = 0; index < supplied.Count; index++)
                    {
                        KLEPObserverEvidence item = supplied[index] ??
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' returned a null item.");
                        if (!suppliedTargetIds.Add(item.TargetExecutableId))
                        {
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' returned more " +
                                $"than one contribution for '{item.TargetExecutableId}'. " +
                                "Each source must aggregate one net value per target.");
                        }

                        if (!evidenceByTarget.TryGetValue(
                                item.TargetExecutableId,
                                out List<KLEPObserverEvidenceTrace> targetEvidence))
                        {
                            throw new InvalidOperationException(
                                $"Evidence source '{source.StableId}' targeted unavailable " +
                                $"Executable '{item.TargetExecutableId}'.");
                        }

                        double total = totals[item.TargetExecutableId] + item.Value;
                        if (double.IsNaN(total) ||
                            double.IsInfinity(total) ||
                            total > float.MaxValue ||
                            total < -float.MaxValue)
                        {
                            throw new InvalidOperationException(
                                $"Holistic evidence for '{item.TargetExecutableId}' " +
                                "exceeded the finite score range.");
                        }

                        targetEvidence.Add(new KLEPObserverEvidenceTrace(
                            registration.StableId,
                            registration.Version,
                            item));
                        totals[item.TargetExecutableId] = total;
                        evidenceCount++;
                    }
                }

                var traces = new List<KLEPObserverTargetTrace>(targets.Count);
                KLEPExecutableDefinition selected = null;
                double selectedValue = double.NegativeInfinity;
                foreach (KLEPExecutableDefinition target in targets)
                {
                    double holisticValue = totals[target.StableId];
                    traces.Add(new KLEPObserverTargetTrace(
                        target,
                        evidenceByTarget[target.StableId],
                        holisticValue));

                    if (holisticValue > Configuration.MinimumDirectionValue &&
                        (selected == null || holisticValue > selectedValue))
                    {
                        selected = target;
                        selectedValue = holisticValue;
                    }
                }

                if (selected == null)
                {
                    KLEPObserverAbstentionReason reason = evidenceCount == 0
                        ? KLEPObserverAbstentionReason.NoEvidence
                        : KLEPObserverAbstentionReason.NoBeneficialDirection;
                    LastTrace = new KLEPObserverTrace(
                        StableId,
                        Version,
                        context.Request,
                        traces,
                        null,
                        reason,
                        null);
                    return null;
                }

                var advice = new KLEPGuidanceAdvice(
                    StableId,
                    Version,
                    context.Request.CycleIndex,
                    context.Request.Environment,
                    selected.StableId,
                    CollectLockIds(selected),
                    Configuration.PolishAmount,
                    context.Request.EvidenceFingerprint);
                LastTrace = new KLEPObserverTrace(
                    StableId,
                    Version,
                    context.Request,
                    traces,
                    selected.StableId,
                    KLEPObserverAbstentionReason.None,
                    advice);
                return advice;
            }
            finally
            {
                isObserving = false;
            }
        }

        public KLEPExecutableStructuralMap ObserveStructure(
            KLEPExecutableCatalogSnapshot snapshot)
        {
            return KLEPExecutableStructuralMapper.Build(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
        }

        /// <summary>
        /// Retains one immutable model of the exact structural assessment and
        /// Key evidence accepted by a completed Agent trace. This is an explicit
        /// observation call; it does not advance or otherwise touch the Agent.
        /// </summary>
        public KLEPObserverSelfModel ObserveSelf(KLEPAgentTickTrace trace)
        {
            if (isObserving || isReasoning)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' cannot capture a self-model while " +
                    "another Observer operation is active.");
            }

            var model = new KLEPObserverSelfModel(StableId, Version, trace);
            LastSelfModel = model;
            LastReasoningTrace = null;
            return model;
        }

        /// <summary>
        /// Builds a deterministic structural dependency proposal from the most
        /// recently captured self-model. It preserves every mapped producer and
        /// Lock-expression branch; it does not select or execute a route.
        /// </summary>
        public KLEPObserverStructuralDependencyProposal
            ProposeStructuralDependencies(KLEPKeyId targetKeyId)
        {
            if (LastSelfModel == null)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' has no captured self-model. Call " +
                    "ObserveSelf with an accepted Agent trace first.");
            }

            return ProposeStructuralDependencies(LastSelfModel, targetKeyId);
        }

        /// <summary>
        /// Builds the same proposal against an explicit retained model, allowing
        /// deterministic questions about historical evidence without pretending
        /// that the model is current.
        /// </summary>
        public KLEPObserverStructuralDependencyProposal
            ProposeStructuralDependencies(
                KLEPObserverSelfModel selfModel,
                KLEPKeyId targetKeyId)
        {
            if (selfModel == null)
            {
                throw new ArgumentNullException(nameof(selfModel));
            }

            if (string.IsNullOrWhiteSpace(targetKeyId.Value))
            {
                throw new ArgumentException(
                    "A structural dependency question requires a stable Key ID.",
                    nameof(targetKeyId));
            }

            if (!IdComparer.Equals(
                    selfModel.ModelerStableId, StableId) ||
                !IdComparer.Equals(selfModel.ModelerVersion, Version))
            {
                throw new ArgumentException(
                    "The supplied self-model belongs to a different Observer " +
                    "identity or version.",
                    nameof(selfModel));
            }

            if (isObserving || isReasoning)
            {
                throw new InvalidOperationException(
                    $"Observer '{StableId}' cannot reason recursively or while " +
                    "guidance observation is active.");
            }

            LastReasoningTrace = null;
            isReasoning = true;
            try
            {
                KLEPObserverDependencyGraph graph =
                    new DependencyGraphBuilder(selfModel).Build(targetKeyId);
                var proposal = new KLEPObserverStructuralDependencyProposal(
                    selfModel,
                    targetKeyId,
                    graph);
                LastReasoningTrace = new KLEPObserverReasoningTrace(
                    StableId,
                    Version,
                    proposal);
                return proposal;
            }
            finally
            {
                isReasoning = false;
            }
        }

        private static List<KLEPExecutableDefinition> CollectEligibleTargets(
            KLEPAgentGuidanceContext context)
        {
            var eligibleIds = new HashSet<string>(
                context.Request.EligibleExecutableIds,
                IdComparer);
            var targets = new List<KLEPExecutableDefinition>();
            foreach (KLEPExecutableDefinition definition in context.RootExecutables)
            {
                bool supportedKind = definition.Kind == KLEPExecutableKind.Action ||
                    definition.Kind == KLEPExecutableKind.Goal;
                if (supportedKind &&
                    definition.ExecutionMode == KLEPExecutionMode.Solo &&
                    eligibleIds.Contains(definition.StableId))
                {
                    targets.Add(definition);
                }
            }

            targets.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return targets;
        }

        private static IReadOnlyList<string> CollectLockIds(
            KLEPExecutableDefinition target)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(IdComparer);
            AppendLockIds(target.ValidationLocks, ids, seen);
            AppendLockIds(target.ExecutionLocks, ids, seen);
            return new ReadOnlyCollection<string>(ids);
        }

        private static void AppendLockIds(
            IReadOnlyList<KLEPLock> locks,
            List<string> ids,
            HashSet<string> seen)
        {
            for (int index = 0; index < locks.Count; index++)
            {
                if (seen.Add(locks[index].StableId))
                {
                    ids.Add(locks[index].StableId);
                }
            }
        }

        private static ReadOnlyCollection<EvidenceSourceRegistration> CopySources(
            IEnumerable<IKLEPObserverEvidenceSource> source)
        {
            var copy = new List<EvidenceSourceRegistration>();
            var ids = new HashSet<string>(IdComparer);
            if (source != null)
            {
                foreach (IKLEPObserverEvidenceSource item in source)
                {
                    if (item == null)
                    {
                        throw new ArgumentException(
                            "Observer evidence sources cannot contain null.",
                            nameof(source));
                    }

                    string stableId = KLEPObserverValidation.RequireId(
                        item.StableId, nameof(source));
                    string version = KLEPObserverValidation.RequireId(
                        item.Version, nameof(source));
                    if (!ids.Add(stableId))
                    {
                        throw new ArgumentException(
                            $"Observer evidence source ID '{stableId}' occurs more than once.",
                            nameof(source));
                    }

                    copy.Add(new EvidenceSourceRegistration(
                        item, stableId, version));
                }
            }

            copy.Sort((left, right) => IdComparer.Compare(
                left.StableId, right.StableId));
            return new ReadOnlyCollection<EvidenceSourceRegistration>(copy);
        }

        private static void ValidateSourceIdentity(
            EvidenceSourceRegistration registration)
        {
            if (!IdComparer.Equals(
                    registration.StableId,
                    registration.Source.StableId) ||
                !IdComparer.Equals(
                    registration.Version,
                    registration.Source.Version))
            {
                throw new InvalidOperationException(
                    $"Evidence source '{registration.StableId}' changed its " +
                    "stable ID or version after Observer registration.");
            }
        }

        /// <summary>
        /// Pure backward graph slicer. The graph is deliberately AND/OR evidence
        /// instead of a chosen action sequence. In particular, Not is retained as
        /// an absence requirement and Goal-owned nodes are never promoted to root
        /// scheduling candidates.
        /// </summary>
        private sealed class DependencyGraphBuilder
        {
            private readonly KLEPObserverSelfModel model;
            private readonly Dictionary<string, KLEPObserverDependencyNode>
                nodesById = new Dictionary<string, KLEPObserverDependencyNode>(
                    IdComparer);
            private readonly List<KLEPObserverDependencyEdge> edges =
                new List<KLEPObserverDependencyEdge>();
            private readonly List<KLEPObserverReasoningDiagnostic> diagnostics =
                new List<KLEPObserverReasoningDiagnostic>();
            private readonly HashSet<string> diagnosticIds =
                new HashSet<string>(IdComparer);
            private readonly HashSet<KLEPKeyId> expandedKeys =
                new HashSet<KLEPKeyId>();
            private readonly HashSet<KLEPKeyId> activeKeys =
                new HashSet<KLEPKeyId>();
            private readonly List<KLEPKeyId> activePath = new List<KLEPKeyId>();

            internal DependencyGraphBuilder(KLEPObserverSelfModel model)
            {
                this.model = model ?? throw new ArgumentNullException(nameof(model));
            }

            internal KLEPObserverDependencyGraph Build(KLEPKeyId targetKeyId)
            {
                EnsureKeyNode(targetKeyId);
                ExpandKey(targetKeyId, true);

                var nodes = new List<KLEPObserverDependencyNode>(nodesById.Values);
                nodes.Sort((left, right) => IdComparer.Compare(
                    left.NodeId, right.NodeId));
                edges.Sort(CompareEdges);
                diagnostics.Sort(CompareDiagnostics);
                return new KLEPObserverDependencyGraph(
                    nodes,
                    edges,
                    diagnostics);
            }

            private void ExpandKey(KLEPKeyId keyId, bool requiresProducer)
            {
                if (activeKeys.Contains(keyId))
                {
                    AddDiagnostic(
                        KLEPObserverReasoningDiagnosticCode.DependencyCycle,
                        "key:" + keyId.Value,
                        "Dependency cycle: " + FormatCycle(keyId) + ".");
                    return;
                }

                if (expandedKeys.Contains(keyId))
                {
                    DiagnoseMissingProducerWhenRequired(
                        keyId,
                        requiresProducer);
                    return;
                }

                activeKeys.Add(keyId);
                activePath.Add(keyId);
                try
                {
                    string keyNodeId = EnsureKeyNode(keyId);
                    if (!model.StructuralMap.TryGetKeyRelation(
                            keyId, out KLEPStructuralKeyRelation relation) ||
                        relation.Producers.Count == 0)
                    {
                        DiagnoseMissingProducerWhenRequired(
                            keyId,
                            requiresProducer);
                        return;
                    }

                    foreach (KLEPStructuralKeyProducerRelation producer in
                             relation.Producers)
                    {
                        string producerNodeId = AddProducerNode(keyId, producer);
                        AddEdge(
                            keyNodeId,
                            producerNodeId,
                            KLEPObserverDependencyEdgeKind.ProducedBy);

                        KLEPExecutableStructuralNode executable = producer.Producer;
                        if (!executable.IsRoot)
                        {
                            AddDiagnostic(
                                KLEPObserverReasoningDiagnosticCode
                                    .GoalOwnedProducerNotIndependentlySchedulable,
                                executable.Path,
                                $"Goal-owned Executable '{executable.StableExecutableId}' " +
                                "is structural producer evidence but is not an " +
                                "independently schedulable root.");
                        }

                        foreach (KLEPStructuralLockSnapshot sourceLock in
                                 executable.Locks)
                        {
                            AddExpression(
                                producerNodeId,
                                sourceLock,
                                sourceLock.Expression,
                                KLEPObserverDependencyEdgeKind
                                    .ProducerRequiresLock,
                                false);
                        }
                    }
                }
                finally
                {
                    activePath.RemoveAt(activePath.Count - 1);
                    activeKeys.Remove(keyId);
                    expandedKeys.Add(keyId);
                }
            }

            private void AddExpression(
                string parentNodeId,
                KLEPStructuralLockSnapshot sourceLock,
                KLEPStructuralLockExpressionSnapshot expression,
                KLEPObserverDependencyEdgeKind incomingEdgeKind,
                bool isNegativeContext)
            {
                string expressionNodeId = AddExpressionNode(
                    parentNodeId,
                    sourceLock,
                    expression,
                    isNegativeContext);
                AddEdge(parentNodeId, expressionNodeId, incomingEdgeKind);

                if (expression.Kind == KLEPLockExpressionKind.KeyPresent)
                {
                    if (!expression.KeyId.HasValue ||
                        string.IsNullOrWhiteSpace(expression.KeyId.Value.Value))
                    {
                        AddDiagnostic(
                            KLEPObserverReasoningDiagnosticCode
                                .UnsupportedLockExpression,
                            ExpressionPath(sourceLock, expression),
                            "A KeyPresent structural expression has no stable Key ID.");
                        return;
                    }

                    KLEPKeyId dependencyKey = expression.KeyId.Value;
                    string dependencyNodeId = EnsureKeyNode(dependencyKey);
                    AddEdge(
                        expressionNodeId,
                        dependencyNodeId,
                        isNegativeContext
                            ? KLEPObserverDependencyEdgeKind.RequiresAbsentKey
                            : KLEPObserverDependencyEdgeKind.RequiresPresentKey);
                    if (isNegativeContext)
                    {
                        AddDiagnostic(
                            KLEPObserverReasoningDiagnosticCode.NegativeRequirement,
                            ExpressionPath(sourceLock, expression),
                            $"Lock '{sourceLock.StableId}' requires Key " +
                            $"'{dependencyKey}' to be absent. DeclaredOutputs do " +
                            "not describe how to make or keep that absence true.");
                    }

                    if (!isNegativeContext)
                    {
                        ExpandKey(dependencyKey, true);
                    }

                    return;
                }

                KLEPObserverDependencyEdgeKind childEdgeKind;
                bool childNegativeContext = isNegativeContext;
                switch (expression.Kind)
                {
                    case KLEPLockExpressionKind.All:
                        childEdgeKind = KLEPObserverDependencyEdgeKind.AllChild;
                        break;
                    case KLEPLockExpressionKind.Any:
                        childEdgeKind = KLEPObserverDependencyEdgeKind.AnyChild;
                        break;
                    case KLEPLockExpressionKind.Not:
                        childEdgeKind = KLEPObserverDependencyEdgeKind.NotChild;
                        childNegativeContext = !isNegativeContext;
                        break;
                    default:
                        AddDiagnostic(
                            KLEPObserverReasoningDiagnosticCode
                                .UnsupportedLockExpression,
                            ExpressionPath(sourceLock, expression),
                            $"Lock expression kind '{expression.Kind}' is not " +
                            "supported by structural dependency reasoning.");
                        return;
                }

                foreach (KLEPStructuralLockExpressionSnapshot child in
                         expression.Children)
                {
                    AddExpression(
                        expressionNodeId,
                        sourceLock,
                        child,
                        childEdgeKind,
                        childNegativeContext);
                }
            }

            private string EnsureKeyNode(KLEPKeyId keyId)
            {
                string nodeId = "key|" + Token(keyId.Value);
                if (!nodesById.ContainsKey(nodeId))
                {
                    nodesById.Add(
                        nodeId,
                        new KLEPObserverDependencyNode(
                            nodeId,
                            KLEPObserverDependencyNodeKind.Key,
                            keyId,
                            model.KeySnapshot.Contains(keyId),
                            null,
                            null,
                            null,
                            null,
                            false));
                }

                return nodeId;
            }

            private string AddProducerNode(
                KLEPKeyId producedKey,
                KLEPStructuralKeyProducerRelation relation)
            {
                string nodeId = "producer|" + Token(producedKey.Value) + "|" +
                    Token(relation.Producer.Path) + "|" +
                    relation.GuaranteedOutput.OutputIndex.ToString(
                        CultureInfo.InvariantCulture);
                if (!nodesById.ContainsKey(nodeId))
                {
                    nodesById.Add(
                        nodeId,
                        new KLEPObserverDependencyNode(
                            nodeId,
                            KLEPObserverDependencyNodeKind.Producer,
                            producedKey,
                            model.KeySnapshot.Contains(producedKey),
                            relation.Producer,
                            relation.GuaranteedOutput,
                            null,
                            null,
                            false));
                }

                return nodeId;
            }

            private string AddExpressionNode(
                string producerNodeId,
                KLEPStructuralLockSnapshot sourceLock,
                KLEPStructuralLockExpressionSnapshot expression,
                bool isNegativeContext)
            {
                string nodeId = "expression|" + Token(producerNodeId) + "|" +
                    ((int)sourceLock.Group).ToString(CultureInfo.InvariantCulture) +
                    "|" + sourceLock.GroupIndex.ToString(
                        CultureInfo.InvariantCulture) + "|" +
                    Token(sourceLock.StableId) + "|" +
                    Token(expression.ExpressionPath);
                if (!nodesById.ContainsKey(nodeId))
                {
                    nodesById.Add(
                        nodeId,
                        new KLEPObserverDependencyNode(
                            nodeId,
                            KLEPObserverDependencyNodeKind.LockExpression,
                            expression.KeyId,
                            expression.KeyId.HasValue &&
                                model.KeySnapshot.Contains(expression.KeyId.Value),
                            null,
                            null,
                            sourceLock,
                            expression,
                            isNegativeContext));
                }

                return nodeId;
            }

            private void AddEdge(
                string fromNodeId,
                string toNodeId,
                KLEPObserverDependencyEdgeKind kind)
            {
                edges.Add(new KLEPObserverDependencyEdge(
                    fromNodeId,
                    toNodeId,
                    kind));
            }

            private void AddDiagnostic(
                KLEPObserverReasoningDiagnosticCode code,
                string path,
                string message)
            {
                string id = ((int)code).ToString(CultureInfo.InvariantCulture) +
                    "|" + (path ?? string.Empty) + "|" +
                    (message ?? string.Empty);
                if (diagnosticIds.Add(id))
                {
                    diagnostics.Add(new KLEPObserverReasoningDiagnostic(
                        code,
                        path,
                        message));
                }
            }

            private void DiagnoseMissingProducerWhenRequired(
                KLEPKeyId keyId,
                bool requiresProducer)
            {
                if (!requiresProducer || model.KeySnapshot.Contains(keyId))
                {
                    return;
                }

                if (model.StructuralMap.TryGetKeyRelation(
                        keyId, out KLEPStructuralKeyRelation relation) &&
                    relation.Producers.Count > 0)
                {
                    return;
                }

                AddDiagnostic(
                    KLEPObserverReasoningDiagnosticCode.MissingProducer,
                    "key:" + keyId.Value,
                    $"No mapped Executable guarantees Key '{keyId}'.");
            }

            private string FormatCycle(KLEPKeyId repeatedKey)
            {
                var parts = new List<string>();
                int start = activePath.IndexOf(repeatedKey);
                if (start < 0)
                {
                    start = 0;
                }

                for (int index = start; index < activePath.Count; index++)
                {
                    parts.Add(activePath[index].Value);
                }

                parts.Add(repeatedKey.Value);
                return string.Join(" -> ", parts);
            }

            private static string ExpressionPath(
                KLEPStructuralLockSnapshot sourceLock,
                KLEPStructuralLockExpressionSnapshot expression)
            {
                return "lock:" + sourceLock.StableId + "/" +
                    expression.ExpressionPath;
            }

            private static string Token(string value)
            {
                value = value ?? string.Empty;
                return value.Length.ToString(CultureInfo.InvariantCulture) +
                    ":" + value;
            }

            private static int CompareEdges(
                KLEPObserverDependencyEdge left,
                KLEPObserverDependencyEdge right)
            {
                int comparison = IdComparer.Compare(
                    left.FromNodeId, right.FromNodeId);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = ((int)left.Kind).CompareTo((int)right.Kind);
                return comparison != 0
                    ? comparison
                    : IdComparer.Compare(left.ToNodeId, right.ToNodeId);
            }

            private static int CompareDiagnostics(
                KLEPObserverReasoningDiagnostic left,
                KLEPObserverReasoningDiagnostic right)
            {
                int comparison = ((int)left.Code).CompareTo((int)right.Code);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = IdComparer.Compare(left.Path, right.Path);
                return comparison != 0
                    ? comparison
                    : IdComparer.Compare(left.Message, right.Message);
            }
        }

        private sealed class EvidenceSourceRegistration
        {
            internal EvidenceSourceRegistration(
                IKLEPObserverEvidenceSource source,
                string stableId,
                string version)
            {
                Source = source;
                StableId = stableId;
                Version = version;
            }

            internal IKLEPObserverEvidenceSource Source { get; }
            internal string StableId { get; }
            internal string Version { get; }
        }
    }
}
