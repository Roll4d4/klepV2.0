using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Desire;

namespace Roll4d4.Klep.LearnedExpectations
{
    /// <summary>
    /// One authored Desire term for a root-candidate binding. The scale maps
    /// normalized Desire-effect units into the project's attraction units.
    /// </summary>
    public sealed class KLEPLearnedDesireBindingTerm
    {
        public KLEPLearnedDesireBindingTerm(
            string desireStableId,
            float selectionScale = 1f)
        {
            DesireStableId = RequireId(
                desireStableId, nameof(desireStableId));
            if (float.IsNaN(selectionScale) ||
                float.IsInfinity(selectionScale) ||
                selectionScale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionScale));
            }

            SelectionScale = selectionScale;
        }

        public string DesireStableId { get; }
        public float SelectionScale { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A learned Desire binding requires a non-empty ID.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Explicitly maps one scored root candidate to the factual action whose
    /// ActionOwned effects trained the critic. Goal descendants are never
    /// inferred through this type.
    /// </summary>
    public sealed class KLEPLearnedDesireCandidateBinding
    {
        private readonly ReadOnlyCollection<KLEPLearnedDesireBindingTerm>
            desireTerms;

        public KLEPLearnedDesireCandidateBinding(
            string candidateRootExecutableId,
            string effectSourceExecutableId,
            IEnumerable<KLEPLearnedDesireBindingTerm> desireTerms)
        {
            CandidateRootExecutableId = RequireId(
                candidateRootExecutableId,
                nameof(candidateRootExecutableId));
            EffectSourceExecutableId = RequireId(
                effectSourceExecutableId,
                nameof(effectSourceExecutableId));
            this.desireTerms = CopyTerms(desireTerms);
            if (this.desireTerms.Count == 0)
            {
                throw new ArgumentException(
                    "A learned Desire candidate binding requires at least " +
                    "one authored Desire term.",
                    nameof(desireTerms));
            }
        }

        public string CandidateRootExecutableId { get; }
        public string EffectSourceExecutableId { get; }
        public IReadOnlyList<KLEPLearnedDesireBindingTerm> DesireTerms =>
            desireTerms;

        private static ReadOnlyCollection<KLEPLearnedDesireBindingTerm>
            CopyTerms(IEnumerable<KLEPLearnedDesireBindingTerm> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPLearnedDesireBindingTerm>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (KLEPLearnedDesireBindingTerm term in source)
            {
                if (term == null)
                {
                    throw new ArgumentException(
                        "A learned Desire binding cannot contain a null term.",
                        nameof(source));
                }

                if (!ids.Add(term.DesireStableId))
                {
                    throw new ArgumentException(
                        $"Desire '{term.DesireStableId}' is bound more than once.",
                        nameof(source));
                }

                copy.Add(term);
            }

            return new ReadOnlyCollection<KLEPLearnedDesireBindingTerm>(copy);
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A learned Desire binding requires a non-empty ID.",
                    parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Risk-neutral first policy accepted by KLEP-AGENT-011. It freezes one
    /// current Desire observation and one critic snapshot for the entire
    /// already-eligible candidate batch, then returns evidence only. The Agent
    /// remains the sole authority that applies the signed result and selects.
    /// </summary>
    public sealed class KLEPLearnedDesireSelectionPolicy :
        IKLEPLearnedDesireSelectionPolicy
    {
        private readonly IKLEPDesireSnapshotView desireSnapshotView;
        private readonly IKLEPLearnedDesireExpectationsView expectationsView;
        private readonly ReadOnlyCollection<KLEPLearnedDesireCandidateBinding>
            bindings;
        private readonly Dictionary<string, KLEPLearnedDesireCandidateBinding>
            bindingByCandidate;

        public KLEPLearnedDesireSelectionPolicy(
            string stableId,
            string version,
            IKLEPDesireSnapshotView desireSnapshotView,
            IKLEPLearnedDesireExpectationsView expectationsView,
            IEnumerable<KLEPLearnedDesireCandidateBinding> bindings)
        {
            StableId = RequireId(stableId, nameof(stableId));
            Version = RequireId(version, nameof(version));
            this.desireSnapshotView = desireSnapshotView ??
                throw new ArgumentNullException(nameof(desireSnapshotView));
            this.expectationsView = expectationsView ??
                throw new ArgumentNullException(nameof(expectationsView));
            this.bindings = CopyBindings(bindings, out bindingByCandidate);
            BindingFingerprint = BuildBindingFingerprint(this.bindings);
        }

        public string StableId { get; }
        public string Version { get; }
        public string BindingFingerprint { get; }
        public IReadOnlyList<KLEPLearnedDesireCandidateBinding> Bindings =>
            bindings;

        public KLEPLearnedDesireSelectionBatch Evaluate(
            KLEPLearnedDesireSelectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateRequestIdentity(request);
            KLEPDesireSnapshot desireSnapshot =
                desireSnapshotView.CurrentSnapshot ??
                throw new InvalidOperationException(
                    $"Learned Desire policy '{StableId}' requires one current " +
                    "Desire snapshot.");
            KLEPLearnedDesireEffectSnapshot criticSnapshot =
                expectationsView.CaptureDesireEffectSnapshot() ??
                throw new InvalidOperationException(
                    $"Learned Desire policy '{StableId}' received no critic " +
                    "snapshot.");

            var evidenceFrame = new KLEPLearnedDesireSelectionEvidenceFrame(
                desireSnapshot.OwnerId,
                desireSnapshot.DefinitionFingerprint,
                desireSnapshot.SnapshotId,
                desireSnapshot.DesireTick,
                desireSnapshot.ObservedMomentId,
                desireSnapshot.ContextIdentity.ContextId,
                desireSnapshot.ContextIdentity.SchemaId,
                desireSnapshot.ContextIdentity.SchemaVersion,
                criticSnapshot.OwnerStableId,
                criticSnapshot.OwnerVersion,
                criticSnapshot.Revision,
                criticSnapshot.LastEvidenceSequence);

            var evaluations = new List<KLEPLearnedDesireCandidateEvaluation>(
                request.Candidates.Count);
            for (int index = 0; index < request.Candidates.Count; index++)
            {
                evaluations.Add(EvaluateCandidate(
                    request,
                    request.Candidates[index],
                    desireSnapshot,
                    criticSnapshot,
                    evidenceFrame));
            }

            // The first policy is single-threaded by contract. These guards
            // turn accidental mutation during a supposedly pure comparison
            // into a fault instead of mixing two evidence moments.
            if (!ReferenceEquals(
                    desireSnapshotView.CurrentSnapshot,
                    desireSnapshot))
            {
                throw new InvalidOperationException(
                    $"Learned Desire policy '{StableId}' observed the Desire " +
                    "snapshot change during one selection batch.");
            }

            if (expectationsView.DesireEffectRevision !=
                    criticSnapshot.Revision ||
                expectationsView.LastDesireEffectEvidenceSequence !=
                    criticSnapshot.LastEvidenceSequence)
            {
                throw new InvalidOperationException(
                    $"Learned Desire policy '{StableId}' observed the critic " +
                    "change during one selection batch.");
            }

            return new KLEPLearnedDesireSelectionBatch(
                StableId,
                Version,
                BindingFingerprint,
                request.CatalogRevision,
                request.CatalogFingerprint,
                request.CurrentEvidenceFingerprint,
                evidenceFrame,
                evaluations);
        }

        private KLEPLearnedDesireCandidateEvaluation EvaluateCandidate(
            KLEPLearnedDesireSelectionRequest request,
            KLEPLearnedDesireSelectionCandidate candidate,
            KLEPDesireSnapshot desireSnapshot,
            KLEPLearnedDesireEffectSnapshot criticSnapshot,
            KLEPLearnedDesireSelectionEvidenceFrame evidenceFrame)
        {
            if (!bindingByCandidate.TryGetValue(
                    candidate.ExecutableStableId,
                    out KLEPLearnedDesireCandidateBinding binding))
            {
                return new KLEPLearnedDesireCandidateEvaluation(
                    StableId,
                    Version,
                    BindingFingerprint,
                    evidenceFrame,
                    candidate.ExecutableStableId,
                    candidate.RootTenureId,
                    string.Empty,
                    KLEPLearnedDesireCandidateDisposition.Unbound,
                    candidate.PrePolicyScore,
                    candidate.IsCurrentRunning,
                    0f,
                    Array.Empty<KLEPLearnedDesireContributionTrace>(),
                    "No authored root-to-effect-action binding; learned " +
                    "Desire influence abstained.");
            }

            ValidateBindingAgainstMap(request, candidate, binding);
            var traces = new List<KLEPLearnedDesireContributionTrace>(
                binding.DesireTerms.Count);
            double total = 0d;
            for (int index = 0; index < binding.DesireTerms.Count; index++)
            {
                KLEPLearnedDesireBindingTerm term = binding.DesireTerms[index];
                KLEPDesireStateSnapshot desire = FindDesire(
                    desireSnapshot, term.DesireStableId);
                KLEPLearnedDesireEffectBucketIdentity bucket =
                    KLEPLearnedDesireEffectBucketIdentity.ForPrediction(
                        desireSnapshot,
                        desire,
                        binding.EffectSourceExecutableId);
                // Dormant or authored-zero terms abstain from selection without
                // querying the critic table. The bucket identity is still
                // retained so the zero trace remains fully inspectable.
                KLEPLearnedDesireEffectEstimate estimate =
                    term.SelectionScale == 0f ||
                    desire.Weight == 0f ||
                    desire.Pressure == 0f
                        ? null
                        : FindEstimate(criticSnapshot, bucket);
                KLEPLearnedDesireContributionTrace trace = EvaluateTerm(
                    criticSnapshot,
                    binding,
                    term,
                    desire,
                    bucket,
                    estimate);
                total = CheckedFinite(
                    total + trace.ScoreContribution,
                    "Learned Desire candidate sum");
                traces.Add(trace);
            }

            float contribution = CheckedFloat(
                total, "Learned Desire candidate contribution");
            return new KLEPLearnedDesireCandidateEvaluation(
                StableId,
                Version,
                BindingFingerprint,
                evidenceFrame,
                candidate.ExecutableStableId,
                candidate.RootTenureId,
                binding.EffectSourceExecutableId,
                KLEPLearnedDesireCandidateDisposition.Applied,
                candidate.PrePolicyScore,
                candidate.IsCurrentRunning,
                contribution,
                traces,
                $"Summed {traces.Count} authored learned-Desire term(s) " +
                "from one frozen Desire/critic frame.");
        }

        private static KLEPLearnedDesireContributionTrace EvaluateTerm(
            KLEPLearnedDesireEffectSnapshot criticSnapshot,
            KLEPLearnedDesireCandidateBinding binding,
            KLEPLearnedDesireBindingTerm term,
            KLEPDesireStateSnapshot desire,
            KLEPLearnedDesireEffectBucketIdentity bucket,
            KLEPLearnedDesireEffectEstimate estimate)
        {
            long support = estimate == null ? 0 : estimate.Support;
            float mean = estimate == null
                ? 0f
                : CheckedFloat(estimate.MeanEffect, "Learned mean effect");
            float variance = estimate == null
                ? 0f
                : CheckedFloat(
                    estimate.SampleVariance, "Learned sample variance");
            float predictionError = estimate == null
                ? 0f
                : CheckedFloat(
                    estimate.LastPredictionError,
                    "Learned prediction error");
            float confidence = estimate == null
                ? 0f
                : CheckedFloat(estimate.Confidence, "Learned confidence");
            float contribution = 0f;
            KLEPLearnedDesireContributionDisposition disposition;
            string explanation;

            if (term.SelectionScale == 0f)
            {
                disposition =
                    KLEPLearnedDesireContributionDisposition.ZeroScale;
                explanation = "Authored selection scale is zero.";
            }
            else if (desire.Weight == 0f)
            {
                disposition =
                    KLEPLearnedDesireContributionDisposition.ZeroWeight;
                explanation = "Current authored Desire weight is zero.";
            }
            else if (desire.Pressure == 0f)
            {
                disposition =
                    KLEPLearnedDesireContributionDisposition.ZeroPressure;
                explanation = "Current Desire pressure is dormant.";
            }
            else if (estimate == null || !estimate.IsKnown || support == 0)
            {
                disposition =
                    KLEPLearnedDesireContributionDisposition.UnknownEvidence;
                explanation = "The exact action/Desire/context bucket has no " +
                    "completed evidence.";
            }
            else if (confidence == 0f)
            {
                disposition =
                    KLEPLearnedDesireContributionDisposition.ZeroConfidence;
                explanation = "The exact estimate has zero evidence confidence.";
            }
            else
            {
                double calculated = CheckedFinite(
                    (double)term.SelectionScale * desire.Weight *
                    desire.Pressure * confidence * mean,
                    "Learned Desire term");
                contribution = CheckedFloat(
                    calculated, "Learned Desire term");
                disposition =
                    KLEPLearnedDesireContributionDisposition.Applied;
                explanation =
                    "scale * current Weight * current Pressure * confidence " +
                    "* learned mean raw effect.";
            }

            return new KLEPLearnedDesireContributionTrace(
                desire.DesireStableId,
                desire.DesireVersion,
                desire.EvaluatorId,
                desire.EvaluatorVersion,
                binding.EffectSourceExecutableId,
                BuildBucketFingerprint(bucket),
                criticSnapshot.Revision,
                support,
                mean,
                variance,
                predictionError,
                confidence,
                desire.Weight,
                desire.Pressure,
                term.SelectionScale,
                contribution,
                disposition,
                explanation);
        }

        private static void ValidateBindingAgainstMap(
            KLEPLearnedDesireSelectionRequest request,
            KLEPLearnedDesireSelectionCandidate candidate,
            KLEPLearnedDesireCandidateBinding binding)
        {
            if (!request.AcceptedStructuralMap.TryGetExecutable(
                    candidate.ExecutableStableId,
                    out KLEPExecutableStructuralNode root) ||
                !root.IsRoot ||
                !StringComparer.Ordinal.Equals(
                    root.RootTenureId, candidate.RootTenureId))
            {
                throw new InvalidOperationException(
                    $"Learned Desire candidate '{candidate.ExecutableStableId}' " +
                    "does not match its accepted root/tenure map evidence.");
            }

            if (!request.AcceptedStructuralMap.TryGetExecutable(
                    binding.EffectSourceExecutableId,
                    out KLEPExecutableStructuralNode effectSource) ||
                !StringComparer.Ordinal.Equals(
                    effectSource.RootTenureId, root.RootTenureId))
            {
                throw new InvalidOperationException(
                    $"Learned Desire binding '{binding.CandidateRootExecutableId}' " +
                    $"-> '{binding.EffectSourceExecutableId}' does not name " +
                    "that root or one of its accepted descendants.");
            }
        }

        private static KLEPDesireStateSnapshot FindDesire(
            KLEPDesireSnapshot snapshot,
            string desireStableId)
        {
            for (int index = 0; index < snapshot.Desires.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(
                        snapshot.Desires[index].DesireStableId,
                        desireStableId))
                {
                    return snapshot.Desires[index];
                }
            }

            throw new InvalidOperationException(
                $"Authored learned-Desire binding names missing Desire " +
                $"'{desireStableId}' in snapshot '{snapshot.SnapshotId}'.");
        }

        private static KLEPLearnedDesireEffectEstimate FindEstimate(
            KLEPLearnedDesireEffectSnapshot snapshot,
            KLEPLearnedDesireEffectBucketIdentity bucket)
        {
            for (int index = 0; index < snapshot.Estimates.Count; index++)
            {
                if (snapshot.Estimates[index].Bucket.Equals(bucket))
                {
                    return snapshot.Estimates[index];
                }
            }

            return null;
        }

        private void ValidateRequestIdentity(
            KLEPLearnedDesireSelectionRequest request)
        {
            if (!StringComparer.Ordinal.Equals(
                    StableId, request.PolicyStableId) ||
                !StringComparer.Ordinal.Equals(
                    Version, request.PolicyVersion) ||
                !StringComparer.Ordinal.Equals(
                    BindingFingerprint, request.BindingFingerprint))
            {
                throw new InvalidOperationException(
                    "Learned Desire request identity does not match the " +
                    "configured policy and binding table.");
            }
        }

        private static ReadOnlyCollection<KLEPLearnedDesireCandidateBinding>
            CopyBindings(
                IEnumerable<KLEPLearnedDesireCandidateBinding> source,
                out Dictionary<string, KLEPLearnedDesireCandidateBinding>
                    byCandidate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPLearnedDesireCandidateBinding>();
            byCandidate = new Dictionary<
                string,
                KLEPLearnedDesireCandidateBinding>(StringComparer.Ordinal);
            foreach (KLEPLearnedDesireCandidateBinding binding in source)
            {
                if (binding == null)
                {
                    throw new ArgumentException(
                        "A learned Desire policy cannot contain a null binding.",
                        nameof(source));
                }

                if (byCandidate.ContainsKey(binding.CandidateRootExecutableId))
                {
                    throw new ArgumentException(
                        $"Candidate '{binding.CandidateRootExecutableId}' has " +
                        "more than one learned Desire binding.",
                        nameof(source));
                }

                byCandidate.Add(binding.CandidateRootExecutableId, binding);
                copy.Add(binding);
            }

            return new ReadOnlyCollection<KLEPLearnedDesireCandidateBinding>(
                copy);
        }

        private static string BuildBindingFingerprint(
            IReadOnlyList<KLEPLearnedDesireCandidateBinding> source)
        {
            ulong hash = 14695981039346656037UL;
            AddFingerprintText(ref hash, "KLEP-LDS-1");
            for (int index = 0; index < source.Count; index++)
            {
                KLEPLearnedDesireCandidateBinding binding = source[index];
                AddFingerprintText(
                    ref hash, binding.CandidateRootExecutableId);
                AddFingerprintText(
                    ref hash, binding.EffectSourceExecutableId);
                AddFingerprintText(
                    ref hash,
                    binding.DesireTerms.Count.ToString(
                        CultureInfo.InvariantCulture));
                for (int termIndex = 0;
                     termIndex < binding.DesireTerms.Count;
                     termIndex++)
                {
                    KLEPLearnedDesireBindingTerm term =
                        binding.DesireTerms[termIndex];
                    AddFingerprintText(ref hash, term.DesireStableId);
                    AddFingerprintText(
                        ref hash,
                        term.SelectionScale.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            return "learned-desire-bindings-" +
                hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static string BuildBucketFingerprint(
            KLEPLearnedDesireEffectBucketIdentity bucket)
        {
            ulong hash = 14695981039346656037UL;
            AddFingerprintText(ref hash, "KLEP-LDE-BUCKET-1");
            AddFingerprintText(ref hash, bucket.DesireOwnerId);
            AddFingerprintText(ref hash, bucket.DefinitionFingerprint);
            AddFingerprintText(ref hash, bucket.ActionStableId);
            AddFingerprintText(ref hash, bucket.DesireStableId);
            AddFingerprintText(ref hash, bucket.DesireVersion);
            AddFingerprintText(ref hash, bucket.EvaluatorId);
            AddFingerprintText(ref hash, bucket.EvaluatorVersion);
            AddFingerprintText(ref hash, bucket.ContextId);
            AddFingerprintText(ref hash, bucket.ContextSchemaId);
            AddFingerprintText(ref hash, bucket.ContextSchemaVersion);
            return "learned-desire-bucket-" +
                hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        private static void AddFingerprintText(ref ulong hash, string value)
        {
            string safe = value ?? string.Empty;
            for (int index = 0; index < safe.Length; index++)
            {
                hash ^= safe[index];
                hash *= 1099511628211UL;
            }

            hash ^= 0xFF;
            hash *= 1099511628211UL;
        }

        private static double CheckedFinite(double value, string meaning)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException(
                    meaning + " exceeded the finite numeric domain.");
            }

            return value;
        }

        private static float CheckedFloat(double value, string meaning)
        {
            CheckedFinite(value, meaning);
            if (value > float.MaxValue || value < -float.MaxValue)
            {
                throw new InvalidOperationException(
                    meaning + " exceeded the finite score range.");
            }

            return (float)value;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A learned Desire selection policy requires a non-empty ID.",
                    parameterName);
            }

            return value;
        }
    }
}
