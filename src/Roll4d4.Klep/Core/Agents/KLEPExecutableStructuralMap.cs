using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// One proposed root registration supplied to the pure structural mapper.
    /// Invalid proposal values are retained so Capture can return diagnostics
    /// instead of throwing before an inspectable result exists.
    /// </summary>
    public sealed class KLEPExecutableCatalogRoot
    {
        public KLEPExecutableCatalogRoot(
            KLEPExecutableBase executable,
            string tenureId)
        {
            Executable = executable;
            TenureId = tenureId ?? string.Empty;
        }

        public KLEPExecutableBase Executable { get; }
        public string TenureId { get; }
    }

    public enum KLEPStructuralMapDiagnosticCode
    {
        MissingCatalogRevision,
        MissingRootCollection,
        NullRootEntry,
        NullRootExecutable,
        MissingTenureId,
        DuplicateTenureId,
        DuplicateExecutableStableId,
        RecursiveExecutableCycle,
        NullGoalLayer,
        NullGoalChild,
        UnsupportedLockExpression
    }

    public sealed class KLEPStructuralMapDiagnostic
    {
        internal KLEPStructuralMapDiagnostic(
            KLEPStructuralMapDiagnosticCode code,
            string path,
            string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public KLEPStructuralMapDiagnosticCode Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Exact ordinal identity for one captured catalog proposal. CanonicalId is
    /// length-prefixed source material; Value is its deterministic FNV-1a digest.
    /// Equality never relies on the digest alone.
    /// </summary>
    public sealed class KLEPStructuralMapFingerprint :
        IEquatable<KLEPStructuralMapFingerprint>,
        IComparable<KLEPStructuralMapFingerprint>
    {
        internal KLEPStructuralMapFingerprint(string canonicalId)
        {
            CanonicalId = canonicalId ?? throw new ArgumentNullException(
                nameof(canonicalId));
            Value = StableDigest(canonicalId);
        }

        public string CanonicalId { get; }
        public string Value { get; }

        public int CompareTo(KLEPStructuralMapFingerprint other)
        {
            return other == null
                ? 1
                : StringComparer.Ordinal.Compare(CanonicalId, other.CanonicalId);
        }

        public bool Equals(KLEPStructuralMapFingerprint other)
        {
            return other != null &&
                StringComparer.Ordinal.Equals(CanonicalId, other.CanonicalId);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPStructuralMapFingerprint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                ulong digest = StableDigestValue(CanonicalId);
                return ((int)digest * 397) ^ (int)(digest >> 32);
            }
        }

        public override string ToString()
        {
            return Value;
        }

        private static string StableDigest(string value)
        {
            return StableDigestValue(value).ToString(
                "x16", CultureInfo.InvariantCulture);
        }

        private static ulong StableDigestValue(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (char character in value)
            {
                hash ^= (byte)(character & 0xff);
                hash *= prime;
                hash ^= (byte)(character >> 8);
                hash *= prime;
            }

            return hash;
        }
    }

    public sealed class KLEPStructuralLockExpressionSnapshot
    {
        private readonly ReadOnlyCollection<KLEPStructuralLockExpressionSnapshot>
            children;

        internal KLEPStructuralLockExpressionSnapshot(
            KLEPLockExpressionKind kind,
            string expressionPath,
            KLEPKeyId? keyId,
            IEnumerable<KLEPStructuralLockExpressionSnapshot> children)
        {
            Kind = kind;
            ExpressionPath = expressionPath ?? string.Empty;
            KeyId = keyId;
            this.children = KLEPStructuralMapCollections.Copy(children);
        }

        public KLEPLockExpressionKind Kind { get; }
        public string ExpressionPath { get; }
        public KLEPKeyId? KeyId { get; }
        public IReadOnlyList<KLEPStructuralLockExpressionSnapshot> Children =>
            children;
    }

    public sealed class KLEPStructuralLockSnapshot
    {
        internal KLEPStructuralLockSnapshot(
            KLEPExecutableLockGroup group,
            int groupIndex,
            string stableId,
            string displayName,
            float attractiveness,
            KLEPStructuralLockExpressionSnapshot expression)
        {
            Group = group;
            GroupIndex = groupIndex;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Attractiveness = attractiveness;
            Expression = expression ?? throw new ArgumentNullException(
                nameof(expression));
        }

        public KLEPExecutableLockGroup Group { get; }
        public int GroupIndex { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public float Attractiveness { get; }
        public KLEPStructuralLockExpressionSnapshot Expression { get; }
    }

    /// <summary>
    /// A successful-completion Key guarantee copied from DeclaredOutputs. This
    /// intentionally contains no payload and no invented add/replace/remove kind.
    /// </summary>
    public sealed class KLEPStructuralGuaranteedOutputSnapshot
    {
        internal KLEPStructuralGuaranteedOutputSnapshot(
            int outputIndex,
            KLEPKeyId keyId,
            KLEPKeyScope scope,
            KLEPKeyLifetime defaultLifetime)
        {
            OutputIndex = outputIndex;
            KeyId = keyId;
            Scope = scope;
            DefaultLifetime = defaultLifetime;
        }

        public int OutputIndex { get; }
        public KLEPKeyId KeyId { get; }
        public KLEPKeyScope Scope { get; }
        public KLEPKeyLifetime DefaultLifetime { get; }
    }

    public sealed class KLEPStructuralGoalLayerSnapshot
    {
        private readonly ReadOnlyCollection<KLEPExecutableStructuralNode> children;

        internal KLEPStructuralGoalLayerSnapshot(
            int layerIndex,
            KLEPGoalLayerRequirement requirement,
            IEnumerable<KLEPExecutableStructuralNode> children)
        {
            LayerIndex = layerIndex;
            Requirement = requirement;
            this.children = KLEPStructuralMapCollections.Copy(children);
        }

        public int LayerIndex { get; }
        public KLEPGoalLayerRequirement Requirement { get; }
        public IReadOnlyList<KLEPExecutableStructuralNode> Children => children;
    }

    /// <summary>
    /// Immutable recursive copy of one root or Goal-owned Executable. It retains
    /// topology and authored structural contracts, never a runtime object.
    /// </summary>
    public sealed class KLEPExecutableStructuralNode
    {
        private readonly ReadOnlyCollection<KLEPStructuralLockSnapshot> locks;
        private readonly ReadOnlyCollection<KLEPStructuralGuaranteedOutputSnapshot>
            guaranteedDeclaredOutputs;
        private readonly ReadOnlyCollection<KLEPStructuralGoalLayerSnapshot> goalLayers;

        internal KLEPExecutableStructuralNode(
            string path,
            string rootTenureId,
            string stableExecutableId,
            string displayName,
            KLEPExecutableKind kind,
            bool isGoalRecipe,
            KLEPExecutionMode executionMode,
            string parentPath,
            string parentExecutableId,
            int? parentLayerIndex,
            KLEPGoalLayerRequirement? parentLayerRequirement,
            int? parentChildIndex,
            bool traversalWasTruncated,
            IEnumerable<KLEPStructuralLockSnapshot> locks,
            IEnumerable<KLEPStructuralGuaranteedOutputSnapshot>
                guaranteedDeclaredOutputs,
            IEnumerable<KLEPStructuralGoalLayerSnapshot> goalLayers)
        {
            Path = path ?? string.Empty;
            RootTenureId = rootTenureId ?? string.Empty;
            StableExecutableId = stableExecutableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            IsGoalRecipe = isGoalRecipe;
            ExecutionMode = executionMode;
            ParentPath = parentPath;
            ParentExecutableId = parentExecutableId;
            ParentLayerIndex = parentLayerIndex;
            ParentLayerRequirement = parentLayerRequirement;
            ParentChildIndex = parentChildIndex;
            TraversalWasTruncated = traversalWasTruncated;
            this.locks = KLEPStructuralMapCollections.Copy(locks);
            this.guaranteedDeclaredOutputs =
                KLEPStructuralMapCollections.Copy(guaranteedDeclaredOutputs);
            this.goalLayers = KLEPStructuralMapCollections.Copy(goalLayers);
        }

        public string Path { get; }
        public string RootTenureId { get; }
        public string StableExecutableId { get; }
        public string DisplayName { get; }
        public KLEPExecutableKind Kind { get; }
        public bool IsGoalRecipe { get; }
        public KLEPExecutionMode ExecutionMode { get; }
        public string ParentPath { get; }
        public string ParentExecutableId { get; }
        public bool IsRoot => ParentPath == null;
        public int? ParentLayerIndex { get; }
        public KLEPGoalLayerRequirement? ParentLayerRequirement { get; }
        public int? ParentChildIndex { get; }
        public bool TraversalWasTruncated { get; }
        public IReadOnlyList<KLEPStructuralLockSnapshot> Locks => locks;
        public IReadOnlyList<KLEPStructuralGuaranteedOutputSnapshot>
            GuaranteedDeclaredOutputs => guaranteedDeclaredOutputs;
        public IReadOnlyList<KLEPStructuralGoalLayerSnapshot> GoalLayers => goalLayers;
    }

    public sealed class KLEPExecutableCatalogSnapshot
    {
        private readonly ReadOnlyCollection<KLEPExecutableStructuralNode> roots;
        private readonly ReadOnlyCollection<KLEPExecutableStructuralNode> nodes;
        private readonly ReadOnlyCollection<KLEPStructuralMapDiagnostic> diagnostics;

        internal KLEPExecutableCatalogSnapshot(
            string proposedCatalogRevision,
            IEnumerable<KLEPExecutableStructuralNode> roots,
            IEnumerable<KLEPExecutableStructuralNode> nodes,
            IEnumerable<KLEPStructuralMapDiagnostic> diagnostics,
            KLEPStructuralMapFingerprint fingerprint)
        {
            ProposedCatalogRevision = proposedCatalogRevision ?? string.Empty;
            this.roots = KLEPStructuralMapCollections.Copy(roots);
            this.nodes = KLEPStructuralMapCollections.Copy(nodes);
            this.diagnostics = KLEPStructuralMapCollections.Copy(diagnostics);
            Fingerprint = fingerprint ?? throw new ArgumentNullException(
                nameof(fingerprint));
        }

        public string ProposedCatalogRevision { get; }
        public IReadOnlyList<KLEPExecutableStructuralNode> Roots => roots;
        public IReadOnlyList<KLEPExecutableStructuralNode> Nodes => nodes;
        public IReadOnlyList<KLEPStructuralMapDiagnostic> Diagnostics => diagnostics;
        public bool IsValid => diagnostics.Count == 0;
        public KLEPStructuralMapFingerprint Fingerprint { get; }
    }

    public sealed class KLEPStructuralKeyProducerRelation
    {
        internal KLEPStructuralKeyProducerRelation(
            KLEPExecutableStructuralNode producer,
            KLEPStructuralGuaranteedOutputSnapshot guaranteedOutput)
        {
            Producer = producer ?? throw new ArgumentNullException(nameof(producer));
            GuaranteedOutput = guaranteedOutput ?? throw new ArgumentNullException(
                nameof(guaranteedOutput));
        }

        public KLEPExecutableStructuralNode Producer { get; }
        public KLEPStructuralGuaranteedOutputSnapshot GuaranteedOutput { get; }
    }

    public sealed class KLEPStructuralKeyConsumerRelation
    {
        internal KLEPStructuralKeyConsumerRelation(
            KLEPExecutableStructuralNode consumer,
            KLEPStructuralLockSnapshot sourceLock,
            string expressionPath)
        {
            Consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            SourceLock = sourceLock ?? throw new ArgumentNullException(nameof(sourceLock));
            ExpressionPath = expressionPath ?? string.Empty;
        }

        public KLEPExecutableStructuralNode Consumer { get; }
        public KLEPStructuralLockSnapshot SourceLock { get; }
        public string ExpressionPath { get; }
    }

    public sealed class KLEPStructuralKeyRelation
    {
        private readonly ReadOnlyCollection<KLEPStructuralKeyProducerRelation> producers;
        private readonly ReadOnlyCollection<KLEPStructuralKeyConsumerRelation>
            positiveConsumers;
        private readonly ReadOnlyCollection<KLEPStructuralKeyConsumerRelation>
            negativeConsumers;

        internal KLEPStructuralKeyRelation(
            KLEPKeyId keyId,
            IEnumerable<KLEPStructuralKeyProducerRelation> producers,
            IEnumerable<KLEPStructuralKeyConsumerRelation> positiveConsumers,
            IEnumerable<KLEPStructuralKeyConsumerRelation> negativeConsumers)
        {
            KeyId = keyId;
            this.producers = KLEPStructuralMapCollections.Copy(producers);
            this.positiveConsumers = KLEPStructuralMapCollections.Copy(
                positiveConsumers);
            this.negativeConsumers = KLEPStructuralMapCollections.Copy(
                negativeConsumers);
        }

        public KLEPKeyId KeyId { get; }
        public IReadOnlyList<KLEPStructuralKeyProducerRelation> Producers => producers;
        public IReadOnlyList<KLEPStructuralKeyConsumerRelation> PositiveConsumers =>
            positiveConsumers;
        public IReadOnlyList<KLEPStructuralKeyConsumerRelation> NegativeConsumers =>
            negativeConsumers;
    }

    /// <summary>
    /// A direct structural candidate whose successful completion guarantees the
    /// requested Key ID. No eligibility or present-world feasibility is asserted.
    /// </summary>
    public sealed class KLEPDirectSuccessfulCandidate
    {
        internal KLEPDirectSuccessfulCandidate(
            KLEPExecutableStructuralNode executable,
            KLEPStructuralGuaranteedOutputSnapshot guaranteedOutput)
        {
            Executable = executable ?? throw new ArgumentNullException(
                nameof(executable));
            GuaranteedOutput = guaranteedOutput ?? throw new ArgumentNullException(
                nameof(guaranteedOutput));
        }

        public KLEPExecutableStructuralNode Executable { get; }
        public KLEPStructuralGuaranteedOutputSnapshot GuaranteedOutput { get; }
        public string ExecutableStableId => Executable.StableExecutableId;
        public string Path => Executable.Path;
        public string RootTenureId => Executable.RootTenureId;
    }

    public sealed class KLEPDirectSuccessfulCandidateProjection
    {
        private readonly ReadOnlyCollection<KLEPDirectSuccessfulCandidate> candidates;
        private readonly ReadOnlyCollection<KLEPStructuralMapDiagnostic> diagnostics;

        internal KLEPDirectSuccessfulCandidateProjection(
            KLEPKeyId targetKeyId,
            KLEPStructuralMapFingerprint catalogFingerprint,
            bool sourceMapIsValid,
            IEnumerable<KLEPDirectSuccessfulCandidate> candidates,
            IEnumerable<KLEPStructuralMapDiagnostic> diagnostics)
        {
            TargetKeyId = targetKeyId;
            CatalogFingerprint = catalogFingerprint ?? throw new ArgumentNullException(
                nameof(catalogFingerprint));
            SourceMapIsValid = sourceMapIsValid;
            this.candidates = KLEPStructuralMapCollections.Copy(candidates);
            this.diagnostics = KLEPStructuralMapCollections.Copy(diagnostics);
        }

        public KLEPKeyId TargetKeyId { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public bool SourceMapIsValid { get; }
        public IReadOnlyList<KLEPDirectSuccessfulCandidate> Candidates => candidates;
        public IReadOnlyList<KLEPStructuralMapDiagnostic> Diagnostics => diagnostics;
    }

    public sealed class KLEPExecutableStructuralMap
    {
        private readonly ReadOnlyCollection<KLEPStructuralKeyRelation> keyRelations;
        private readonly Dictionary<KLEPKeyId, KLEPStructuralKeyRelation>
            keyRelationsById;
        private readonly Dictionary<string, KLEPExecutableStructuralNode> nodesById;

        internal KLEPExecutableStructuralMap(
            KLEPExecutableCatalogSnapshot snapshot,
            IEnumerable<KLEPStructuralKeyRelation> keyRelations)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.keyRelations = KLEPStructuralMapCollections.Copy(keyRelations);
            keyRelationsById = new Dictionary<KLEPKeyId, KLEPStructuralKeyRelation>();
            foreach (KLEPStructuralKeyRelation relation in this.keyRelations)
            {
                keyRelationsById.Add(relation.KeyId, relation);
            }

            nodesById = new Dictionary<string, KLEPExecutableStructuralNode>(
                StringComparer.Ordinal);
            if (snapshot.IsValid)
            {
                foreach (KLEPExecutableStructuralNode node in snapshot.Nodes)
                {
                    nodesById.Add(node.StableExecutableId, node);
                }
            }
        }

        public KLEPExecutableCatalogSnapshot Snapshot { get; }
        public bool IsValid => Snapshot.IsValid;
        public IReadOnlyList<KLEPStructuralMapDiagnostic> Diagnostics =>
            Snapshot.Diagnostics;
        public KLEPStructuralMapFingerprint Fingerprint => Snapshot.Fingerprint;
        public IReadOnlyList<KLEPStructuralKeyRelation> KeyRelations => keyRelations;

        public bool TryGetKeyRelation(
            KLEPKeyId keyId,
            out KLEPStructuralKeyRelation relation)
        {
            return keyRelationsById.TryGetValue(keyId, out relation);
        }

        public bool TryGetExecutable(
            string stableExecutableId,
            out KLEPExecutableStructuralNode node)
        {
            if (string.IsNullOrWhiteSpace(stableExecutableId))
            {
                node = null;
                return false;
            }

            return nodesById.TryGetValue(stableExecutableId, out node);
        }

        public KLEPDirectSuccessfulCandidateProjection
            ProjectDirectSuccessfulCandidates(KLEPKeyId targetKeyId)
        {
            if (string.IsNullOrWhiteSpace(targetKeyId.Value))
            {
                throw new ArgumentException(
                    "A direct candidate projection requires a stable Key ID.",
                    nameof(targetKeyId));
            }

            var candidates = new List<KLEPDirectSuccessfulCandidate>();
            if (IsValid && keyRelationsById.TryGetValue(
                    targetKeyId, out KLEPStructuralKeyRelation relation))
            {
                foreach (KLEPStructuralKeyProducerRelation producer in
                         relation.Producers)
                {
                    candidates.Add(new KLEPDirectSuccessfulCandidate(
                        producer.Producer,
                        producer.GuaranteedOutput));
                }
            }

            return new KLEPDirectSuccessfulCandidateProjection(
                targetKeyId,
                Fingerprint,
                IsValid,
                candidates,
                Diagnostics);
        }
    }

    /// <summary>
    /// Pure structural Observer boundary. Implementations validate and map the
    /// supplied immutable snapshot; they never evaluate current Locks or fire.
    /// </summary>
    public interface IKLEPExecutableStructuralObserver
    {
        string StableId { get; }
        string Version { get; }
        KLEPExecutableStructuralMap ObserveStructure(
            KLEPExecutableCatalogSnapshot snapshot);
    }

    /// <summary>
    /// Deterministic Core fallback used when no higher-cognition Observer is
    /// injected. It performs exactly the baseline structural mapping contract.
    /// </summary>
    public sealed class KLEPBaselineStructuralObserver :
        IKLEPExecutableStructuralObserver
    {
        private KLEPBaselineStructuralObserver()
        {
        }

        public static KLEPBaselineStructuralObserver Instance { get; } =
            new KLEPBaselineStructuralObserver();

        public string StableId => "klep.observer.structural.baseline";
        public string Version => "1";

        public KLEPExecutableStructuralMap ObserveStructure(
            KLEPExecutableCatalogSnapshot snapshot)
        {
            return KLEPExecutableStructuralMapper.Build(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
        }
    }

    /// <summary>
    /// Deterministic baseline mapper. It snapshots authored structure and builds
    /// direct Key-ID relations only; it never reads a Key snapshot, evaluates a
    /// Lock, mutates a runtime, or invents a payload/removal transition.
    /// </summary>
    public static class KLEPExecutableStructuralMapper
    {
        public static KLEPExecutableStructuralMap Build(
            string proposedCatalogRevision,
            IEnumerable<KLEPExecutableCatalogRoot> roots)
        {
            return Build(Capture(proposedCatalogRevision, roots));
        }

        public static KLEPExecutableStructuralMap Build(
            KLEPExecutableCatalogSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new KLEPExecutableStructuralMap(
                snapshot,
                BuildKeyRelations(snapshot.Nodes));
        }

        public static KLEPExecutableCatalogSnapshot Capture(
            string proposedCatalogRevision,
            IEnumerable<KLEPExecutableCatalogRoot> roots)
        {
            var context = new CaptureContext(proposedCatalogRevision);
            context.CaptureRoots(roots);
            return context.Complete();
        }

        private static IReadOnlyList<KLEPStructuralKeyRelation> BuildKeyRelations(
            IReadOnlyList<KLEPExecutableStructuralNode> nodes)
        {
            var accumulators = new Dictionary<KLEPKeyId, RelationAccumulator>();
            foreach (KLEPExecutableStructuralNode node in nodes)
            {
                foreach (KLEPStructuralGuaranteedOutputSnapshot output in
                         node.GuaranteedDeclaredOutputs)
                {
                    GetAccumulator(accumulators, output.KeyId).Producers.Add(
                        new KLEPStructuralKeyProducerRelation(node, output));
                }

                foreach (KLEPStructuralLockSnapshot sourceLock in node.Locks)
                {
                    AppendConsumers(
                        accumulators,
                        node,
                        sourceLock,
                        sourceLock.Expression,
                        positive: true);
                }
            }

            var keys = new List<KLEPKeyId>(accumulators.Keys);
            keys.Sort((left, right) => left.CompareTo(right));
            var relations = new List<KLEPStructuralKeyRelation>(keys.Count);
            foreach (KLEPKeyId key in keys)
            {
                RelationAccumulator accumulator = accumulators[key];
                accumulator.Sort();
                relations.Add(new KLEPStructuralKeyRelation(
                    key,
                    accumulator.Producers,
                    accumulator.PositiveConsumers,
                    accumulator.NegativeConsumers));
            }

            return new ReadOnlyCollection<KLEPStructuralKeyRelation>(relations);
        }

        private static void AppendConsumers(
            Dictionary<KLEPKeyId, RelationAccumulator> accumulators,
            KLEPExecutableStructuralNode node,
            KLEPStructuralLockSnapshot sourceLock,
            KLEPStructuralLockExpressionSnapshot expression,
            bool positive)
        {
            bool childPolarity = expression.Kind == KLEPLockExpressionKind.Not
                ? !positive
                : positive;
            if (expression.Kind == KLEPLockExpressionKind.KeyPresent &&
                expression.KeyId.HasValue)
            {
                var relation = new KLEPStructuralKeyConsumerRelation(
                    node,
                    sourceLock,
                    expression.ExpressionPath);
                RelationAccumulator accumulator = GetAccumulator(
                    accumulators, expression.KeyId.Value);
                if (positive)
                {
                    accumulator.PositiveConsumers.Add(relation);
                }
                else
                {
                    accumulator.NegativeConsumers.Add(relation);
                }

                return;
            }

            foreach (KLEPStructuralLockExpressionSnapshot child in
                     expression.Children)
            {
                AppendConsumers(
                    accumulators,
                    node,
                    sourceLock,
                    child,
                    childPolarity);
            }
        }

        private static RelationAccumulator GetAccumulator(
            Dictionary<KLEPKeyId, RelationAccumulator> accumulators,
            KLEPKeyId keyId)
        {
            if (!accumulators.TryGetValue(
                    keyId, out RelationAccumulator accumulator))
            {
                accumulator = new RelationAccumulator();
                accumulators.Add(keyId, accumulator);
            }

            return accumulator;
        }

        private sealed class CaptureContext
        {
            private readonly string proposedCatalogRevision;
            private readonly List<KLEPExecutableStructuralNode> roots =
                new List<KLEPExecutableStructuralNode>();
            private readonly List<KLEPExecutableStructuralNode> nodes =
                new List<KLEPExecutableStructuralNode>();
            private readonly List<KLEPStructuralMapDiagnostic> diagnostics =
                new List<KLEPStructuralMapDiagnostic>();
            private readonly Dictionary<string, string> firstExecutablePaths =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> tenureIds =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<KLEPExecutableBase> activeExecutables =
                new HashSet<KLEPExecutableBase>(ReferenceComparer.Instance);

            internal CaptureContext(string proposedCatalogRevision)
            {
                this.proposedCatalogRevision = proposedCatalogRevision ??
                    string.Empty;
                if (string.IsNullOrWhiteSpace(proposedCatalogRevision))
                {
                    AddDiagnostic(
                        KLEPStructuralMapDiagnosticCode.MissingCatalogRevision,
                        "catalog",
                        "A proposed catalog revision must be non-empty.");
                }
            }

            internal void CaptureRoots(
                IEnumerable<KLEPExecutableCatalogRoot> source)
            {
                if (source == null)
                {
                    AddDiagnostic(
                        KLEPStructuralMapDiagnosticCode.MissingRootCollection,
                        "catalog",
                        "A root Executable collection is required.");
                    return;
                }

                var proposals = new List<IndexedRoot>();
                int sourceIndex = 0;
                foreach (KLEPExecutableCatalogRoot root in source)
                {
                    proposals.Add(new IndexedRoot(root, sourceIndex));
                    sourceIndex++;
                }

                proposals.Sort(IndexedRoot.Compare);
                foreach (IndexedRoot indexed in proposals)
                {
                    string proposalPath = "root-proposal[" +
                        indexed.SourceIndex.ToString(CultureInfo.InvariantCulture) +
                        "]";
                    if (indexed.Root == null)
                    {
                        AddDiagnostic(
                            KLEPStructuralMapDiagnosticCode.NullRootEntry,
                            proposalPath,
                            "A root catalog entry cannot be null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(indexed.Root.TenureId))
                    {
                        AddDiagnostic(
                            KLEPStructuralMapDiagnosticCode.MissingTenureId,
                            proposalPath,
                            "Every root proposal requires a non-empty tenure ID.");
                    }
                    else if (!tenureIds.Add(indexed.Root.TenureId))
                    {
                        AddDiagnostic(
                            KLEPStructuralMapDiagnosticCode.DuplicateTenureId,
                            proposalPath,
                            $"Root tenure ID '{indexed.Root.TenureId}' is duplicated.");
                    }

                    if (indexed.Root.Executable == null)
                    {
                        AddDiagnostic(
                            KLEPStructuralMapDiagnosticCode.NullRootExecutable,
                            proposalPath,
                            "A root catalog entry requires an Executable.");
                        continue;
                    }

                    string rootPath = BuildRootPath(
                        indexed.Root.TenureId,
                        indexed.Root.Executable.StableId);
                    roots.Add(CaptureNode(
                        indexed.Root.Executable,
                        indexed.Root.TenureId,
                        rootPath,
                        null,
                        null,
                        null,
                        null,
                        null));
                }
            }

            internal KLEPExecutableCatalogSnapshot Complete()
            {
                nodes.Sort((left, right) => StringComparer.Ordinal.Compare(
                    left.Path, right.Path));
                diagnostics.Sort(CompareDiagnostics);
                string canonicalId = BuildCanonicalId(
                    proposedCatalogRevision,
                    roots,
                    diagnostics);
                return new KLEPExecutableCatalogSnapshot(
                    proposedCatalogRevision,
                    roots,
                    nodes,
                    diagnostics,
                    new KLEPStructuralMapFingerprint(canonicalId));
            }

            private KLEPExecutableStructuralNode CaptureNode(
                KLEPExecutableBase executable,
                string rootTenureId,
                string path,
                string parentPath,
                string parentExecutableId,
                int? parentLayerIndex,
                KLEPGoalLayerRequirement? parentLayerRequirement,
                int? parentChildIndex)
            {
                if (firstExecutablePaths.TryGetValue(
                        executable.StableId, out string firstPath))
                {
                    AddDiagnostic(
                        KLEPStructuralMapDiagnosticCode.DuplicateExecutableStableId,
                        path,
                        $"Executable ID '{executable.StableId}' first appeared at " +
                        $"'{firstPath}'.");
                }
                else
                {
                    firstExecutablePaths.Add(executable.StableId, path);
                }

                bool recursiveCycle = activeExecutables.Contains(executable);
                if (recursiveCycle)
                {
                    AddDiagnostic(
                        KLEPStructuralMapDiagnosticCode.RecursiveExecutableCycle,
                        path,
                        $"Executable '{executable.StableId}' recursively contains itself.");
                }

                List<KLEPStructuralLockSnapshot> lockSnapshots = CaptureLocks(
                    executable, path);
                List<KLEPStructuralGuaranteedOutputSnapshot> outputSnapshots =
                    CaptureOutputs(executable);
                var layerSnapshots = new List<KLEPStructuralGoalLayerSnapshot>();

                if (!recursiveCycle && executable is KLEPGoal goal)
                {
                    activeExecutables.Add(executable);
                    try
                    {
                        for (int layerIndex = 0;
                             layerIndex < goal.Layers.Count;
                             layerIndex++)
                        {
                            KLEPGoalLayer layer = goal.Layers[layerIndex];
                            if (layer == null)
                            {
                                AddDiagnostic(
                                    KLEPStructuralMapDiagnosticCode.NullGoalLayer,
                                    path,
                                    $"Goal layer {layerIndex} is null.");
                                continue;
                            }

                            var children = new List<KLEPExecutableStructuralNode>();
                            for (int childIndex = 0;
                                 childIndex < layer.Children.Count;
                                 childIndex++)
                            {
                                KLEPExecutableBase child = layer.Children[childIndex];
                                string childPath = BuildChildPath(
                                    path,
                                    layerIndex,
                                    childIndex,
                                    child == null ? "<null>" : child.StableId);
                                if (child == null)
                                {
                                    AddDiagnostic(
                                        KLEPStructuralMapDiagnosticCode.NullGoalChild,
                                        childPath,
                                        "A Goal layer child cannot be null.");
                                    continue;
                                }

                                children.Add(CaptureNode(
                                    child,
                                    rootTenureId,
                                    childPath,
                                    path,
                                    executable.StableId,
                                    layerIndex,
                                    layer.Requirement,
                                    childIndex));
                            }

                            layerSnapshots.Add(new KLEPStructuralGoalLayerSnapshot(
                                layerIndex,
                                layer.Requirement,
                                children));
                        }
                    }
                    finally
                    {
                        activeExecutables.Remove(executable);
                    }
                }

                var node = new KLEPExecutableStructuralNode(
                    path,
                    rootTenureId,
                    executable.StableId,
                    executable.DisplayName,
                    executable.Kind,
                    executable is KLEPGoal,
                    executable.ExecutionMode,
                    parentPath,
                    parentExecutableId,
                    parentLayerIndex,
                    parentLayerRequirement,
                    parentChildIndex,
                    recursiveCycle,
                    lockSnapshots,
                    outputSnapshots,
                    layerSnapshots);
                nodes.Add(node);
                return node;
            }

            private List<KLEPStructuralLockSnapshot> CaptureLocks(
                KLEPExecutableBase executable,
                string nodePath)
            {
                var snapshots = new List<KLEPStructuralLockSnapshot>();
                AppendLockGroup(
                    snapshots,
                    executable.ValidationLocks,
                    KLEPExecutableLockGroup.Validation,
                    nodePath);
                AppendLockGroup(
                    snapshots,
                    executable.ExecutionLocks,
                    KLEPExecutableLockGroup.Execution,
                    nodePath);
                return snapshots;
            }

            private void AppendLockGroup(
                List<KLEPStructuralLockSnapshot> destination,
                IReadOnlyList<KLEPLock> locks,
                KLEPExecutableLockGroup group,
                string nodePath)
            {
                for (int index = 0; index < locks.Count; index++)
                {
                    KLEPLock source = locks[index];
                    destination.Add(new KLEPStructuralLockSnapshot(
                        group,
                        index,
                        source.StableId,
                        source.DisplayName,
                        source.Attractiveness,
                        CaptureExpression(
                            source.Expression,
                            "root",
                            nodePath + "/lock:" + source.StableId)));
                }
            }

            private KLEPStructuralLockExpressionSnapshot CaptureExpression(
                KLEPLockExpression expression,
                string expressionPath,
                string diagnosticPath)
            {
                if (expression is KLEPKeyPresent present)
                {
                    return new KLEPStructuralLockExpressionSnapshot(
                        KLEPLockExpressionKind.KeyPresent,
                        expressionPath,
                        new KLEPKeyId(present.StableKeyId),
                        null);
                }

                var children = new List<KLEPStructuralLockExpressionSnapshot>();
                if (expression is KLEPAll all)
                {
                    for (int index = 0; index < all.Children.Count; index++)
                    {
                        children.Add(CaptureExpression(
                            all.Children[index],
                            expressionPath + ".all[" +
                                index.ToString(CultureInfo.InvariantCulture) + "]",
                            diagnosticPath));
                    }

                    return new KLEPStructuralLockExpressionSnapshot(
                        KLEPLockExpressionKind.All,
                        expressionPath,
                        null,
                        children);
                }

                if (expression is KLEPAny any)
                {
                    for (int index = 0; index < any.Children.Count; index++)
                    {
                        children.Add(CaptureExpression(
                            any.Children[index],
                            expressionPath + ".any[" +
                                index.ToString(CultureInfo.InvariantCulture) + "]",
                            diagnosticPath));
                    }

                    return new KLEPStructuralLockExpressionSnapshot(
                        KLEPLockExpressionKind.Any,
                        expressionPath,
                        null,
                        children);
                }

                if (expression is KLEPNot not)
                {
                    children.Add(CaptureExpression(
                        not.Child,
                        expressionPath + ".not",
                        diagnosticPath));
                    return new KLEPStructuralLockExpressionSnapshot(
                        KLEPLockExpressionKind.Not,
                        expressionPath,
                        null,
                        children);
                }

                AddDiagnostic(
                    KLEPStructuralMapDiagnosticCode.UnsupportedLockExpression,
                    diagnosticPath,
                    "The Lock contains an unsupported expression implementation.");
                return new KLEPStructuralLockExpressionSnapshot(
                    expression.Kind,
                    expressionPath,
                    null,
                    children);
            }

            private static List<KLEPStructuralGuaranteedOutputSnapshot>
                CaptureOutputs(KLEPExecutableBase executable)
            {
                var outputs = new List<KLEPStructuralGuaranteedOutputSnapshot>(
                    executable.DeclaredOutputs.Count);
                for (int index = 0;
                     index < executable.DeclaredOutputs.Count;
                     index++)
                {
                    KLEPKeyDefinition output = executable.DeclaredOutputs[index];
                    outputs.Add(new KLEPStructuralGuaranteedOutputSnapshot(
                        index,
                        output.Id,
                        output.Scope,
                        output.DefaultLifetime));
                }

                return outputs;
            }

            private void AddDiagnostic(
                KLEPStructuralMapDiagnosticCode code,
                string path,
                string message)
            {
                diagnostics.Add(new KLEPStructuralMapDiagnostic(code, path, message));
            }
        }

        private sealed class RelationAccumulator
        {
            internal List<KLEPStructuralKeyProducerRelation> Producers { get; } =
                new List<KLEPStructuralKeyProducerRelation>();
            internal List<KLEPStructuralKeyConsumerRelation> PositiveConsumers { get; } =
                new List<KLEPStructuralKeyConsumerRelation>();
            internal List<KLEPStructuralKeyConsumerRelation> NegativeConsumers { get; } =
                new List<KLEPStructuralKeyConsumerRelation>();

            internal void Sort()
            {
                Producers.Sort((left, right) =>
                    CompareProducer(left, right));
                PositiveConsumers.Sort((left, right) =>
                    CompareConsumer(left, right));
                NegativeConsumers.Sort((left, right) =>
                    CompareConsumer(left, right));
            }
        }

        private sealed class IndexedRoot
        {
            internal IndexedRoot(KLEPExecutableCatalogRoot root, int sourceIndex)
            {
                Root = root;
                SourceIndex = sourceIndex;
            }

            internal KLEPExecutableCatalogRoot Root { get; }
            internal int SourceIndex { get; }

            internal static int Compare(IndexedRoot left, IndexedRoot right)
            {
                string leftId = left.Root?.Executable?.StableId ?? string.Empty;
                string rightId = right.Root?.Executable?.StableId ?? string.Empty;
                int comparison = StringComparer.Ordinal.Compare(leftId, rightId);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = StringComparer.Ordinal.Compare(
                    left.Root?.TenureId ?? string.Empty,
                    right.Root?.TenureId ?? string.Empty);
                return comparison != 0
                    ? comparison
                    : left.SourceIndex.CompareTo(right.SourceIndex);
            }
        }

        private sealed class ReferenceComparer :
            IEqualityComparer<KLEPExecutableBase>
        {
            internal static ReferenceComparer Instance { get; } =
                new ReferenceComparer();

            public bool Equals(KLEPExecutableBase left, KLEPExecutableBase right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(KLEPExecutableBase value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        private static int CompareProducer(
            KLEPStructuralKeyProducerRelation left,
            KLEPStructuralKeyProducerRelation right)
        {
            int comparison = StringComparer.Ordinal.Compare(
                left.Producer.Path, right.Producer.Path);
            return comparison != 0
                ? comparison
                : left.GuaranteedOutput.OutputIndex.CompareTo(
                    right.GuaranteedOutput.OutputIndex);
        }

        private static int CompareConsumer(
            KLEPStructuralKeyConsumerRelation left,
            KLEPStructuralKeyConsumerRelation right)
        {
            int comparison = StringComparer.Ordinal.Compare(
                left.Consumer.Path, right.Consumer.Path);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = ((int)left.SourceLock.Group).CompareTo(
                (int)right.SourceLock.Group);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.SourceLock.GroupIndex.CompareTo(
                right.SourceLock.GroupIndex);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(
                    left.ExpressionPath, right.ExpressionPath);
        }

        private static int CompareDiagnostics(
            KLEPStructuralMapDiagnostic left,
            KLEPStructuralMapDiagnostic right)
        {
            int comparison = ((int)left.Code).CompareTo((int)right.Code);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.Path, right.Path);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.Message, right.Message);
        }

        private static string BuildRootPath(string tenureId, string stableId)
        {
            return "root(" + (tenureId ?? string.Empty) + "):" +
                (stableId ?? string.Empty);
        }

        private static string BuildChildPath(
            string parentPath,
            int layerIndex,
            int childIndex,
            string stableId)
        {
            return parentPath + "/layer[" +
                layerIndex.ToString(CultureInfo.InvariantCulture) +
                "]/child[" + childIndex.ToString(CultureInfo.InvariantCulture) +
                "]:" + stableId;
        }

        private static string BuildCanonicalId(
            string revision,
            IReadOnlyList<KLEPExecutableStructuralNode> roots,
            IReadOnlyList<KLEPStructuralMapDiagnostic> diagnostics)
        {
            var writer = new CanonicalWriter();
            writer.Append("klep-structural-map-v2");
            writer.Append(revision);
            writer.Append(roots.Count);
            foreach (KLEPExecutableStructuralNode root in roots)
            {
                AppendNode(writer, root);
            }

            writer.Append(diagnostics.Count);
            foreach (KLEPStructuralMapDiagnostic diagnostic in diagnostics)
            {
                writer.Append((int)diagnostic.Code);
                writer.Append(diagnostic.Path);
                writer.Append(diagnostic.Message);
            }

            return writer.ToString();
        }

        private static void AppendNode(
            CanonicalWriter writer,
            KLEPExecutableStructuralNode node)
        {
            writer.Append(node.Path);
            writer.Append(node.RootTenureId);
            writer.Append(node.StableExecutableId);
            writer.Append(node.DisplayName);
            writer.Append((int)node.Kind);
            writer.Append(node.IsGoalRecipe);
            writer.Append((int)node.ExecutionMode);
            writer.Append(node.ParentPath);
            writer.Append(node.ParentExecutableId);
            writer.Append(node.ParentLayerIndex);
            writer.Append(node.ParentLayerRequirement.HasValue
                ? (int?)node.ParentLayerRequirement.Value
                : null);
            writer.Append(node.ParentChildIndex);
            writer.Append(node.TraversalWasTruncated);
            writer.Append(node.Locks.Count);
            foreach (KLEPStructuralLockSnapshot sourceLock in node.Locks)
            {
                writer.Append((int)sourceLock.Group);
                writer.Append(sourceLock.GroupIndex);
                writer.Append(sourceLock.StableId);
                writer.Append(sourceLock.DisplayName);
                writer.Append(sourceLock.Attractiveness.ToString(
                    "R", CultureInfo.InvariantCulture));
                AppendExpression(writer, sourceLock.Expression);
            }

            writer.Append(node.GuaranteedDeclaredOutputs.Count);
            foreach (KLEPStructuralGuaranteedOutputSnapshot output in
                     node.GuaranteedDeclaredOutputs)
            {
                writer.Append(output.OutputIndex);
                writer.Append(output.KeyId.Value);
                writer.Append((int)output.Scope);
                writer.Append((int)output.DefaultLifetime);
            }

            writer.Append(node.GoalLayers.Count);
            foreach (KLEPStructuralGoalLayerSnapshot layer in node.GoalLayers)
            {
                writer.Append(layer.LayerIndex);
                writer.Append((int)layer.Requirement);
                writer.Append(layer.Children.Count);
                foreach (KLEPExecutableStructuralNode child in layer.Children)
                {
                    AppendNode(writer, child);
                }
            }
        }

        private static void AppendExpression(
            CanonicalWriter writer,
            KLEPStructuralLockExpressionSnapshot expression)
        {
            writer.Append((int)expression.Kind);
            writer.Append(expression.ExpressionPath);
            writer.Append(expression.KeyId.HasValue
                ? expression.KeyId.Value.Value
                : null);
            writer.Append(expression.Children.Count);
            foreach (KLEPStructuralLockExpressionSnapshot child in
                     expression.Children)
            {
                AppendExpression(writer, child);
            }
        }

        private sealed class CanonicalWriter
        {
            private readonly StringBuilder builder = new StringBuilder();

            internal void Append(string value)
            {
                if (value == null)
                {
                    builder.Append("-1:|");
                    return;
                }

                builder.Append(value.Length.ToString(
                    CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(value);
                builder.Append('|');
            }

            internal void Append(int value)
            {
                Append(value.ToString(CultureInfo.InvariantCulture));
            }

            internal void Append(int? value)
            {
                Append(value.HasValue
                    ? value.Value.ToString(CultureInfo.InvariantCulture)
                    : null);
            }

            internal void Append(bool value)
            {
                Append(value ? "1" : "0");
            }

            public override string ToString()
            {
                return builder.ToString();
            }
        }
    }

    internal static class KLEPStructuralMapCollections
    {
        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source)
        {
            var copy = new List<T>();
            if (source != null)
            {
                foreach (T item in source)
                {
                    copy.Add(item);
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }
}
