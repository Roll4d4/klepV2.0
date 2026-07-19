using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// The only candidate-state horizon currently understood by Core. It is
    /// the complete Key-presence state immediately after one successful run,
    /// not the union of Keys emitted at arbitrary earlier points in that run.
    /// </summary>
    public enum KLEPCandidateStateProjectionHorizon
    {
        SuccessfulRunCompletion
    }

    public enum KLEPCandidateStateProjectionKind
    {
        Complete,
        Abstained
    }

    /// <summary>
    /// Immutable complete Key-presence state. Absence means known absent, so a
    /// projector can represent both successful additions and removals.
    /// </summary>
    public sealed class KLEPCompleteProjectedKeyState : IKLEPLockKeySource
    {
        private readonly ReadOnlyCollection<KLEPKeyId> presentKeyIds;
        private readonly HashSet<string> present =
            new HashSet<string>(StringComparer.Ordinal);

        public KLEPCompleteProjectedKeyState(params string[] presentKeyIds)
            : this(CopyIds(presentKeyIds))
        {
        }

        public KLEPCompleteProjectedKeyState(
            IEnumerable<KLEPKeyId> presentKeyIds)
        {
            if (presentKeyIds == null)
            {
                throw new ArgumentNullException(nameof(presentKeyIds));
            }

            var copy = new List<KLEPKeyId>();
            foreach (KLEPKeyId keyId in presentKeyIds)
            {
                if (string.IsNullOrWhiteSpace(keyId.Value))
                {
                    throw new ArgumentException(
                        "A projected presence state cannot contain an empty Key ID.",
                        nameof(presentKeyIds));
                }

                if (present.Add(keyId.Value))
                {
                    copy.Add(keyId);
                }
            }

            copy.Sort((left, right) => left.CompareTo(right));
            this.presentKeyIds = new ReadOnlyCollection<KLEPKeyId>(copy);
        }

        public IReadOnlyList<KLEPKeyId> PresentKeyIds => presentKeyIds;

        public bool Contains(string stableKeyId)
        {
            return !string.IsNullOrWhiteSpace(stableKeyId) &&
                present.Contains(stableKeyId);
        }

        private static IEnumerable<KLEPKeyId> CopyIds(string[] stableKeyIds)
        {
            if (stableKeyIds == null)
            {
                throw new ArgumentNullException(nameof(stableKeyIds));
            }

            var copy = new List<KLEPKeyId>(stableKeyIds.Length);
            for (int index = 0; index < stableKeyIds.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(stableKeyIds[index]))
                {
                    throw new ArgumentException(
                        "A projected presence state cannot contain an empty Key ID.",
                        nameof(stableKeyIds));
                }

                copy.Add(new KLEPKeyId(stableKeyIds[index]));
            }

            return copy;
        }
    }

    /// <summary>
    /// Immutable evidence supplied to one optional candidate-state Observer.
    /// Every response must retain these exact bindings.
    /// </summary>
    public sealed class KLEPCandidateStateProjectionRequest
    {
        internal KLEPCandidateStateProjectionRequest(
            string catalogRevision,
            KLEPStructuralMapFingerprint catalogFingerprint,
            string targetExecutableId,
            string targetRootTenureId,
            KLEPKeySnapshot currentSnapshot,
            KLEPCandidateStateProjectionHorizon horizon)
        {
            CatalogRevision = RequireId(
                catalogRevision, nameof(catalogRevision));
            CatalogFingerprint = catalogFingerprint ??
                throw new ArgumentNullException(nameof(catalogFingerprint));
            TargetExecutableId = RequireId(
                targetExecutableId, nameof(targetExecutableId));
            TargetRootTenureId = RequireId(
                targetRootTenureId, nameof(targetRootTenureId));
            CurrentSnapshot = currentSnapshot ??
                throw new ArgumentNullException(nameof(currentSnapshot));
            CurrentEvidenceFingerprint =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(currentSnapshot);
            Horizon = horizon;
        }

        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public string TargetExecutableId { get; }
        public string TargetRootTenureId { get; }
        public KLEPKeySnapshot CurrentSnapshot { get; }
        public KLEPGuidanceEvidenceFingerprint CurrentEvidenceFingerprint { get; }
        public KLEPCandidateStateProjectionHorizon Horizon { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty projection binding is required.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// One complete or explicitly abstaining candidate-state projection.
    /// Provenance repeats every request binding so the Agent can reject stale
    /// or cross-candidate evidence before it affects a score.
    /// </summary>
    public sealed class KLEPCandidateStateProjection
    {
        private KLEPCandidateStateProjection(
            KLEPCandidateStateProjectionKind kind,
            string projectorStableId,
            string projectorVersion,
            string catalogRevision,
            KLEPStructuralMapFingerprint catalogFingerprint,
            string targetExecutableId,
            string targetRootTenureId,
            KLEPGuidanceEvidenceFingerprint currentEvidenceFingerprint,
            KLEPCandidateStateProjectionHorizon horizon,
            KLEPCompleteProjectedKeyState completeState,
            string provenance,
            string abstentionReason)
        {
            Kind = kind;
            ProjectorStableId = RequireId(
                projectorStableId, nameof(projectorStableId));
            ProjectorVersion = RequireId(
                projectorVersion, nameof(projectorVersion));
            CatalogRevision = RequireId(
                catalogRevision, nameof(catalogRevision));
            CatalogFingerprint = catalogFingerprint ??
                throw new ArgumentNullException(nameof(catalogFingerprint));
            TargetExecutableId = RequireId(
                targetExecutableId, nameof(targetExecutableId));
            TargetRootTenureId = RequireId(
                targetRootTenureId, nameof(targetRootTenureId));
            CurrentEvidenceFingerprint = currentEvidenceFingerprint ??
                throw new ArgumentNullException(
                    nameof(currentEvidenceFingerprint));
            Horizon = horizon;
            Provenance = RequireId(provenance, nameof(provenance));

            if (kind == KLEPCandidateStateProjectionKind.Complete)
            {
                CompleteState = completeState ?? throw new ArgumentNullException(
                    nameof(completeState));
                AbstentionReason = string.Empty;
            }
            else if (kind == KLEPCandidateStateProjectionKind.Abstained)
            {
                if (completeState != null)
                {
                    throw new ArgumentException(
                        "An abstaining projection cannot contain a complete state.",
                        nameof(completeState));
                }

                CompleteState = null;
                AbstentionReason = RequireId(
                    abstentionReason, nameof(abstentionReason));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public KLEPCandidateStateProjectionKind Kind { get; }
        public string ProjectorStableId { get; }
        public string ProjectorVersion { get; }
        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public string TargetExecutableId { get; }
        public string TargetRootTenureId { get; }
        public KLEPGuidanceEvidenceFingerprint CurrentEvidenceFingerprint { get; }
        public KLEPCandidateStateProjectionHorizon Horizon { get; }
        public KLEPCompleteProjectedKeyState CompleteState { get; }
        public string Provenance { get; }
        public string AbstentionReason { get; }
        public bool IsComplete =>
            Kind == KLEPCandidateStateProjectionKind.Complete;

        public static KLEPCandidateStateProjection Complete(
            KLEPCandidateStateProjectionRequest request,
            string projectorStableId,
            string projectorVersion,
            KLEPCompleteProjectedKeyState completeState,
            string provenance)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new KLEPCandidateStateProjection(
                KLEPCandidateStateProjectionKind.Complete,
                projectorStableId,
                projectorVersion,
                request.CatalogRevision,
                request.CatalogFingerprint,
                request.TargetExecutableId,
                request.TargetRootTenureId,
                request.CurrentEvidenceFingerprint,
                request.Horizon,
                completeState,
                provenance,
                string.Empty);
        }

        public static KLEPCandidateStateProjection Abstain(
            KLEPCandidateStateProjectionRequest request,
            string projectorStableId,
            string projectorVersion,
            string reason,
            string provenance)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new KLEPCandidateStateProjection(
                KLEPCandidateStateProjectionKind.Abstained,
                projectorStableId,
                projectorVersion,
                request.CatalogRevision,
                request.CatalogFingerprint,
                request.TargetExecutableId,
                request.TargetRootTenureId,
                request.CurrentEvidenceFingerprint,
                request.Horizon,
                null,
                provenance,
                reason);
        }

        /// <summary>
        /// Testable/project-facing factory for a response with explicit
        /// bindings. The Agent still validates every field against its request.
        /// </summary>
        public static KLEPCandidateStateProjection CreateBoundResponse(
            KLEPCandidateStateProjectionKind kind,
            string projectorStableId,
            string projectorVersion,
            string catalogRevision,
            KLEPStructuralMapFingerprint catalogFingerprint,
            string targetExecutableId,
            string targetRootTenureId,
            KLEPGuidanceEvidenceFingerprint currentEvidenceFingerprint,
            KLEPCandidateStateProjectionHorizon horizon,
            KLEPCompleteProjectedKeyState completeState,
            string provenance,
            string abstentionReason = "")
        {
            return new KLEPCandidateStateProjection(
                kind,
                projectorStableId,
                projectorVersion,
                catalogRevision,
                catalogFingerprint,
                targetExecutableId,
                targetRootTenureId,
                currentEvidenceFingerprint,
                horizon,
                completeState,
                provenance,
                abstentionReason);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty projection identity or explanation is required.",
                    parameterName);
            }

            return value;
        }
    }

    public interface IKLEPCandidateStateProjectionObserver
    {
        string StableId { get; }
        string Version { get; }
        KLEPCandidateStateProjection ProjectCandidateState(
            KLEPCandidateStateProjectionRequest request);
    }

    /// <summary>
    /// Safe deterministic fallback. Structural DeclaredOutputs alone do not
    /// prove a complete final state, so the baseline explicitly abstains.
    /// </summary>
    public sealed class KLEPBaselineCandidateStateProjectionObserver :
        IKLEPCandidateStateProjectionObserver
    {
        private KLEPBaselineCandidateStateProjectionObserver()
        {
        }

        public static KLEPBaselineCandidateStateProjectionObserver Instance
            { get; } = new KLEPBaselineCandidateStateProjectionObserver();

        public string StableId => "klep.observer.candidate-state.baseline";
        public string Version => "1";

        public KLEPCandidateStateProjection ProjectCandidateState(
            KLEPCandidateStateProjectionRequest request)
        {
            return KLEPCandidateStateProjection.Abstain(
                request ?? throw new ArgumentNullException(nameof(request)),
                StableId,
                Version,
                "No complete successful-run-completion state projector was supplied.",
                "Baseline abstention; DeclaredOutputs are cumulative emissions, " +
                "not a complete final presence state.");
        }
    }

    /// <summary>
    /// One designer-authored desired condition used by the default projected-
    /// satisfaction policy. The Lock expression is evaluated as one boolean
    /// desire; its individual leaves are not counted as separate desires.
    /// </summary>
    public sealed class KLEPAgentDesire
    {
        public KLEPAgentDesire(
            string stableId,
            string version,
            KLEPLockExpression expression,
            float weight,
            float pressure = 1f,
            string explanation = "")
        {
            StableId = RequireId(stableId, nameof(stableId));
            Version = RequireId(version, nameof(version));
            Expression = expression ??
                throw new ArgumentNullException(nameof(expression));
            Weight = RequireFinite(weight, nameof(weight));
            Pressure = RequireFinite(pressure, nameof(pressure));
            Explanation = explanation ?? string.Empty;
        }

        public string StableId { get; }
        public string Version { get; }
        public KLEPLockExpression Expression { get; }
        public float Weight { get; }
        public float Pressure { get; }
        public string Explanation { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable identity is required.", parameterName);
            }

            return value;
        }

        private static float RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Immutable arithmetic evidence for one desire in one candidate
    /// projection. Truth values are retained as the contract's literal 0 or 1.
    /// </summary>
    public sealed class KLEPProjectedSatisfactionDesireTrace
    {
        internal KLEPProjectedSatisfactionDesireTrace(
            KLEPAgentDesire desire,
            int currentTruth,
            int? projectedTruth,
            double contribution)
        {
            Desire = desire ?? throw new ArgumentNullException(nameof(desire));
            if ((currentTruth != 0 && currentTruth != 1) ||
                (projectedTruth.HasValue &&
                 projectedTruth.Value != 0 && projectedTruth.Value != 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTruth),
                    "Projected-satisfaction truth must be zero or one.");
            }

            if (double.IsNaN(contribution) || double.IsInfinity(contribution))
            {
                throw new ArgumentOutOfRangeException(nameof(contribution));
            }

            CurrentTruth = currentTruth;
            ProjectedTruth = projectedTruth;
            Contribution = contribution;
        }

        public KLEPAgentDesire Desire { get; }
        public string DesireStableId => Desire.StableId;
        public string DesireVersion => Desire.Version;
        public KLEPLockExpression Expression => Desire.Expression;
        public float Weight => Desire.Weight;
        public float Pressure => Desire.Pressure;
        public string Explanation => Desire.Explanation;
        public int CurrentTruth { get; }
        public int? ProjectedTruth { get; }
        public bool IsProjectedTruthKnown => ProjectedTruth.HasValue;
        public double Contribution { get; }
    }

    /// <summary>
    /// One complete, immutable candidate evaluation. RawTotal retains the
    /// checked double-precision calculation; ScoreContribution is its checked
    /// representation in Core's Single-precision score model.
    /// </summary>
    public sealed class KLEPProjectedSatisfactionEvaluation
    {
        private readonly ReadOnlyCollection<
            KLEPProjectedSatisfactionDesireTrace> desires;

        internal KLEPProjectedSatisfactionEvaluation(
            string policyStableId,
            string policyVersion,
            string targetExecutableId,
            IEnumerable<KLEPProjectedSatisfactionDesireTrace> desires,
            double rawTotal,
            float scoreContribution,
            KLEPCandidateStateProjection projection = null)
        {
            PolicyStableId = RequireId(
                policyStableId, nameof(policyStableId));
            PolicyVersion = RequireId(policyVersion, nameof(policyVersion));
            TargetExecutableId = RequireId(
                targetExecutableId, nameof(targetExecutableId));
            if (double.IsNaN(rawTotal) || double.IsInfinity(rawTotal))
            {
                throw new ArgumentOutOfRangeException(nameof(rawTotal));
            }

            if (float.IsNaN(scoreContribution) ||
                float.IsInfinity(scoreContribution))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scoreContribution));
            }

            var copy = new List<KLEPProjectedSatisfactionDesireTrace>();
            if (desires != null)
            {
                foreach (KLEPProjectedSatisfactionDesireTrace desire in desires)
                {
                    copy.Add(desire ?? throw new ArgumentException(
                        "Projected-satisfaction traces cannot contain null.",
                        nameof(desires)));
                }
            }

            this.desires = new ReadOnlyCollection<
                KLEPProjectedSatisfactionDesireTrace>(copy);
            RawTotal = rawTotal;
            ScoreContribution = scoreContribution;
            Projection = projection;
        }

        public string PolicyStableId { get; }
        public string PolicyVersion { get; }
        public string TargetExecutableId { get; }
        public IReadOnlyList<KLEPProjectedSatisfactionDesireTrace> Desires =>
            desires;
        public double RawTotal { get; }
        public float ScoreContribution { get; }
        public KLEPCandidateStateProjection Projection { get; }
        public bool ProjectionAbstained =>
            Projection != null && !Projection.IsComplete;
        public string ProjectorStableId =>
            Projection?.ProjectorStableId ?? "klep.projection.direct-state";
        public string ProjectorVersion => Projection?.ProjectorVersion ?? "1";
        public string ProjectionProvenance => Projection?.Provenance ??
            "Direct complete-state policy evaluation.";
        public string ProjectionAbstentionReason =>
            Projection?.AbstentionReason ?? string.Empty;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable identity is required.", parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Default deterministic policy for comparing the current complete Key
    /// state with one complete Observer-projected Key state. This class does
    /// not determine eligibility or select an Executable.
    /// </summary>
    public sealed class KLEPProjectedSatisfactionPolicy
    {
        private const string DefaultStableId =
            "klep.agent.projected-satisfaction.default";
        private const string DefaultVersion = "1";

        private readonly ReadOnlyCollection<KLEPAgentDesire> desires;

        public KLEPProjectedSatisfactionPolicy(
            IEnumerable<KLEPAgentDesire> desires)
        {
            var copy = new List<KLEPAgentDesire>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            if (desires != null)
            {
                foreach (KLEPAgentDesire desire in desires)
                {
                    if (desire == null)
                    {
                        throw new ArgumentException(
                            "Agent desires cannot contain null.",
                            nameof(desires));
                    }

                    if (!stableIds.Add(desire.StableId))
                    {
                        throw new ArgumentException(
                            $"Agent desire ID '{desire.StableId}' occurs more than once.",
                            nameof(desires));
                    }

                    copy.Add(desire);
                }
            }

            this.desires = new ReadOnlyCollection<KLEPAgentDesire>(copy);
        }

        public string StableId => DefaultStableId;
        public string Version => DefaultVersion;
        public IReadOnlyList<KLEPAgentDesire> Desires => desires;

        public KLEPProjectedSatisfactionEvaluation Evaluate(
            string targetExecutableId,
            IKLEPLockKeySource currentState,
            IKLEPLockKeySource completeProjectedState)
        {
            if (string.IsNullOrWhiteSpace(targetExecutableId))
            {
                throw new ArgumentException(
                    "A non-empty target Executable ID is required.",
                    nameof(targetExecutableId));
            }

            if (currentState == null)
            {
                throw new ArgumentNullException(nameof(currentState));
            }

            if (completeProjectedState == null)
            {
                throw new ArgumentNullException(nameof(completeProjectedState));
            }

            var traces = new List<KLEPProjectedSatisfactionDesireTrace>(
                desires.Count);
            double total = 0d;
            for (int index = 0; index < desires.Count; index++)
            {
                KLEPAgentDesire desire = desires[index];
                int currentTruth = EvaluateTruth(
                    desire.Expression, currentState);
                int projectedTruth = EvaluateTruth(
                    desire.Expression, completeProjectedState);
                int truthDelta = projectedTruth - currentTruth;
                double contribution =
                    (double)desire.Weight * desire.Pressure * truthDelta;
                if (double.IsNaN(contribution) ||
                    double.IsInfinity(contribution))
                {
                    throw new InvalidOperationException(
                        $"Agent desire '{desire.StableId}' produced a " +
                        "non-finite projected-satisfaction contribution.");
                }

                double nextTotal = total + contribution;
                if (double.IsNaN(nextTotal) || double.IsInfinity(nextTotal))
                {
                    throw new InvalidOperationException(
                        "Projected-satisfaction contributions exceeded the " +
                        "finite calculation range.");
                }

                traces.Add(new KLEPProjectedSatisfactionDesireTrace(
                    desire,
                    currentTruth,
                    projectedTruth,
                    contribution));
                total = nextTotal;
            }

            if (total > float.MaxValue || total < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    "Projected satisfaction exceeded the finite Executable " +
                    "score range.");
            }

            return new KLEPProjectedSatisfactionEvaluation(
                StableId,
                Version,
                targetExecutableId,
                traces,
                total,
                (float)total);
        }

        internal KLEPProjectedSatisfactionEvaluation Evaluate(
            KLEPCandidateStateProjectionRequest request,
            KLEPCandidateStateProjection projection)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            if (projection.IsComplete)
            {
                KLEPProjectedSatisfactionEvaluation complete = Evaluate(
                    request.TargetExecutableId,
                    request.CurrentSnapshot,
                    projection.CompleteState);
                return new KLEPProjectedSatisfactionEvaluation(
                    complete.PolicyStableId,
                    complete.PolicyVersion,
                    complete.TargetExecutableId,
                    complete.Desires,
                    complete.RawTotal,
                    complete.ScoreContribution,
                    projection);
            }

            var traces = new List<KLEPProjectedSatisfactionDesireTrace>(
                desires.Count);
            for (int index = 0; index < desires.Count; index++)
            {
                KLEPAgentDesire desire = desires[index];
                traces.Add(new KLEPProjectedSatisfactionDesireTrace(
                    desire,
                    EvaluateTruth(desire.Expression, request.CurrentSnapshot),
                    null,
                    0d));
            }

            return new KLEPProjectedSatisfactionEvaluation(
                StableId,
                Version,
                request.TargetExecutableId,
                traces,
                0d,
                0f,
                projection);
        }

        private static int EvaluateTruth(
            KLEPLockExpression expression,
            IKLEPLockKeySource state)
        {
            var results = new List<KLEPLockExpressionResult>();
            return expression.Evaluate(state, "root", results) ? 1 : 0;
        }
    }
}
