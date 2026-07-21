using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Roll4d4.Klep.Core
{
    /// <summary>
    /// Why one learned Desire term did or did not contribute to attraction.
    /// The policy owns the meaning of the numbers; Core preserves and validates
    /// the evidence needed to inspect the resulting score.
    /// </summary>
    public enum KLEPLearnedDesireContributionDisposition
    {
        Applied,
        UnknownEvidence,
        Unbound,
        ZeroWeight,
        ZeroPressure,
        ZeroConfidence,
        ZeroScale
    }

    public enum KLEPLearnedDesireCandidateDisposition
    {
        Applied,
        Unbound,
        NoDesireTerms
    }

    /// <summary>
    /// The one frozen Desire/critic frame used for every candidate in a batch.
    /// It remains attached to each score evaluation after the batch is gone.
    /// </summary>
    public sealed class KLEPLearnedDesireSelectionEvidenceFrame :
        IEquatable<KLEPLearnedDesireSelectionEvidenceFrame>
    {
        public KLEPLearnedDesireSelectionEvidenceFrame(
            string desireOwnerId,
            string desireDefinitionFingerprint,
            string desireSnapshotId,
            long desireTick,
            string observedMomentId,
            string desireContextId,
            string desireContextSchemaId,
            string desireContextSchemaVersion,
            string criticOwnerStableId,
            string criticOwnerVersion,
            long criticRevision,
            long criticLastEvidenceSequence)
        {
            DesireOwnerId = RequireId(
                desireOwnerId, nameof(desireOwnerId));
            DesireDefinitionFingerprint = RequireId(
                desireDefinitionFingerprint,
                nameof(desireDefinitionFingerprint));
            DesireSnapshotId = RequireId(
                desireSnapshotId, nameof(desireSnapshotId));
            ObservedMomentId = RequireId(
                observedMomentId, nameof(observedMomentId));
            DesireContextId = RequireId(
                desireContextId, nameof(desireContextId));
            DesireContextSchemaId = RequireId(
                desireContextSchemaId, nameof(desireContextSchemaId));
            DesireContextSchemaVersion = RequireId(
                desireContextSchemaVersion,
                nameof(desireContextSchemaVersion));
            CriticOwnerStableId = RequireId(
                criticOwnerStableId, nameof(criticOwnerStableId));
            CriticOwnerVersion = RequireId(
                criticOwnerVersion, nameof(criticOwnerVersion));
            if (desireTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(desireTick));
            }

            if (criticRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(criticRevision));
            }

            if (criticLastEvidenceSequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticLastEvidenceSequence));
            }

            DesireTick = desireTick;
            CriticRevision = criticRevision;
            CriticLastEvidenceSequence = criticLastEvidenceSequence;
        }

        public string DesireOwnerId { get; }
        public string DesireDefinitionFingerprint { get; }
        public string DesireSnapshotId { get; }
        public long DesireTick { get; }
        public string ObservedMomentId { get; }
        public string DesireContextId { get; }
        public string DesireContextSchemaId { get; }
        public string DesireContextSchemaVersion { get; }
        public string CriticOwnerStableId { get; }
        public string CriticOwnerVersion { get; }
        public long CriticRevision { get; }
        public long CriticLastEvidenceSequence { get; }

        public bool Equals(KLEPLearnedDesireSelectionEvidenceFrame other)
        {
            return other != null &&
                StringComparer.Ordinal.Equals(
                    DesireOwnerId, other.DesireOwnerId) &&
                StringComparer.Ordinal.Equals(
                    DesireDefinitionFingerprint,
                    other.DesireDefinitionFingerprint) &&
                StringComparer.Ordinal.Equals(
                    DesireSnapshotId, other.DesireSnapshotId) &&
                DesireTick == other.DesireTick &&
                StringComparer.Ordinal.Equals(
                    ObservedMomentId, other.ObservedMomentId) &&
                StringComparer.Ordinal.Equals(
                    DesireContextId, other.DesireContextId) &&
                StringComparer.Ordinal.Equals(
                    DesireContextSchemaId, other.DesireContextSchemaId) &&
                StringComparer.Ordinal.Equals(
                    DesireContextSchemaVersion,
                    other.DesireContextSchemaVersion) &&
                StringComparer.Ordinal.Equals(
                    CriticOwnerStableId, other.CriticOwnerStableId) &&
                StringComparer.Ordinal.Equals(
                    CriticOwnerVersion, other.CriticOwnerVersion) &&
                CriticRevision == other.CriticRevision &&
                CriticLastEvidenceSequence == other.CriticLastEvidenceSequence;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as KLEPLearnedDesireSelectionEvidenceFrame);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = AddHash(hash, DesireOwnerId);
                hash = AddHash(hash, DesireDefinitionFingerprint);
                hash = AddHash(hash, DesireSnapshotId);
                hash = hash * 31 + DesireTick.GetHashCode();
                hash = AddHash(hash, ObservedMomentId);
                hash = AddHash(hash, DesireContextId);
                hash = AddHash(hash, DesireContextSchemaId);
                hash = AddHash(hash, DesireContextSchemaVersion);
                hash = AddHash(hash, CriticOwnerStableId);
                hash = AddHash(hash, CriticOwnerVersion);
                hash = hash * 31 + CriticRevision.GetHashCode();
                return hash * 31 + CriticLastEvidenceSequence.GetHashCode();
            }
        }

        private static int AddHash(int current, string value)
        {
            return current * 31 + StringComparer.Ordinal.GetHashCode(value);
        }

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
    /// One immutable, policy-authored Desire term in a candidate evaluation.
    /// It intentionally contains only Core-safe identities and scalar evidence.
    /// </summary>
    public sealed class KLEPLearnedDesireContributionTrace
    {
        public KLEPLearnedDesireContributionTrace(
            string desireStableId,
            string desireVersion,
            string evaluatorStableId,
            string evaluatorVersion,
            string effectSourceExecutableId,
            string learnedEvidenceFingerprint,
            long learnedRevision,
            long support,
            float meanEffect,
            float sampleVariance,
            float predictionError,
            float confidence,
            float currentWeight,
            float currentPressure,
            float selectionScale,
            float scoreContribution,
            KLEPLearnedDesireContributionDisposition disposition,
            string explanation = "")
        {
            DesireStableId = RequireId(desireStableId, nameof(desireStableId));
            DesireVersion = RequireId(desireVersion, nameof(desireVersion));
            EvaluatorStableId = RequireId(
                evaluatorStableId, nameof(evaluatorStableId));
            EvaluatorVersion = RequireId(
                evaluatorVersion, nameof(evaluatorVersion));
            EffectSourceExecutableId = RequireId(
                effectSourceExecutableId, nameof(effectSourceExecutableId));
            LearnedEvidenceFingerprint = RequireId(
                learnedEvidenceFingerprint,
                nameof(learnedEvidenceFingerprint));
            if (learnedRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(learnedRevision));
            }

            if (support < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(support));
            }

            RequireFinite(meanEffect, nameof(meanEffect));
            RequireFinite(sampleVariance, nameof(sampleVariance));
            RequireFinite(predictionError, nameof(predictionError));
            RequireFinite(confidence, nameof(confidence));
            RequireFinite(currentWeight, nameof(currentWeight));
            RequireFinite(currentPressure, nameof(currentPressure));
            RequireFinite(selectionScale, nameof(selectionScale));
            RequireFinite(scoreContribution, nameof(scoreContribution));
            if (sampleVariance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleVariance));
            }

            if (confidence < 0f || confidence > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence));
            }

            if (currentWeight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(currentWeight));
            }

            if (currentPressure < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(currentPressure));
            }

            if (selectionScale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionScale));
            }

            if (!Enum.IsDefined(
                    typeof(KLEPLearnedDesireContributionDisposition),
                    disposition))
            {
                throw new ArgumentOutOfRangeException(nameof(disposition));
            }

            LearnedRevision = learnedRevision;
            Support = support;
            MeanEffect = meanEffect;
            SampleVariance = sampleVariance;
            PredictionError = predictionError;
            Confidence = confidence;
            CurrentWeight = currentWeight;
            CurrentPressure = currentPressure;
            SelectionScale = selectionScale;
            ScoreContribution = scoreContribution;
            Disposition = disposition;
            Explanation = explanation ?? string.Empty;
        }

        public string DesireStableId { get; }
        public string DesireVersion { get; }
        public string EvaluatorStableId { get; }
        public string EvaluatorVersion { get; }
        public string EffectSourceExecutableId { get; }
        public string LearnedEvidenceFingerprint { get; }
        public long LearnedRevision { get; }
        public long Support { get; }
        public float MeanEffect { get; }
        public float SampleVariance { get; }
        public float PredictionError { get; }
        public float Confidence { get; }
        public float CurrentWeight { get; }
        public float CurrentPressure { get; }
        public float SelectionScale { get; }
        public float ScoreContribution { get; }
        public KLEPLearnedDesireContributionDisposition Disposition { get; }
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

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// One already-eligible root Solo presented to the optional learned Desire
    /// policy. Ineligible roots and Tandems never receive this representation.
    /// </summary>
    public sealed class KLEPLearnedDesireSelectionCandidate
    {
        internal KLEPLearnedDesireSelectionCandidate(
            string executableStableId,
            string rootTenureId,
            float prePolicyScore,
            bool isCurrentRunning)
        {
            ExecutableStableId = RequireId(
                executableStableId, nameof(executableStableId));
            RootTenureId = RequireId(rootTenureId, nameof(rootTenureId));
            if (float.IsNaN(prePolicyScore) || float.IsInfinity(prePolicyScore))
            {
                throw new ArgumentOutOfRangeException(nameof(prePolicyScore));
            }

            PrePolicyScore = prePolicyScore;
            IsCurrentRunning = isCurrentRunning;
        }

        public string ExecutableStableId { get; }
        public string RootTenureId { get; }
        public float PrePolicyScore { get; }
        public bool IsCurrentRunning { get; }

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
    /// One immutable batch request over the accepted structural map and the
    /// settled post-Tandem Key evidence for this Agent cycle.
    /// </summary>
    public sealed class KLEPLearnedDesireSelectionRequest
    {
        private readonly ReadOnlyCollection<KLEPLearnedDesireSelectionCandidate>
            candidates;

        internal KLEPLearnedDesireSelectionRequest(
            string policyStableId,
            string policyVersion,
            string bindingFingerprint,
            KLEPExecutableStructuralMap acceptedStructuralMap,
            KLEPKeySnapshot currentSnapshot,
            IEnumerable<KLEPLearnedDesireSelectionCandidate> candidates)
        {
            PolicyStableId = RequireId(
                policyStableId, nameof(policyStableId));
            PolicyVersion = RequireId(policyVersion, nameof(policyVersion));
            BindingFingerprint = RequireId(
                bindingFingerprint, nameof(bindingFingerprint));
            AcceptedStructuralMap = acceptedStructuralMap ??
                throw new ArgumentNullException(nameof(acceptedStructuralMap));
            if (!acceptedStructuralMap.IsValid)
            {
                throw new ArgumentException(
                    "Learned Desire selection requires a valid accepted map.",
                    nameof(acceptedStructuralMap));
            }

            CurrentSnapshot = currentSnapshot ??
                throw new ArgumentNullException(nameof(currentSnapshot));
            CurrentEvidenceFingerprint =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(currentSnapshot);
            this.candidates = CopyCandidates(candidates);
            if (this.candidates.Count == 0)
            {
                throw new ArgumentException(
                    "A learned Desire request requires an eligible root Solo.",
                    nameof(candidates));
            }
        }

        public string PolicyStableId { get; }
        public string PolicyVersion { get; }
        public string BindingFingerprint { get; }
        public KLEPExecutableStructuralMap AcceptedStructuralMap { get; }
        public string CatalogRevision =>
            AcceptedStructuralMap.Snapshot.ProposedCatalogRevision;
        public KLEPStructuralMapFingerprint CatalogFingerprint =>
            AcceptedStructuralMap.Fingerprint;
        public KLEPKeySnapshot CurrentSnapshot { get; }
        public KLEPGuidanceEvidenceFingerprint CurrentEvidenceFingerprint { get; }
        public IReadOnlyList<KLEPLearnedDesireSelectionCandidate> Candidates =>
            candidates;

        private static ReadOnlyCollection<KLEPLearnedDesireSelectionCandidate>
            CopyCandidates(
                IEnumerable<KLEPLearnedDesireSelectionCandidate> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPLearnedDesireSelectionCandidate>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (KLEPLearnedDesireSelectionCandidate candidate in source)
            {
                if (candidate == null)
                {
                    throw new ArgumentException(
                        "Learned Desire candidates cannot contain null.",
                        nameof(source));
                }

                if (!ids.Add(candidate.ExecutableStableId))
                {
                    throw new ArgumentException(
                        $"Candidate '{candidate.ExecutableStableId}' occurs more than once.",
                        nameof(source));
                }

                copy.Add(candidate);
            }

            return new ReadOnlyCollection<KLEPLearnedDesireSelectionCandidate>(
                copy);
        }

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
    /// The typed score evidence for exactly one requested candidate.
    /// </summary>
    public sealed class KLEPLearnedDesireCandidateEvaluation
    {
        private readonly ReadOnlyCollection<KLEPLearnedDesireContributionTrace>
            desires;

        public KLEPLearnedDesireCandidateEvaluation(
            string policyStableId,
            string policyVersion,
            string bindingFingerprint,
            KLEPLearnedDesireSelectionEvidenceFrame evidenceFrame,
            string targetExecutableId,
            string targetRootTenureId,
            string effectSourceExecutableId,
            KLEPLearnedDesireCandidateDisposition disposition,
            float prePolicyScore,
            bool wasCurrentRunning,
            float scoreContribution,
            IEnumerable<KLEPLearnedDesireContributionTrace> desires,
            string explanation = "")
        {
            PolicyStableId = RequireId(
                policyStableId, nameof(policyStableId));
            PolicyVersion = RequireId(policyVersion, nameof(policyVersion));
            BindingFingerprint = RequireId(
                bindingFingerprint, nameof(bindingFingerprint));
            EvidenceFrame = evidenceFrame ??
                throw new ArgumentNullException(nameof(evidenceFrame));
            TargetExecutableId = RequireId(
                targetExecutableId, nameof(targetExecutableId));
            TargetRootTenureId = RequireId(
                targetRootTenureId, nameof(targetRootTenureId));
            if (!Enum.IsDefined(
                    typeof(KLEPLearnedDesireCandidateDisposition),
                    disposition))
            {
                throw new ArgumentOutOfRangeException(nameof(disposition));
            }

            EffectSourceExecutableId = effectSourceExecutableId ?? string.Empty;
            if (disposition == KLEPLearnedDesireCandidateDisposition.Unbound)
            {
                if (EffectSourceExecutableId.Length != 0)
                {
                    throw new ArgumentException(
                        "An unbound candidate cannot claim an effect source.",
                        nameof(effectSourceExecutableId));
                }
            }
            else
            {
                EffectSourceExecutableId = RequireId(
                    EffectSourceExecutableId,
                    nameof(effectSourceExecutableId));
            }

            RequireFinite(prePolicyScore, nameof(prePolicyScore));
            RequireFinite(scoreContribution, nameof(scoreContribution));
            this.desires = CopyDesires(desires);
            if (disposition == KLEPLearnedDesireCandidateDisposition.Applied &&
                this.desires.Count == 0)
            {
                throw new ArgumentException(
                    "An applied candidate requires at least one Desire term.",
                    nameof(desires));
            }

            if (disposition != KLEPLearnedDesireCandidateDisposition.Applied &&
                (this.desires.Count != 0 || scoreContribution != 0f))
            {
                throw new ArgumentException(
                    "An abstaining candidate must have no Desire terms and zero contribution.",
                    nameof(desires));
            }

            for (int index = 0; index < this.desires.Count; index++)
            {
                if (!StringComparer.Ordinal.Equals(
                        EffectSourceExecutableId,
                        this.desires[index].EffectSourceExecutableId))
                {
                    throw new ArgumentException(
                        "Every Desire term must use the candidate's bound effect source.",
                        nameof(desires));
                }
            }

            PrePolicyScore = prePolicyScore;
            WasCurrentRunning = wasCurrentRunning;
            ScoreContribution = scoreContribution;
            Disposition = disposition;
            Explanation = explanation ?? string.Empty;
            CatalogRevision = string.Empty;
        }

        private KLEPLearnedDesireCandidateEvaluation(
            KLEPLearnedDesireCandidateEvaluation source,
            KLEPLearnedDesireSelectionRequest request)
            : this(
                source.PolicyStableId,
                source.PolicyVersion,
                source.BindingFingerprint,
                source.EvidenceFrame,
                source.TargetExecutableId,
                source.TargetRootTenureId,
                source.EffectSourceExecutableId,
                source.Disposition,
                source.PrePolicyScore,
                source.WasCurrentRunning,
                source.ScoreContribution,
                source.Desires,
                source.Explanation)
        {
            AgentCycleIndex = request.CurrentSnapshot.Tick;
            AgentWaveIndex = request.CurrentSnapshot.WaveIndex;
            CatalogRevision = request.CatalogRevision;
            CatalogFingerprint = request.CatalogFingerprint;
            CurrentEvidenceFingerprint = request.CurrentEvidenceFingerprint;
        }

        public string PolicyStableId { get; }
        public string PolicyVersion { get; }
        public string BindingFingerprint { get; }
        public KLEPLearnedDesireSelectionEvidenceFrame EvidenceFrame { get; }
        public string TargetExecutableId { get; }
        public string TargetRootTenureId { get; }
        public string EffectSourceExecutableId { get; }
        public KLEPLearnedDesireCandidateDisposition Disposition { get; }
        public float PrePolicyScore { get; }
        public bool WasCurrentRunning { get; }
        public float ScoreContribution { get; }
        public IReadOnlyList<KLEPLearnedDesireContributionTrace> Desires => desires;
        public string Explanation { get; }
        public long AgentCycleIndex { get; }
        public int AgentWaveIndex { get; }
        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public KLEPGuidanceEvidenceFingerprint CurrentEvidenceFingerprint { get; }
        public bool IsAgentEvidenceBound => CatalogFingerprint != null;

        internal KLEPLearnedDesireCandidateEvaluation BindAgentEvidence(
            KLEPLearnedDesireSelectionRequest request)
        {
            return new KLEPLearnedDesireCandidateEvaluation(
                this,
                request ?? throw new ArgumentNullException(nameof(request)));
        }

        private static ReadOnlyCollection<KLEPLearnedDesireContributionTrace>
            CopyDesires(IEnumerable<KLEPLearnedDesireContributionTrace> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPLearnedDesireContributionTrace>();
            var desireIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KLEPLearnedDesireContributionTrace desire in source)
            {
                if (desire == null)
                {
                    throw new ArgumentException(
                        "Learned Desire traces cannot contain null.",
                        nameof(source));
                }

                if (!desireIds.Add(desire.DesireStableId))
                {
                    throw new ArgumentException(
                        $"Desire '{desire.DesireStableId}' occurs more than once.",
                        nameof(source));
                }

                copy.Add(desire);
            }

            return new ReadOnlyCollection<KLEPLearnedDesireContributionTrace>(
                copy);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable identity is required.", parameterName);
            }

            return value;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    /// <summary>
    /// One complete response. Core accepts it only when it is an exact ordered
    /// result set for the request and the policy/binding identities stayed put.
    /// </summary>
    public sealed class KLEPLearnedDesireSelectionBatch
    {
        private readonly ReadOnlyCollection<KLEPLearnedDesireCandidateEvaluation>
            candidates;

        public KLEPLearnedDesireSelectionBatch(
            string policyStableId,
            string policyVersion,
            string bindingFingerprint,
            string catalogRevision,
            KLEPStructuralMapFingerprint catalogFingerprint,
            KLEPGuidanceEvidenceFingerprint currentEvidenceFingerprint,
            KLEPLearnedDesireSelectionEvidenceFrame evidenceFrame,
            IEnumerable<KLEPLearnedDesireCandidateEvaluation> candidates)
        {
            PolicyStableId = RequireId(
                policyStableId, nameof(policyStableId));
            PolicyVersion = RequireId(policyVersion, nameof(policyVersion));
            BindingFingerprint = RequireId(
                bindingFingerprint, nameof(bindingFingerprint));
            CatalogRevision = RequireId(
                catalogRevision, nameof(catalogRevision));
            CatalogFingerprint = catalogFingerprint ??
                throw new ArgumentNullException(nameof(catalogFingerprint));
            CurrentEvidenceFingerprint = currentEvidenceFingerprint ??
                throw new ArgumentNullException(
                    nameof(currentEvidenceFingerprint));
            EvidenceFrame = evidenceFrame ??
                throw new ArgumentNullException(nameof(evidenceFrame));
            this.candidates = CopyCandidates(candidates);
        }

        public string PolicyStableId { get; }
        public string PolicyVersion { get; }
        public string BindingFingerprint { get; }
        public string CatalogRevision { get; }
        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public KLEPGuidanceEvidenceFingerprint CurrentEvidenceFingerprint { get; }
        public KLEPLearnedDesireSelectionEvidenceFrame EvidenceFrame { get; }
        public IReadOnlyList<KLEPLearnedDesireCandidateEvaluation> Candidates =>
            candidates;

        private static ReadOnlyCollection<KLEPLearnedDesireCandidateEvaluation>
            CopyCandidates(
                IEnumerable<KLEPLearnedDesireCandidateEvaluation> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPLearnedDesireCandidateEvaluation>();
            foreach (KLEPLearnedDesireCandidateEvaluation candidate in source)
            {
                copy.Add(candidate ?? throw new ArgumentException(
                    "Learned Desire results cannot contain null.",
                    nameof(source)));
            }

            return new ReadOnlyCollection<KLEPLearnedDesireCandidateEvaluation>(
                copy);
        }

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
    /// Optional dependency-inversion seam. Implementations may read Desire and
    /// learned-expectation authorities, but they never own eligibility or live
    /// execution.
    /// </summary>
    public interface IKLEPLearnedDesireSelectionPolicy
    {
        string StableId { get; }
        string Version { get; }
        string BindingFingerprint { get; }

        KLEPLearnedDesireSelectionBatch Evaluate(
            KLEPLearnedDesireSelectionRequest request);
    }
}
