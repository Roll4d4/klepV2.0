using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Desire;
using Roll4d4.Klep.Observer;

namespace Roll4d4.Klep.LearnedExpectations
{
    /// <summary>
    /// Read-only critic evidence. It exposes learned raw Desire effects only;
    /// no authored Weight, current Pressure, utility, eligibility, or selection.
    /// </summary>
    public interface IKLEPLearnedDesireExpectationsView
    {
        long DesireEffectRevision { get; }
        long LastDesireEffectEvidenceSequence { get; }

        KLEPLearnedDesireEffectEstimate QueryDesireEffect(
            KLEPLearnedDesireEffectBucketIdentity bucket);

        KLEPLearnedDesireEffectSnapshot CaptureDesireEffectSnapshot();
    }

    /// <summary>
    /// Combined outward-only view for diagnostics that render both learned
    /// evidence domains. It deliberately inherits only query/snapshot
    /// interfaces and exposes no recording or reset authority.
    /// </summary>
    public interface IKLEPLearnedExpectationsDiagnosticView :
        IKLEPLearnedExpectationsView,
        IKLEPLearnedDesireExpectationsView
    {
    }

    /// <summary>
    /// Exact conditioning identity for one learned raw Desire effect.
    /// </summary>
    public sealed class KLEPLearnedDesireEffectBucketIdentity :
        IEquatable<KLEPLearnedDesireEffectBucketIdentity>,
        IComparable<KLEPLearnedDesireEffectBucketIdentity>
    {
        private KLEPLearnedDesireEffectBucketIdentity(
            string desireOwnerId,
            string definitionFingerprint,
            string actionStableId,
            string desireStableId,
            string desireVersion,
            string evaluatorId,
            string evaluatorVersion,
            string contextId,
            string contextSchemaId,
            string contextSchemaVersion)
        {
            DesireOwnerId = RequireId(desireOwnerId, nameof(desireOwnerId));
            DefinitionFingerprint = RequireId(
                definitionFingerprint, nameof(definitionFingerprint));
            ActionStableId = RequireId(actionStableId, nameof(actionStableId));
            DesireStableId = RequireId(desireStableId, nameof(desireStableId));
            DesireVersion = RequireId(desireVersion, nameof(desireVersion));
            EvaluatorId = RequireId(evaluatorId, nameof(evaluatorId));
            EvaluatorVersion = RequireId(
                evaluatorVersion, nameof(evaluatorVersion));
            ContextId = RequireId(contextId, nameof(contextId));
            ContextSchemaId = RequireId(
                contextSchemaId, nameof(contextSchemaId));
            ContextSchemaVersion = RequireId(
                contextSchemaVersion, nameof(contextSchemaVersion));
        }

        public string DesireOwnerId { get; }
        public string DefinitionFingerprint { get; }
        public string ActionStableId { get; }
        public string DesireStableId { get; }
        public string DesireVersion { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public string ContextId { get; }
        public string ContextSchemaId { get; }
        public string ContextSchemaVersion { get; }

        public static KLEPLearnedDesireEffectBucketIdentity From(
            KLEPDesireEffectVector vector,
            KLEPDesireEffectTrace effect)
        {
            if (vector == null)
            {
                throw new ArgumentNullException(nameof(vector));
            }

            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            if (!vector.Attribution.IsEligibleForAutomaticExpectationLearning ||
                !effect.IsEligibleForAutomaticExpectationLearning)
            {
                throw new ArgumentException(
                    "Only ActionOwned Desire effects may enter the critic.",
                    nameof(vector));
            }

            KLEPDesireContextIdentity context = vector.PriorContextIdentity;
            return new KLEPLearnedDesireEffectBucketIdentity(
                vector.OwnerId,
                vector.DefinitionFingerprint,
                vector.Attribution.ActionStableId,
                effect.DesireStableId,
                effect.DesireVersion,
                effect.EvaluatorId,
                effect.EvaluatorVersion,
                context.ContextId,
                context.SchemaId,
                context.SchemaVersion);
        }

        /// <summary>
        /// Reconstructs the exact critic bucket for a prospective action from
        /// one current immutable Desire state. The state must belong to the
        /// supplied snapshot; selection cannot splice identities from two
        /// independently observed Desire moments.
        /// </summary>
        public static KLEPLearnedDesireEffectBucketIdentity ForPrediction(
            KLEPDesireSnapshot snapshot,
            KLEPDesireStateSnapshot desire,
            string actionStableId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (desire == null)
            {
                throw new ArgumentNullException(nameof(desire));
            }

            bool belongsToSnapshot = false;
            for (int index = 0; index < snapshot.Desires.Count; index++)
            {
                if (ReferenceEquals(snapshot.Desires[index], desire))
                {
                    belongsToSnapshot = true;
                    break;
                }
            }

            if (!belongsToSnapshot)
            {
                throw new ArgumentException(
                    "A prospective Desire state must belong to the supplied " +
                    "immutable snapshot.",
                    nameof(desire));
            }

            KLEPDesireContextIdentity context = snapshot.ContextIdentity;
            return new KLEPLearnedDesireEffectBucketIdentity(
                snapshot.OwnerId,
                snapshot.DefinitionFingerprint,
                RequireId(actionStableId, nameof(actionStableId)),
                desire.DesireStableId,
                desire.DesireVersion,
                desire.EvaluatorId,
                desire.EvaluatorVersion,
                context.ContextId,
                context.SchemaId,
                context.SchemaVersion);
        }

        public int CompareTo(KLEPLearnedDesireEffectBucketIdentity other)
        {
            if (other == null)
            {
                return 1;
            }

            int comparison = Compare(DesireOwnerId, other.DesireOwnerId);
            if (comparison == 0)
                comparison = Compare(
                    DefinitionFingerprint, other.DefinitionFingerprint);
            if (comparison == 0)
                comparison = Compare(ActionStableId, other.ActionStableId);
            if (comparison == 0)
                comparison = Compare(DesireStableId, other.DesireStableId);
            if (comparison == 0)
                comparison = Compare(DesireVersion, other.DesireVersion);
            if (comparison == 0)
                comparison = Compare(EvaluatorId, other.EvaluatorId);
            if (comparison == 0)
                comparison = Compare(EvaluatorVersion, other.EvaluatorVersion);
            if (comparison == 0)
                comparison = Compare(ContextId, other.ContextId);
            if (comparison == 0)
                comparison = Compare(ContextSchemaId, other.ContextSchemaId);
            return comparison != 0
                ? comparison
                : Compare(ContextSchemaVersion, other.ContextSchemaVersion);
        }

        public bool Equals(KLEPLearnedDesireEffectBucketIdentity other)
        {
            return other != null && CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as KLEPLearnedDesireEffectBucketIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = AddHash(hash, DesireOwnerId);
                hash = AddHash(hash, DefinitionFingerprint);
                hash = AddHash(hash, ActionStableId);
                hash = AddHash(hash, DesireStableId);
                hash = AddHash(hash, DesireVersion);
                hash = AddHash(hash, EvaluatorId);
                hash = AddHash(hash, EvaluatorVersion);
                hash = AddHash(hash, ContextId);
                hash = AddHash(hash, ContextSchemaId);
                return AddHash(hash, ContextSchemaVersion);
            }
        }

        private static int Compare(string left, string right)
        {
            return StringComparer.Ordinal.Compare(left, right);
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
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// One owner-bound, monotonically sequenced factual Desire effect vector.
    /// </summary>
    public sealed class KLEPLearnedDesireEffectTrial
    {
        public KLEPLearnedDesireEffectTrial(
            string evidenceOwnerStableId,
            string evidenceOwnerVersion,
            long evidenceSequence,
            KLEPDesireEffectVector effectVector)
        {
            EvidenceOwnerStableId = RequireId(
                evidenceOwnerStableId, nameof(evidenceOwnerStableId));
            EvidenceOwnerVersion = RequireId(
                evidenceOwnerVersion, nameof(evidenceOwnerVersion));
            if (evidenceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidenceSequence));
            }

            EffectVector = effectVector ??
                throw new ArgumentNullException(nameof(effectVector));
            if (!effectVector.Attribution
                    .IsEligibleForAutomaticExpectationLearning)
            {
                throw new ArgumentException(
                    "Only ActionOwned Desire effect vectors may enter the " +
                    "learned critic.",
                    nameof(effectVector));
            }

            if (effectVector.Effects.Count == 0)
            {
                throw new ArgumentException(
                    "A learned Desire trial must contain at least one effect.",
                    nameof(effectVector));
            }

            EvidenceSequence = evidenceSequence;
        }

        public string EvidenceOwnerStableId { get; }
        public string EvidenceOwnerVersion { get; }
        public long EvidenceSequence { get; }
        public KLEPDesireEffectVector EffectVector { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable ID is required.", parameterName);
            }

            return value;
        }
    }

    /// <summary>
    /// Immutable online critic estimate for one exact bucket. Variance is the
    /// sample variance (M2 / (N - 1)); it is zero until support reaches two.
    /// </summary>
    public sealed class KLEPLearnedDesireEffectEstimate
    {
        internal KLEPLearnedDesireEffectEstimate(
            KLEPLearnedDesireEffectBucketIdentity bucket,
            long support,
            double meanEffect,
            double sampleVariance,
            double lastPredictionError,
            double confidence,
            long lastEvidenceSequence,
            long lastRevision)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            Support = support;
            MeanEffect = RequireFinite(meanEffect, nameof(meanEffect));
            SampleVariance = RequireFinite(
                sampleVariance, nameof(sampleVariance));
            LastPredictionError = RequireFinite(
                lastPredictionError, nameof(lastPredictionError));
            Confidence = RequireFinite(confidence, nameof(confidence));
            LastEvidenceSequence = lastEvidenceSequence;
            LastRevision = lastRevision;
        }

        public KLEPLearnedDesireEffectBucketIdentity Bucket { get; }
        public long Support { get; }
        public bool IsKnown => Support > 0;
        public double MeanEffect { get; }
        public double SampleVariance { get; }
        public double LastPredictionError { get; }
        public double Confidence { get; }
        public long LastEvidenceSequence { get; }
        public long LastRevision { get; }

        private static double RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    public sealed class KLEPLearnedDesireEffectSnapshot
    {
        private readonly ReadOnlyCollection<KLEPLearnedDesireEffectEstimate>
            estimates;

        internal KLEPLearnedDesireEffectSnapshot(
            string ownerStableId,
            string ownerVersion,
            double confidenceScale,
            long revision,
            long lastEvidenceSequence,
            IEnumerable<KLEPLearnedDesireEffectEstimate> estimates)
        {
            OwnerStableId = ownerStableId;
            OwnerVersion = ownerVersion;
            ConfidenceScale = confidenceScale;
            Revision = revision;
            LastEvidenceSequence = lastEvidenceSequence;
            this.estimates = new ReadOnlyCollection<
                KLEPLearnedDesireEffectEstimate>(
                    new List<KLEPLearnedDesireEffectEstimate>(estimates));
        }

        public string OwnerStableId { get; }
        public string OwnerVersion { get; }
        public double ConfidenceScale { get; }
        public long Revision { get; }
        public long LastEvidenceSequence { get; }
        public IReadOnlyList<KLEPLearnedDesireEffectEstimate> Estimates =>
            estimates;
    }

    public sealed partial class KLEPLearnedExpectations :
        IKLEPLearnedDesireExpectationsView,
        IKLEPLearnedExpectationsDiagnosticView
    {
        private sealed class MutableDesireEstimate
        {
            internal long Support;
            internal double MeanEffect;
            internal double M2;
            internal double LastPredictionError;
            internal long LastEvidenceSequence;
            internal long LastRevision;
        }

        private sealed class PendingDesireUpdate
        {
            internal KLEPLearnedDesireEffectBucketIdentity Bucket;
            internal MutableDesireEstimate Existing;
            internal long Support;
            internal double MeanEffect;
            internal double M2;
            internal double LastPredictionError;
        }

        private readonly Dictionary<KLEPLearnedDesireEffectBucketIdentity,
            MutableDesireEstimate> desireEstimates =
                new Dictionary<KLEPLearnedDesireEffectBucketIdentity,
                    MutableDesireEstimate>();
        private long desireEffectRevision;
        private long lastDesireEffectEvidenceSequence;

        public long DesireEffectRevision => desireEffectRevision;
        public long LastDesireEffectEvidenceSequence =>
            lastDesireEffectEvidenceSequence;

        /// <summary>
        /// Performs one deterministic Welford critic update per raw Desire
        /// effect. The complete vector is preflighted before any bucket mutates.
        /// </summary>
        public KLEPLearnedDesireEffectSnapshot RecordDesireEffects(
            KLEPLearnedDesireEffectTrial trial)
        {
            if (trial == null)
            {
                throw new ArgumentNullException(nameof(trial));
            }

            if (!StringComparer.Ordinal.Equals(
                    ownerStableId, trial.EvidenceOwnerStableId) ||
                !StringComparer.Ordinal.Equals(
                    ownerVersion, trial.EvidenceOwnerVersion))
            {
                throw new ArgumentException(
                    "Desire effect evidence belongs to a different learned-" +
                    "expectation owner.",
                    nameof(trial));
            }

            if (trial.EvidenceSequence <= lastDesireEffectEvidenceSequence)
            {
                throw new InvalidOperationException(
                    "Desire effect evidence sequence must increase strictly; " +
                    "replay and out-of-order evidence are rejected.");
            }

            if (desireEffectRevision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Desire critic revision capacity has been exhausted.");
            }

            long revisionAfter = desireEffectRevision + 1;
            var seen = new HashSet<
                KLEPLearnedDesireEffectBucketIdentity>();
            var pending = new List<PendingDesireUpdate>(
                trial.EffectVector.Effects.Count);
            foreach (KLEPDesireEffectTrace effect in
                     trial.EffectVector.Effects)
            {
                KLEPLearnedDesireEffectBucketIdentity bucket =
                    KLEPLearnedDesireEffectBucketIdentity.From(
                        trial.EffectVector, effect);
                if (!seen.Add(bucket))
                {
                    throw new ArgumentException(
                        "A Desire effect vector cannot update the same exact " +
                        "critic bucket twice.",
                        nameof(trial));
                }

                MutableDesireEstimate current;
                desireEstimates.TryGetValue(bucket, out current);
                long priorSupport = current == null ? 0 : current.Support;
                long support = checked(priorSupport + 1);
                double priorMean = current == null ? 0d : current.MeanEffect;
                double priorM2 = current == null ? 0d : current.M2;
                double observed = effect.Effect;
                double predictionError = observed - priorMean;
                double mean = priorMean + predictionError / support;
                double m2 = priorM2 +
                    predictionError * (observed - mean);
                if (m2 < 0d && m2 > -0.000000000000001d)
                {
                    m2 = 0d;
                }

                RequireFinite(mean, "learned mean effect");
                RequireFinite(m2, "learned effect M2");
                RequireFinite(predictionError, "prediction error");
                if (m2 < 0d)
                {
                    throw new InvalidOperationException(
                        "A Desire critic update produced negative variance.");
                }

                pending.Add(new PendingDesireUpdate
                {
                    Bucket = bucket,
                    Existing = current,
                    Support = support,
                    MeanEffect = mean,
                    M2 = m2,
                    LastPredictionError = predictionError
                });
            }

            foreach (PendingDesireUpdate update in pending)
            {
                MutableDesireEstimate destination = update.Existing;
                if (destination == null)
                {
                    destination = new MutableDesireEstimate();
                    desireEstimates.Add(update.Bucket, destination);
                }

                destination.Support = update.Support;
                destination.MeanEffect = update.MeanEffect;
                destination.M2 = update.M2;
                destination.LastPredictionError =
                    update.LastPredictionError;
                destination.LastEvidenceSequence = trial.EvidenceSequence;
                destination.LastRevision = revisionAfter;
            }

            lastDesireEffectEvidenceSequence = trial.EvidenceSequence;
            desireEffectRevision = revisionAfter;
            return CaptureDesireEffectSnapshot();
        }

        public KLEPLearnedDesireEffectEstimate QueryDesireEffect(
            KLEPLearnedDesireEffectBucketIdentity bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException(nameof(bucket));
            }

            MutableDesireEstimate current;
            return desireEstimates.TryGetValue(bucket, out current)
                ? ToImmutable(bucket, current)
                : new KLEPLearnedDesireEffectEstimate(
                    bucket, 0, 0d, 0d, 0d, 0d, 0, 0);
        }

        public KLEPLearnedDesireEffectSnapshot CaptureDesireEffectSnapshot()
        {
            var copied = new List<KLEPLearnedDesireEffectEstimate>(
                desireEstimates.Count);
            foreach (KeyValuePair<KLEPLearnedDesireEffectBucketIdentity,
                MutableDesireEstimate> pair in desireEstimates)
            {
                copied.Add(ToImmutable(pair.Key, pair.Value));
            }

            copied.Sort((left, right) =>
                left.Bucket.CompareTo(right.Bucket));
            return new KLEPLearnedDesireEffectSnapshot(
                ownerStableId,
                ownerVersion,
                confidenceScale,
                desireEffectRevision,
                lastDesireEffectEvidenceSequence,
                copied);
        }

        private KLEPLearnedDesireEffectEstimate ToImmutable(
            KLEPLearnedDesireEffectBucketIdentity bucket,
            MutableDesireEstimate source)
        {
            double variance = source.Support > 1
                ? source.M2 / (source.Support - 1)
                : 0d;
            double confidence = source.Support /
                (source.Support + confidenceScale);
            return new KLEPLearnedDesireEffectEstimate(
                bucket,
                source.Support,
                source.MeanEffect,
                variance,
                source.LastPredictionError,
                confidence,
                source.LastEvidenceSequence,
                source.LastRevision);
        }

        private static double RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException(name + " must be finite.");
            }

            return value;
        }
    }
}
