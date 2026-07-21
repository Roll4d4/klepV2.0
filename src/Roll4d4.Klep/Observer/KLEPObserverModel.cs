using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Observer
{
    public sealed class KLEPObserverConfiguration
    {
        public KLEPObserverConfiguration(
            float polishAmount = 1f,
            float minimumDirectionValue = 0f)
        {
            if (float.IsNaN(polishAmount) ||
                float.IsInfinity(polishAmount) ||
                polishAmount <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(polishAmount));
            }

            if (float.IsNaN(minimumDirectionValue) ||
                float.IsInfinity(minimumDirectionValue) ||
                minimumDirectionValue < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDirectionValue));
            }

            PolishAmount = polishAmount;
            MinimumDirectionValue = minimumDirectionValue;
        }

        public static KLEPObserverConfiguration Default { get; } =
            new KLEPObserverConfiguration();

        public float PolishAmount { get; }
        public float MinimumDirectionValue { get; }
    }

    /// <summary>
    /// One project-owned holistic consideration. The Observer supplies source
    /// provenance from the evidence provider that returned it.
    /// </summary>
    public sealed class KLEPObserverEvidence
    {
        public KLEPObserverEvidence(
            string targetExecutableId,
            float value,
            string explanation = "")
        {
            TargetExecutableId = KLEPObserverValidation.RequireId(
                targetExecutableId, nameof(targetExecutableId));
            Value = KLEPObserverValidation.RequireFinite(value, nameof(value));
            Explanation = explanation ?? string.Empty;
        }

        public string TargetExecutableId { get; }
        public float Value { get; }
        public string Explanation { get; }
    }

    public interface IKLEPObserverEvidenceSource
    {
        string StableId { get; }
        string Version { get; }
        IReadOnlyList<KLEPObserverEvidence> Evaluate(
            KLEPObserverEvidenceContext context);
    }

    /// <summary>
    /// Read-only view presented to project evidence providers. The target list
    /// contains only currently eligible root Solo Goals and actions.
    /// </summary>
    public sealed class KLEPObserverEvidenceContext
    {
        private readonly ReadOnlyCollection<KLEPExecutableDefinition> targets;

        internal KLEPObserverEvidenceContext(
            KLEPAgentGuidanceContext guidance,
            IEnumerable<KLEPExecutableDefinition> targets)
        {
            Guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
            var copy = new List<KLEPExecutableDefinition>();
            if (targets != null)
            {
                foreach (KLEPExecutableDefinition target in targets)
                {
                    copy.Add(target ?? throw new ArgumentException(
                        "Observer targets cannot contain null.", nameof(targets)));
                }
            }

            copy.Sort((left, right) => StringComparer.Ordinal.Compare(
                left.StableId, right.StableId));
            this.targets = new ReadOnlyCollection<KLEPExecutableDefinition>(copy);
        }

        public KLEPAgentGuidanceContext Guidance { get; }
        public KLEPAgentTickTrace AgentTrace => Guidance.Trace;
        public KLEPGuidanceRequest Request => Guidance.Request;
        public KLEPKeySnapshot KeySnapshot => Guidance.Trace.Decision.KeySnapshot;
        public IReadOnlyList<KLEPExecutableDefinition> EligibleTargets => targets;
        public IReadOnlyList<KLEPAgentExperience> AgentExperiences =>
            Guidance.Experiences;
    }

    public sealed class KLEPObserverEvidenceTrace
    {
        internal KLEPObserverEvidenceTrace(
            string sourceId,
            string sourceVersion,
            KLEPObserverEvidence evidence)
        {
            SourceId = sourceId;
            SourceVersion = sourceVersion;
            TargetExecutableId = evidence.TargetExecutableId;
            Value = evidence.Value;
            Explanation = evidence.Explanation;
        }

        public string SourceId { get; }
        public string SourceVersion { get; }
        public string TargetExecutableId { get; }
        public float Value { get; }
        public string Explanation { get; }
    }

    public sealed class KLEPObserverTargetTrace
    {
        private readonly ReadOnlyCollection<KLEPObserverEvidenceTrace> evidence;

        internal KLEPObserverTargetTrace(
            KLEPExecutableDefinition definition,
            IEnumerable<KLEPObserverEvidenceTrace> evidence,
            double holisticValue)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            var copy = new List<KLEPObserverEvidenceTrace>();
            if (evidence != null)
            {
                foreach (KLEPObserverEvidenceTrace item in evidence)
                {
                    copy.Add(item ?? throw new ArgumentException(
                        "Observer target evidence cannot contain null.",
                        nameof(evidence)));
                }
            }

            this.evidence = new ReadOnlyCollection<KLEPObserverEvidenceTrace>(copy);
            HolisticValue = KLEPObserverValidation.RequireFinite(
                holisticValue, nameof(holisticValue));
        }

        public KLEPExecutableDefinition Definition { get; }
        public string ExecutableStableId => Definition.StableId;
        public KLEPExecutableKind Kind => Definition.Kind;
        public IReadOnlyList<KLEPObserverEvidenceTrace> Evidence => evidence;
        public double HolisticValue { get; }
    }

    public enum KLEPObserverAbstentionReason
    {
        None,
        NoEligibleGoalOrAction,
        NoEvidence,
        NoBeneficialDirection
    }

    public sealed class KLEPObserverTrace
    {
        private readonly ReadOnlyCollection<KLEPObserverTargetTrace> targets;

        internal KLEPObserverTrace(
            string observerStableId,
            string observerVersion,
            KLEPGuidanceRequest request,
            IEnumerable<KLEPObserverTargetTrace> targets,
            string selectedExecutableId,
            KLEPObserverAbstentionReason abstentionReason,
            KLEPGuidanceAdvice advice)
        {
            ObserverStableId = observerStableId;
            ObserverVersion = observerVersion;
            Request = request ?? throw new ArgumentNullException(nameof(request));
            var copy = new List<KLEPObserverTargetTrace>();
            if (targets != null)
            {
                foreach (KLEPObserverTargetTrace target in targets)
                {
                    copy.Add(target ?? throw new ArgumentException(
                        "Observer traces cannot contain a null target.",
                        nameof(targets)));
                }
            }

            this.targets = new ReadOnlyCollection<KLEPObserverTargetTrace>(copy);
            SelectedExecutableId = selectedExecutableId;
            AbstentionReason = abstentionReason;
            Advice = advice;
        }

        public string ObserverStableId { get; }
        public string ObserverVersion { get; }
        public KLEPGuidanceRequest Request { get; }
        public IReadOnlyList<KLEPObserverTargetTrace> Targets => targets;
        public string SelectedExecutableId { get; }
        public KLEPObserverAbstentionReason AbstentionReason { get; }
        public KLEPGuidanceAdvice Advice { get; }
        public bool ProvidedDirection => Advice != null;
    }

    /// <summary>
    /// One immutable, evidence-bound view of the Agent's accepted self. The
    /// structural map is always the active assessment retained by the supplied
    /// Agent trace, never the attempted assessment of a rejected proposal.
    /// </summary>
    public sealed class KLEPObserverSelfModel
    {
        internal KLEPObserverSelfModel(
            string modelerStableId,
            string modelerVersion,
            KLEPAgentTickTrace agentTrace)
        {
            ModelerStableId = KLEPObserverValidation.RequireId(
                modelerStableId, nameof(modelerStableId));
            ModelerVersion = KLEPObserverValidation.RequireId(
                modelerVersion, nameof(modelerVersion));
            AgentTrace = agentTrace ?? throw new ArgumentNullException(
                nameof(agentTrace));

            KLEPDecisionTrace decision = AgentTrace.Decision ??
                throw new ArgumentException(
                    "An Observer self-model requires one Agent decision trace.",
                    nameof(agentTrace));
            KLEPStructuralMapDecisionTrace structural = decision.StructuralMap ??
                throw new ArgumentException(
                    "An Observer self-model requires structural-map evidence.",
                    nameof(agentTrace));
            StructuralMap = structural.ActiveAssessment ??
                throw new ArgumentException(
                    "An Observer self-model requires one accepted active " +
                    "structural assessment.",
                    nameof(agentTrace));
            if (!StructuralMap.IsValid)
            {
                throw new ArgumentException(
                    "An Observer self-model cannot retain an invalid active " +
                    "structural assessment.",
                    nameof(agentTrace));
            }

            StructuralObserverStableId = KLEPObserverValidation.RequireId(
                structural.ObserverStableId,
                nameof(agentTrace));
            StructuralObserverVersion = KLEPObserverValidation.RequireId(
                structural.ObserverVersion,
                nameof(agentTrace));
            CatalogRevision = KLEPObserverValidation.RequireId(
                StructuralMap.Snapshot.ProposedCatalogRevision,
                nameof(agentTrace));
            CatalogFingerprint = StructuralMap.Fingerprint ??
                throw new ArgumentException(
                    "An Observer self-model requires a catalog fingerprint.",
                    nameof(agentTrace));
            KeySnapshot = decision.KeySnapshot ?? throw new ArgumentException(
                "An Observer self-model requires one immutable Key snapshot.",
                nameof(agentTrace));
            if (decision.CycleIndex != KeySnapshot.Tick)
            {
                throw new ArgumentException(
                    "The Agent decision cycle and Key snapshot Tick must match.",
                    nameof(agentTrace));
            }

            KLEPGuidanceEvidenceFingerprint observedEvidence =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(KeySnapshot);
            if (AgentTrace.EvidenceFingerprint == null ||
                !AgentTrace.EvidenceFingerprint.Equals(observedEvidence))
            {
                throw new ArgumentException(
                    "The Agent trace evidence fingerprint does not describe its " +
                    "Key snapshot.",
                    nameof(agentTrace));
            }

            EvidenceFingerprint = observedEvidence;
            CycleIndex = decision.CycleIndex;
            WaveIndex = KeySnapshot.WaveIndex;
        }

        public string ModelerStableId { get; }
        public string ModelerVersion { get; }
        public string StructuralObserverStableId { get; }
        public string StructuralObserverVersion { get; }
        public KLEPAgentTickTrace AgentTrace { get; }
        public KLEPExecutableStructuralMap StructuralMap { get; }
        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public KLEPKeySnapshot KeySnapshot { get; }
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint { get; }
        public long CycleIndex { get; }
        public int WaveIndex { get; }
    }

    public enum KLEPObserverDependencyNodeKind
    {
        Key,
        Producer,
        LockExpression
    }

    public enum KLEPObserverDependencyEdgeKind
    {
        ProducedBy,
        ProducerRequiresLock,
        AllChild,
        AnyChild,
        NotChild,
        RequiresPresentKey,
        RequiresAbsentKey
    }

    public enum KLEPObserverReasoningDiagnosticCode
    {
        MissingProducer,
        DependencyCycle,
        NegativeRequirement,
        GoalOwnedProducerNotIndependentlySchedulable,
        UnsupportedLockExpression
    }

    /// <summary>
    /// One node in a structural dependency proposal. Optional source properties
    /// are populated according to Kind. Every retained Core object is already an
    /// immutable structural snapshot or immutable successful-output guarantee.
    /// </summary>
    public sealed class KLEPObserverDependencyNode
    {
        internal KLEPObserverDependencyNode(
            string nodeId,
            KLEPObserverDependencyNodeKind kind,
            KLEPKeyId? keyId,
            bool isPresentInCurrentEvidence,
            KLEPExecutableStructuralNode producer,
            KLEPStructuralGuaranteedOutputSnapshot guaranteedOutput,
            KLEPStructuralLockSnapshot sourceLock,
            KLEPStructuralLockExpressionSnapshot sourceExpression,
            bool isNegativeContext)
        {
            NodeId = KLEPObserverValidation.RequireId(nodeId, nameof(nodeId));
            Kind = kind;
            KeyId = keyId;
            IsPresentInCurrentEvidence = isPresentInCurrentEvidence;
            Producer = producer;
            GuaranteedOutput = guaranteedOutput;
            SourceLock = sourceLock;
            SourceExpression = sourceExpression;
            IsNegativeContext = isNegativeContext;
        }

        public string NodeId { get; }
        public KLEPObserverDependencyNodeKind Kind { get; }
        public KLEPKeyId? KeyId { get; }
        public bool IsPresentInCurrentEvidence { get; }
        public KLEPExecutableStructuralNode Producer { get; }
        public KLEPStructuralGuaranteedOutputSnapshot GuaranteedOutput { get; }
        public KLEPStructuralLockSnapshot SourceLock { get; }
        public KLEPStructuralLockExpressionSnapshot SourceExpression { get; }
        public bool IsNegativeContext { get; }
        public bool IsIndependentlySchedulable =>
            Kind == KLEPObserverDependencyNodeKind.Producer &&
            Producer != null &&
            Producer.IsRoot;
    }

    public sealed class KLEPObserverDependencyEdge
    {
        internal KLEPObserverDependencyEdge(
            string fromNodeId,
            string toNodeId,
            KLEPObserverDependencyEdgeKind kind)
        {
            FromNodeId = KLEPObserverValidation.RequireId(
                fromNodeId, nameof(fromNodeId));
            ToNodeId = KLEPObserverValidation.RequireId(
                toNodeId, nameof(toNodeId));
            Kind = kind;
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public KLEPObserverDependencyEdgeKind Kind { get; }
    }

    public sealed class KLEPObserverReasoningDiagnostic
    {
        internal KLEPObserverReasoningDiagnostic(
            KLEPObserverReasoningDiagnosticCode code,
            string path,
            string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public KLEPObserverReasoningDiagnosticCode Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Immutable AND/OR dependency graph. It reports structural evidence only;
    /// it neither chooses a route nor claims current eligibility or feasibility.
    /// </summary>
    public sealed class KLEPObserverDependencyGraph
    {
        private readonly ReadOnlyCollection<KLEPObserverDependencyNode> nodes;
        private readonly ReadOnlyCollection<KLEPObserverDependencyEdge> edges;
        private readonly ReadOnlyCollection<KLEPObserverReasoningDiagnostic>
            diagnostics;
        private readonly Dictionary<string, KLEPObserverDependencyNode> nodesById;

        internal KLEPObserverDependencyGraph(
            IEnumerable<KLEPObserverDependencyNode> nodes,
            IEnumerable<KLEPObserverDependencyEdge> edges,
            IEnumerable<KLEPObserverReasoningDiagnostic> diagnostics)
        {
            this.nodes = CopyRequired(nodes, nameof(nodes));
            this.edges = CopyRequired(edges, nameof(edges));
            this.diagnostics = CopyRequired(diagnostics, nameof(diagnostics));
            nodesById = new Dictionary<string, KLEPObserverDependencyNode>(
                StringComparer.Ordinal);
            foreach (KLEPObserverDependencyNode node in this.nodes)
            {
                if (nodesById.ContainsKey(node.NodeId))
                {
                    throw new ArgumentException(
                        $"Dependency node ID '{node.NodeId}' occurs more than once.",
                        nameof(nodes));
                }

                nodesById.Add(node.NodeId, node);
            }

            foreach (KLEPObserverDependencyEdge edge in this.edges)
            {
                if (!nodesById.ContainsKey(edge.FromNodeId) ||
                    !nodesById.ContainsKey(edge.ToNodeId))
                {
                    throw new ArgumentException(
                        "Every dependency edge endpoint must name a retained node.",
                        nameof(edges));
                }
            }
        }

        public IReadOnlyList<KLEPObserverDependencyNode> Nodes => nodes;
        public IReadOnlyList<KLEPObserverDependencyEdge> Edges => edges;
        public IReadOnlyList<KLEPObserverReasoningDiagnostic> Diagnostics =>
            diagnostics;

        public bool TryGetNode(
            string nodeId,
            out KLEPObserverDependencyNode node)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                node = null;
                return false;
            }

            return nodesById.TryGetValue(nodeId, out node);
        }

        private static ReadOnlyCollection<T> CopyRequired<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (T item in source)
            {
                copy.Add(item ?? throw new ArgumentException(
                    "Observer dependency collections cannot contain null.",
                    parameterName));
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    /// <summary>
    /// One deterministic structural question result. It is a proposal for
    /// consideration, not an Agent plan, Goal recipe, or permission to execute.
    /// </summary>
    public sealed class KLEPObserverStructuralDependencyProposal
    {
        internal KLEPObserverStructuralDependencyProposal(
            KLEPObserverSelfModel selfModel,
            KLEPKeyId targetKeyId,
            KLEPObserverDependencyGraph graph)
        {
            SelfModel = selfModel ?? throw new ArgumentNullException(
                nameof(selfModel));
            if (string.IsNullOrWhiteSpace(targetKeyId.Value))
            {
                throw new ArgumentException(
                    "A structural dependency proposal requires a stable Key ID.",
                    nameof(targetKeyId));
            }

            TargetKeyId = targetKeyId;
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            TargetPresentInCurrentEvidence =
                selfModel.KeySnapshot.Contains(targetKeyId);
        }

        public KLEPObserverSelfModel SelfModel { get; }
        public KLEPKeyId TargetKeyId { get; }
        public bool TargetPresentInCurrentEvidence { get; }
        public KLEPObserverDependencyGraph Graph { get; }
        public IReadOnlyList<KLEPObserverReasoningDiagnostic> Diagnostics =>
            Graph.Diagnostics;
        public string CatalogRevision => SelfModel.CatalogRevision;
        public KLEPStructuralMapFingerprint CatalogFingerprint =>
            SelfModel.CatalogFingerprint;
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint =>
            SelfModel.EvidenceFingerprint;
    }

    public sealed class KLEPObserverReasoningTrace
    {
        internal KLEPObserverReasoningTrace(
            string observerStableId,
            string observerVersion,
            KLEPObserverStructuralDependencyProposal proposal)
        {
            ObserverStableId = KLEPObserverValidation.RequireId(
                observerStableId, nameof(observerStableId));
            ObserverVersion = KLEPObserverValidation.RequireId(
                observerVersion, nameof(observerVersion));
            Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        }

        public string ObserverStableId { get; }
        public string ObserverVersion { get; }
        public KLEPObserverSelfModel SelfModel => Proposal.SelfModel;
        public KLEPKeyId TargetKeyId => Proposal.TargetKeyId;
        public KLEPObserverStructuralDependencyProposal Proposal { get; }
        public string CatalogRevision => Proposal.CatalogRevision;
        public KLEPStructuralMapFingerprint CatalogFingerprint =>
            Proposal.CatalogFingerprint;
        public KLEPGuidanceEvidenceFingerprint EvidenceFingerprint =>
            Proposal.EvidenceFingerprint;
        public int NodeCount => Proposal.Graph.Nodes.Count;
        public int EdgeCount => Proposal.Graph.Edges.Count;
        public int DiagnosticCount => Proposal.Diagnostics.Count;
    }

    internal static class KLEPObserverValidation
    {
        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }

        internal static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static double RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
