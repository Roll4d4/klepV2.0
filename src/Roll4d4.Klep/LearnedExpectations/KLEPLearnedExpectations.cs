using System;
using System.Collections.Generic;
using Roll4d4.Klep.Core;
using Roll4d4.Klep.Observer;

namespace Roll4d4.Klep.LearnedExpectations
{
    /// <summary>
    /// Independent learned authority for exact empirical Executable-to-later-
    /// Key associations. It owns mutation, support counts, likelihood, and
    /// confidence. It discovers no trials, mutates no structural guarantee,
    /// emits no influence, and owns no execution, Desire value, or Memory state.
    /// </summary>
    public sealed partial class KLEPLearnedExpectations :
        IKLEPLearnedExpectationsView
    {
        private sealed class MutableAggregate
        {
            internal long ObservedCount;
            internal long NotObservedCount;
            internal long CensoredCount;
            internal long LastEvidenceSequence;
            internal long LastRevision;
        }

        private readonly string ownerStableId;
        private readonly string ownerVersion;
        private readonly double confidenceScale;
        private readonly Dictionary<KLEPObserverExpectationBucketIdentity,
            MutableAggregate> aggregates =
                new Dictionary<KLEPObserverExpectationBucketIdentity,
                    MutableAggregate>();
        private long revision;
        private long lastEvidenceSequence;
        private KLEPObserverExpectationUpdateTrace lastUpdate;

        public KLEPLearnedExpectations(
            string ownerStableId,
            string ownerVersion,
            double confidenceScale = 4d)
        {
            this.ownerStableId = ExpectationValidation.RequireId(
                ownerStableId, nameof(ownerStableId));
            this.ownerVersion = ExpectationValidation.RequireId(
                ownerVersion, nameof(ownerVersion));
            this.confidenceScale = ExpectationValidation.RequirePositiveFinite(
                confidenceScale, nameof(confidenceScale));
        }

        public string OwnerStableId => ownerStableId;
        public string OwnerVersion => ownerVersion;
        public double ConfidenceScale => confidenceScale;
        public long Revision => revision;
        public long LastEvidenceSequence => lastEvidenceSequence;

        public KLEPObserverExpectationUpdateTrace Record(
            KLEPObserverExpectationTrial trial)
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
                    "Expectation evidence belongs to a different learned-" +
                    "expectation owner.",
                    nameof(trial));
            }

            if (trial.EvidenceSequence <= lastEvidenceSequence)
            {
                throw new InvalidOperationException(
                    "Expectation evidence sequence must increase strictly; " +
                    "replay and out-of-order evidence are rejected.");
            }

            if (revision == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Expectation revision capacity has been exhausted.");
            }

            MutableAggregate current;
            bool hasCurrent = aggregates.TryGetValue(trial.Bucket, out current);
            long observedBefore = hasCurrent ? current.ObservedCount : 0;
            long notObservedBefore = hasCurrent ? current.NotObservedCount : 0;
            long censoredBefore = hasCurrent ? current.CensoredCount : 0;
            long nextObserved = observedBefore;
            long nextNotObserved = notObservedBefore;
            long nextCensored = censoredBefore;

            switch (trial.Outcome)
            {
                case KLEPObserverExpectationTrialOutcome.Observed:
                    nextObserved = ExpectationValidation.Increment(
                        nextObserved, "observed expectation count");
                    break;
                case KLEPObserverExpectationTrialOutcome.NotObserved:
                    nextNotObserved = ExpectationValidation.Increment(
                        nextNotObserved, "not-observed expectation count");
                    break;
                case KLEPObserverExpectationTrialOutcome.Censored:
                    nextCensored = ExpectationValidation.Increment(
                        nextCensored, "censored expectation count");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trial));
            }

            checked
            {
                long ignoredCompleted = nextObserved + nextNotObserved;
                long ignoredTotal = ignoredCompleted + nextCensored;
                if (ignoredTotal < 0)
                {
                    throw new OverflowException();
                }
            }

            long revisionBefore = revision;
            long revisionAfter = revisionBefore + 1;
            var before = new KLEPObserverExpectationAggregate(
                trial.Bucket,
                observedBefore,
                notObservedBefore,
                censoredBefore,
                hasCurrent ? current.LastEvidenceSequence : 0,
                hasCurrent ? current.LastRevision : 0,
                confidenceScale);
            var after = new KLEPObserverExpectationAggregate(
                trial.Bucket,
                nextObserved,
                nextNotObserved,
                nextCensored,
                trial.EvidenceSequence,
                revisionAfter,
                confidenceScale);
            var trace = new KLEPObserverExpectationUpdateTrace(
                revisionBefore,
                revisionAfter,
                trial,
                before,
                after);

            if (!hasCurrent)
            {
                current = new MutableAggregate();
                aggregates.Add(trial.Bucket, current);
            }

            current.ObservedCount = nextObserved;
            current.NotObservedCount = nextNotObserved;
            current.CensoredCount = nextCensored;
            current.LastEvidenceSequence = trial.EvidenceSequence;
            current.LastRevision = revisionAfter;
            lastUpdate = trace;
            lastEvidenceSequence = trial.EvidenceSequence;
            revision = revisionAfter;
            return trace;
        }

        public KLEPObserverExpectationQueryResult Query(
            KLEPObserverSelfModel acceptedSelfModel,
            string sourceExecutableId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon)
        {
            if (acceptedSelfModel == null)
            {
                throw new ArgumentNullException(nameof(acceptedSelfModel));
            }

            if (!StringComparer.Ordinal.Equals(
                    ownerStableId, acceptedSelfModel.ModelerStableId) ||
                !StringComparer.Ordinal.Equals(
                    ownerVersion, acceptedSelfModel.ModelerVersion))
            {
                throw new ArgumentException(
                    "An expectation query requires a self-model bound to " +
                    "this learned-expectation identity and version.",
                    nameof(acceptedSelfModel));
            }

            KLEPExecutableStructuralNode ignored;
            KLEPObserverExpectationBucketIdentity bucket =
                KLEPObserverExpectationBucketIdentity.Bind(
                    acceptedSelfModel,
                    sourceExecutableId,
                    outcomeKeyId,
                    observationMeaning,
                    context,
                    horizon,
                    out ignored);

            MutableAggregate current;
            KLEPObserverExpectationAggregate aggregate =
                aggregates.TryGetValue(bucket, out current)
                    ? ToImmutable(bucket, current)
                    : new KLEPObserverExpectationAggregate(
                        bucket, 0, 0, 0, 0, 0, confidenceScale);
            return new KLEPObserverExpectationQueryResult(
                ownerStableId, ownerVersion, revision, aggregate);
        }

        public KLEPObserverExpectationSnapshot CaptureSnapshot()
        {
            var copiedAggregates =
                new List<KLEPObserverExpectationAggregate>(aggregates.Count);
            foreach (KeyValuePair<KLEPObserverExpectationBucketIdentity,
                MutableAggregate> pair in aggregates)
            {
                copiedAggregates.Add(ToImmutable(pair.Key, pair.Value));
            }

            copiedAggregates.Sort((left, right) =>
                left.Bucket.CompareTo(right.Bucket));
            return new KLEPObserverExpectationSnapshot(
                ownerStableId,
                ownerVersion,
                confidenceScale,
                revision,
                lastEvidenceSequence,
                copiedAggregates,
                lastUpdate);
        }

        public static KLEPLearnedExpectations Replay(
            string ownerStableId,
            string ownerVersion,
            IEnumerable<KLEPObserverExpectationTrial> orderedTrials,
            double confidenceScale = 4d)
        {
            if (orderedTrials == null)
            {
                throw new ArgumentNullException(nameof(orderedTrials));
            }

            var replayed = new KLEPLearnedExpectations(
                ownerStableId, ownerVersion, confidenceScale);
            foreach (KLEPObserverExpectationTrial trial in orderedTrials)
            {
                replayed.Record(trial ?? throw new ArgumentException(
                    "An expectation replay cannot contain null trials.",
                    nameof(orderedTrials)));
            }

            return replayed;
        }

        private KLEPObserverExpectationAggregate ToImmutable(
            KLEPObserverExpectationBucketIdentity bucket,
            MutableAggregate source)
        {
            return new KLEPObserverExpectationAggregate(
                bucket,
                source.ObservedCount,
                source.NotObservedCount,
                source.CensoredCount,
                source.LastEvidenceSequence,
                source.LastRevision,
                confidenceScale);
        }
    }
}
