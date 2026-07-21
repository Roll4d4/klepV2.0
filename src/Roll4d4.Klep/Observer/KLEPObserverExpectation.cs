using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Core;

namespace Roll4d4.Klep.Observer
{
    /// <summary>
    /// The factual disposition of one explicitly supplied expectation trial.
    /// Censored trials remain evidence but do not enter completed counts.
    /// </summary>
    public enum KLEPObserverExpectationTrialOutcome
    {
        Observed,
        NotObserved,
        Censored
    }

    /// <summary>
    /// The exact Key observation being estimated. PresentAfter asks whether the
    /// Key is present at the consequence boundary. Acquired additionally
    /// requires it to have been absent at the prior boundary.
    /// </summary>
    public enum KLEPObserverExpectationObservationMeaning
    {
        PresentAfter,
        Acquired
    }

    public enum KLEPObserverExpectationKnowledge
    {
        Unknown,
        Known
    }

    /// <summary>
    /// Project-owned exact context identity. KLEP assigns no meaning or
    /// similarity to any field; equality is ordinal equality of every field.
    /// </summary>
    public sealed class KLEPObserverExpectationContextIdentity :
        IEquatable<KLEPObserverExpectationContextIdentity>,
        IComparable<KLEPObserverExpectationContextIdentity>
    {
        public KLEPObserverExpectationContextIdentity(
            string contextId,
            string schemaId,
            string schemaVersion,
            string fingerprint)
        {
            ContextId = ExpectationValidation.RequireId(
                contextId, nameof(contextId));
            SchemaId = ExpectationValidation.RequireId(
                schemaId, nameof(schemaId));
            SchemaVersion = ExpectationValidation.RequireId(
                schemaVersion, nameof(schemaVersion));
            Fingerprint = ExpectationValidation.RequireId(
                fingerprint, nameof(fingerprint));
        }

        public string ContextId { get; }
        public string SchemaId { get; }
        public string SchemaVersion { get; }
        public string Fingerprint { get; }

        public int CompareTo(KLEPObserverExpectationContextIdentity other)
        {
            if (other == null)
            {
                return 1;
            }

            int comparison = StringComparer.Ordinal.Compare(
                ContextId, other.ContextId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(SchemaId, other.SchemaId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                SchemaVersion, other.SchemaVersion);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(Fingerprint, other.Fingerprint);
        }

        public bool Equals(KLEPObserverExpectationContextIdentity other)
        {
            return other != null &&
                StringComparer.Ordinal.Equals(ContextId, other.ContextId) &&
                StringComparer.Ordinal.Equals(SchemaId, other.SchemaId) &&
                StringComparer.Ordinal.Equals(
                    SchemaVersion, other.SchemaVersion) &&
                StringComparer.Ordinal.Equals(Fingerprint, other.Fingerprint);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPObserverExpectationContextIdentity other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ContextId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SchemaId);
                hash = (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(SchemaVersion);
                return (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(Fingerprint);
            }
        }

        public override string ToString()
        {
            return ContextId + "@" + SchemaId + ":" + SchemaVersion +
                "#" + Fingerprint;
        }
    }

    /// <summary>
    /// Project-owned exact observation horizon. KLEP does not infer time,
    /// distance, completion, or route semantics from these strings.
    /// </summary>
    public sealed class KLEPObserverExpectationHorizonIdentity :
        IEquatable<KLEPObserverExpectationHorizonIdentity>,
        IComparable<KLEPObserverExpectationHorizonIdentity>
    {
        public KLEPObserverExpectationHorizonIdentity(
            string horizonId,
            string horizonVersion)
        {
            HorizonId = ExpectationValidation.RequireId(
                horizonId, nameof(horizonId));
            HorizonVersion = ExpectationValidation.RequireId(
                horizonVersion, nameof(horizonVersion));
        }

        public string HorizonId { get; }
        public string HorizonVersion { get; }

        public int CompareTo(KLEPObserverExpectationHorizonIdentity other)
        {
            if (other == null)
            {
                return 1;
            }

            int comparison = StringComparer.Ordinal.Compare(
                HorizonId, other.HorizonId);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(
                    HorizonVersion, other.HorizonVersion);
        }

        public bool Equals(KLEPObserverExpectationHorizonIdentity other)
        {
            return other != null &&
                StringComparer.Ordinal.Equals(HorizonId, other.HorizonId) &&
                StringComparer.Ordinal.Equals(
                    HorizonVersion, other.HorizonVersion);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPObserverExpectationHorizonIdentity other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(HorizonId) * 397) ^
                    StringComparer.Ordinal.GetHashCode(HorizonVersion);
            }
        }

        public override string ToString()
        {
            return HorizonId + "@" + HorizonVersion;
        }
    }

    /// <summary>
    /// Exact aggregation identity for empirical expectation evidence. A source
    /// with the same stable ID under a different catalog fingerprint or root
    /// tenure necessarily belongs to a different bucket.
    /// </summary>
    public sealed class KLEPObserverExpectationBucketIdentity :
        IEquatable<KLEPObserverExpectationBucketIdentity>,
        IComparable<KLEPObserverExpectationBucketIdentity>
    {
        private KLEPObserverExpectationBucketIdentity(
            KLEPStructuralMapFingerprint catalogFingerprint,
            string sourceExecutableId,
            string sourceRootTenureId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon)
        {
            CatalogFingerprint = catalogFingerprint ??
                throw new ArgumentNullException(nameof(catalogFingerprint));
            SourceExecutableId = ExpectationValidation.RequireId(
                sourceExecutableId, nameof(sourceExecutableId));
            SourceRootTenureId = ExpectationValidation.RequireId(
                sourceRootTenureId, nameof(sourceRootTenureId));
            OutcomeKeyId = ExpectationValidation.RequireKeyId(
                outcomeKeyId, nameof(outcomeKeyId));
            ObservationMeaning =
                ExpectationValidation.RequireObservationMeaning(
                    observationMeaning, nameof(observationMeaning));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Horizon = horizon ?? throw new ArgumentNullException(nameof(horizon));
        }

        public KLEPStructuralMapFingerprint CatalogFingerprint { get; }
        public string SourceExecutableId { get; }
        public string SourceRootTenureId { get; }
        public KLEPKeyId OutcomeKeyId { get; }
        public KLEPObserverExpectationObservationMeaning ObservationMeaning { get; }
        public KLEPObserverExpectationContextIdentity Context { get; }
        public KLEPObserverExpectationHorizonIdentity Horizon { get; }

        internal static KLEPObserverExpectationBucketIdentity Bind(
            KLEPObserverSelfModel selfModel,
            string sourceExecutableId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon,
            out KLEPExecutableStructuralNode sourceNode)
        {
            if (selfModel == null)
            {
                throw new ArgumentNullException(nameof(selfModel));
            }

            string requiredSourceId = ExpectationValidation.RequireId(
                sourceExecutableId, nameof(sourceExecutableId));
            if (!selfModel.StructuralMap.TryGetExecutable(
                requiredSourceId, out sourceNode))
            {
                throw new ArgumentException(
                    "The expectation source Executable is not mapped by the " +
                    "supplied accepted Observer self-model.",
                    nameof(sourceExecutableId));
            }

            return new KLEPObserverExpectationBucketIdentity(
                selfModel.CatalogFingerprint,
                requiredSourceId,
                sourceNode.RootTenureId,
                outcomeKeyId,
                observationMeaning,
                context,
                horizon);
        }

        public int CompareTo(KLEPObserverExpectationBucketIdentity other)
        {
            if (other == null)
            {
                return 1;
            }

            int comparison = CatalogFingerprint.CompareTo(
                other.CatalogFingerprint);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                SourceExecutableId, other.SourceExecutableId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(
                SourceRootTenureId, other.SourceRootTenureId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = OutcomeKeyId.CompareTo(other.OutcomeKeyId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = ((int)ObservationMeaning).CompareTo(
                (int)other.ObservationMeaning);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = Context.CompareTo(other.Context);
            return comparison != 0
                ? comparison
                : Horizon.CompareTo(other.Horizon);
        }

        public bool Equals(KLEPObserverExpectationBucketIdentity other)
        {
            return other != null &&
                CatalogFingerprint.Equals(other.CatalogFingerprint) &&
                StringComparer.Ordinal.Equals(
                    SourceExecutableId, other.SourceExecutableId) &&
                StringComparer.Ordinal.Equals(
                    SourceRootTenureId, other.SourceRootTenureId) &&
                OutcomeKeyId == other.OutcomeKeyId &&
                ObservationMeaning == other.ObservationMeaning &&
                Context.Equals(other.Context) &&
                Horizon.Equals(other.Horizon);
        }

        public override bool Equals(object obj)
        {
            return obj is KLEPObserverExpectationBucketIdentity other &&
                Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CatalogFingerprint.GetHashCode();
                hash = (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(SourceExecutableId);
                hash = (hash * 397) ^
                    StringComparer.Ordinal.GetHashCode(SourceRootTenureId);
                hash = (hash * 397) ^ OutcomeKeyId.GetHashCode();
                hash = (hash * 397) ^ (int)ObservationMeaning;
                hash = (hash * 397) ^ Context.GetHashCode();
                return (hash * 397) ^ Horizon.GetHashCode();
            }
        }

        public override string ToString()
        {
            return CatalogFingerprint.Value + ":" + SourceRootTenureId + ":" +
                SourceExecutableId + "->" + OutcomeKeyId + ":" +
                ObservationMeaning + ":" + Context + ":" + Horizon;
        }
    }

    /// <summary>
    /// One explicit factual temporal-association trial. Construction verifies
    /// that its source is mapped in the accepted self-model and, for completed
    /// outcomes, that the declared disposition agrees with exact prior and
    /// consequence Key snapshots. This does not claim causation.
    /// </summary>
    public sealed class KLEPObserverExpectationTrial
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPObserverExpectationTrial(
            string evidenceOwnerStableId,
            string evidenceOwnerVersion,
            long evidenceSequence,
            string trialId,
            IEnumerable<string> evidenceIds,
            KLEPObserverSelfModel acceptedSelfModel,
            string sourceExecutableId,
            string sourceRunId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon,
            KLEPKeySnapshot consequenceSnapshot,
            KLEPObserverExpectationTrialOutcome outcome)
        {
            EvidenceOwnerStableId = ExpectationValidation.RequireId(
                evidenceOwnerStableId, nameof(evidenceOwnerStableId));
            EvidenceOwnerVersion = ExpectationValidation.RequireId(
                evidenceOwnerVersion, nameof(evidenceOwnerVersion));
            if (evidenceSequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidenceSequence));
            }

            EvidenceSequence = evidenceSequence;
            TrialId = ExpectationValidation.RequireId(trialId, nameof(trialId));
            this.evidenceIds = ExpectationValidation.CopyIds(
                evidenceIds, nameof(evidenceIds), requireAtLeastOne: true);
            SourceRunId = ExpectationValidation.RequireId(
                sourceRunId, nameof(sourceRunId));
            Outcome = ExpectationValidation.RequireOutcome(
                outcome, nameof(outcome));

            if (acceptedSelfModel == null)
            {
                throw new ArgumentNullException(nameof(acceptedSelfModel));
            }

            if (!StringComparer.Ordinal.Equals(
                    EvidenceOwnerStableId, acceptedSelfModel.ModelerStableId) ||
                !StringComparer.Ordinal.Equals(
                    EvidenceOwnerVersion, acceptedSelfModel.ModelerVersion))
            {
                throw new ArgumentException(
                    "Expectation evidence and its accepted self-model must " +
                    "belong to the same Observer identity and version.",
                    nameof(acceptedSelfModel));
            }

            KLEPExecutableStructuralNode sourceNode;
            Bucket = KLEPObserverExpectationBucketIdentity.Bind(
                acceptedSelfModel,
                sourceExecutableId,
                outcomeKeyId,
                observationMeaning,
                context,
                horizon,
                out sourceNode);

            if (consequenceSnapshot == null)
            {
                throw new ArgumentNullException(nameof(consequenceSnapshot));
            }

            KLEPKeySnapshot priorSnapshot = acceptedSelfModel.KeySnapshot;
            ExpectationValidation.RequireLaterBoundary(
                priorSnapshot, consequenceSnapshot, nameof(consequenceSnapshot));

            ModelerStableId = acceptedSelfModel.ModelerStableId;
            ModelerVersion = acceptedSelfModel.ModelerVersion;
            StructuralObserverStableId =
                acceptedSelfModel.StructuralObserverStableId;
            StructuralObserverVersion =
                acceptedSelfModel.StructuralObserverVersion;
            CatalogRevision = acceptedSelfModel.CatalogRevision;
            SourceExecutablePath = sourceNode.Path;
            PriorEvidenceFingerprint = acceptedSelfModel.EvidenceFingerprint;
            ConsequenceEvidenceFingerprint =
                KLEPGuidanceEvidenceFingerprint.FromSnapshot(consequenceSnapshot);
            PriorTick = priorSnapshot.Tick;
            PriorWaveIndex = priorSnapshot.WaveIndex;
            ConsequenceTick = consequenceSnapshot.Tick;
            ConsequenceWaveIndex = consequenceSnapshot.WaveIndex;
            PriorContainedOutcomeKey = priorSnapshot.Contains(outcomeKeyId);
            ConsequenceContainedOutcomeKey =
                consequenceSnapshot.Contains(outcomeKeyId);

            bool wasObserved = observationMeaning ==
                KLEPObserverExpectationObservationMeaning.PresentAfter
                    ? ConsequenceContainedOutcomeKey
                    : !PriorContainedOutcomeKey &&
                        ConsequenceContainedOutcomeKey;

            // A censored trial records only the evidence available at its censor
            // boundary. It deliberately makes no completed disposition claim.
            if (Outcome != KLEPObserverExpectationTrialOutcome.Censored &&
                (Outcome == KLEPObserverExpectationTrialOutcome.Observed) !=
                    wasObserved)
            {
                throw new ArgumentException(
                    "The completed expectation outcome does not agree with its " +
                    "exact prior/consequence Key evidence.",
                    nameof(outcome));
            }
        }

        public string EvidenceOwnerStableId { get; }
        public string EvidenceOwnerVersion { get; }
        public long EvidenceSequence { get; }
        public string TrialId { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;
        public KLEPObserverExpectationBucketIdentity Bucket { get; }
        public string SourceRunId { get; }
        public string SourceExecutablePath { get; }
        public string ModelerStableId { get; }
        public string ModelerVersion { get; }
        public string StructuralObserverStableId { get; }
        public string StructuralObserverVersion { get; }
        public string CatalogRevision { get; }
        public KLEPGuidanceEvidenceFingerprint PriorEvidenceFingerprint { get; }
        public KLEPGuidanceEvidenceFingerprint ConsequenceEvidenceFingerprint { get; }
        public long PriorTick { get; }
        public int PriorWaveIndex { get; }
        public long ConsequenceTick { get; }
        public int ConsequenceWaveIndex { get; }
        public bool PriorContainedOutcomeKey { get; }
        public bool ConsequenceContainedOutcomeKey { get; }
        public KLEPObserverExpectationTrialOutcome Outcome { get; }
        public bool IsCompleted =>
            Outcome != KLEPObserverExpectationTrialOutcome.Censored;
    }

    /// <summary>
    /// Immutable exact-bucket counts and their two deliberately distinct
    /// measures. Likelihood is absent while completed evidence is unknown.
    /// </summary>
    public sealed class KLEPObserverExpectationAggregate
    {
        private const double MaximumFiniteEvidenceConfidence =
            0.9999999999999999d;

        internal KLEPObserverExpectationAggregate(
            KLEPObserverExpectationBucketIdentity bucket,
            long observedCount,
            long notObservedCount,
            long censoredCount,
            long lastEvidenceSequence,
            long lastRevision,
            double confidenceScale)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            ObservedCount = ExpectationValidation.RequireNonNegative(
                observedCount, nameof(observedCount));
            NotObservedCount = ExpectationValidation.RequireNonNegative(
                notObservedCount, nameof(notObservedCount));
            CensoredCount = ExpectationValidation.RequireNonNegative(
                censoredCount, nameof(censoredCount));
            CompletedCount = checked(ObservedCount + NotObservedCount);
            LastEvidenceSequence = ExpectationValidation.RequireNonNegative(
                lastEvidenceSequence, nameof(lastEvidenceSequence));
            LastRevision = ExpectationValidation.RequireNonNegative(
                lastRevision, nameof(lastRevision));
            ConfidenceScale = ExpectationValidation.RequirePositiveFinite(
                confidenceScale, nameof(confidenceScale));
            Knowledge = CompletedCount == 0
                ? KLEPObserverExpectationKnowledge.Unknown
                : KLEPObserverExpectationKnowledge.Known;
            Likelihood = CompletedCount == 0
                ? (double?)null
                : (double)ObservedCount / CompletedCount;
            double computedConfidence = CompletedCount == 0
                ? 0d
                : (double)CompletedCount /
                    ((double)CompletedCount + ConfidenceScale);
            // The real-number formula is strictly below one for finite N.
            // Preserve that fact when double precision rounds the ratio up.
            Confidence = computedConfidence >= 1d
                ? MaximumFiniteEvidenceConfidence
                : computedConfidence;
        }

        public KLEPObserverExpectationBucketIdentity Bucket { get; }
        public long ObservedCount { get; }
        public long NotObservedCount { get; }
        public long CensoredCount { get; }
        public long CompletedCount { get; }
        public long TotalEvidenceCount => checked(CompletedCount + CensoredCount);
        public long LastEvidenceSequence { get; }
        public long LastRevision { get; }
        public double ConfidenceScale { get; }
        public KLEPObserverExpectationKnowledge Knowledge { get; }
        public bool IsKnown => Knowledge == KLEPObserverExpectationKnowledge.Known;
        public double? Likelihood { get; }
        public double Confidence { get; }
    }

    /// <summary>
    /// One atomic ledger revision. Trial contains the exact replay material;
    /// before/after aggregates make censored and counted changes inspectable.
    /// </summary>
    public sealed class KLEPObserverExpectationUpdateTrace
    {
        internal KLEPObserverExpectationUpdateTrace(
            long revisionBefore,
            long revisionAfter,
            KLEPObserverExpectationTrial trial,
            KLEPObserverExpectationAggregate aggregateBefore,
            KLEPObserverExpectationAggregate aggregateAfter)
        {
            RevisionBefore = ExpectationValidation.RequireNonNegative(
                revisionBefore, nameof(revisionBefore));
            RevisionAfter = ExpectationValidation.RequirePositive(
                revisionAfter, nameof(revisionAfter));
            if (revisionAfter != revisionBefore + 1)
            {
                throw new ArgumentException(
                    "An expectation update must advance exactly one revision.",
                    nameof(revisionAfter));
            }

            Trial = trial ?? throw new ArgumentNullException(nameof(trial));
            AggregateBefore = aggregateBefore ?? throw new ArgumentNullException(
                nameof(aggregateBefore));
            AggregateAfter = aggregateAfter ?? throw new ArgumentNullException(
                nameof(aggregateAfter));
            if (!Trial.Bucket.Equals(AggregateBefore.Bucket) ||
                !Trial.Bucket.Equals(AggregateAfter.Bucket))
            {
                throw new ArgumentException(
                    "An expectation update trace requires one exact bucket.");
            }
        }

        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public KLEPObserverExpectationTrial Trial { get; }
        public KLEPObserverExpectationAggregate AggregateBefore { get; }
        public KLEPObserverExpectationAggregate AggregateAfter { get; }
    }

    public sealed class KLEPObserverExpectationQueryResult
    {
        internal KLEPObserverExpectationQueryResult(
            string ownerStableId,
            string ownerVersion,
            long ledgerRevision,
            KLEPObserverExpectationAggregate aggregate)
        {
            OwnerStableId = ExpectationValidation.RequireId(
                ownerStableId, nameof(ownerStableId));
            OwnerVersion = ExpectationValidation.RequireId(
                ownerVersion, nameof(ownerVersion));
            LedgerRevision = ExpectationValidation.RequireNonNegative(
                ledgerRevision, nameof(ledgerRevision));
            Aggregate = aggregate ?? throw new ArgumentNullException(
                nameof(aggregate));
        }

        public string OwnerStableId { get; }
        public string OwnerVersion { get; }
        public long LedgerRevision { get; }
        public KLEPObserverExpectationAggregate Aggregate { get; }
        public KLEPObserverExpectationBucketIdentity Bucket => Aggregate.Bucket;
        public KLEPObserverExpectationKnowledge Knowledge => Aggregate.Knowledge;
        public bool IsKnown => Aggregate.IsKnown;
        public double? Likelihood => Aggregate.Likelihood;
        public double Confidence => Aggregate.Confidence;
        public long CompletedCount => Aggregate.CompletedCount;
        public long ObservedCount => Aggregate.ObservedCount;
        public long NotObservedCount => Aggregate.NotObservedCount;
        public long CensoredCount => Aggregate.CensoredCount;
    }

    public sealed class KLEPObserverExpectationSnapshot
    {
        private readonly ReadOnlyCollection<KLEPObserverExpectationAggregate>
            aggregates;

        internal KLEPObserverExpectationSnapshot(
            string ownerStableId,
            string ownerVersion,
            double confidenceScale,
            long revision,
            long lastEvidenceSequence,
            IEnumerable<KLEPObserverExpectationAggregate> aggregates,
            KLEPObserverExpectationUpdateTrace lastUpdate)
        {
            OwnerStableId = ExpectationValidation.RequireId(
                ownerStableId, nameof(ownerStableId));
            OwnerVersion = ExpectationValidation.RequireId(
                ownerVersion, nameof(ownerVersion));
            ConfidenceScale = ExpectationValidation.RequirePositiveFinite(
                confidenceScale, nameof(confidenceScale));
            Revision = ExpectationValidation.RequireNonNegative(
                revision, nameof(revision));
            LastEvidenceSequence = ExpectationValidation.RequireNonNegative(
                lastEvidenceSequence, nameof(lastEvidenceSequence));
            this.aggregates = ExpectationValidation.CopyAggregates(aggregates);
            ValidateState(lastUpdate);
            LastUpdate = lastUpdate;
        }

        public string OwnerStableId { get; }
        public string OwnerVersion { get; }
        public double ConfidenceScale { get; }
        public long Revision { get; }
        public long LastEvidenceSequence { get; }
        public IReadOnlyList<KLEPObserverExpectationAggregate> Aggregates =>
            aggregates;
        public KLEPObserverExpectationUpdateTrace LastUpdate { get; }

        private void ValidateState(KLEPObserverExpectationUpdateTrace lastUpdate)
        {
            bool emptyMatches = Revision == 0 && LastEvidenceSequence == 0 &&
                lastUpdate == null && Aggregates.Count == 0;
            bool populatedMatches = Revision > 0 && LastEvidenceSequence > 0 &&
                lastUpdate != null && lastUpdate.RevisionAfter == Revision &&
                lastUpdate.Trial.EvidenceSequence == LastEvidenceSequence;
            if (!emptyMatches && !populatedMatches)
            {
                throw new ArgumentException(nameof(lastUpdate));
            }
        }

        public bool TryGetAggregate(
            KLEPObserverExpectationBucketIdentity bucket,
            out KLEPObserverExpectationAggregate aggregate)
        {
            if (bucket == null)
            {
                aggregate = null;
                return false;
            }

            foreach (KLEPObserverExpectationAggregate candidate in aggregates)
            {
                if (candidate.Bucket.Equals(bucket))
                {
                    aggregate = candidate;
                    return true;
                }
            }

            aggregate = null;
            return false;
        }
    }

    /// <summary>
    /// Read-only expectation evidence that an Observer may query while
    /// comparing mapped possibilities. The mutable learned authority lives in
    /// the independent LearnedExpectations subsystem; this view intentionally
    /// exposes no Record, reset, replay, or persistence operation.
    /// </summary>
    public interface IKLEPLearnedExpectationsView
    {
        string OwnerStableId { get; }
        string OwnerVersion { get; }
        double ConfidenceScale { get; }
        long Revision { get; }
        long LastEvidenceSequence { get; }

        KLEPObserverExpectationQueryResult Query(
            KLEPObserverSelfModel acceptedSelfModel,
            string sourceExecutableId,
            KLEPKeyId outcomeKeyId,
            KLEPObserverExpectationObservationMeaning observationMeaning,
            KLEPObserverExpectationContextIdentity context,
            KLEPObserverExpectationHorizonIdentity horizon);

        KLEPObserverExpectationSnapshot CaptureSnapshot();
    }

    internal static class ExpectationValidation
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

        internal static KLEPKeyId RequireKeyId(
            KLEPKeyId value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value.Value))
            {
                throw new ArgumentException(
                    "A non-empty outcome Key ID is required.", parameterName);
            }

            return value;
        }

        internal static KLEPObserverExpectationObservationMeaning
            RequireObservationMeaning(
                KLEPObserverExpectationObservationMeaning value,
                string parameterName)
        {
            if (value != KLEPObserverExpectationObservationMeaning.PresentAfter &&
                value != KLEPObserverExpectationObservationMeaning.Acquired)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static KLEPObserverExpectationTrialOutcome RequireOutcome(
            KLEPObserverExpectationTrialOutcome value,
            string parameterName)
        {
            if (value != KLEPObserverExpectationTrialOutcome.Observed &&
                value != KLEPObserverExpectationTrialOutcome.NotObserved &&
                value != KLEPObserverExpectationTrialOutcome.Censored)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static double RequirePositiveFinite(
            double value,
            string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static long RequireNonNegative(long value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static long RequirePositive(long value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static long Increment(long value, string description)
        {
            if (value == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "The " + description + " has exhausted its capacity.");
            }

            return value + 1;
        }

        internal static void RequireLaterBoundary(
            KLEPKeySnapshot prior,
            KLEPKeySnapshot consequence,
            string parameterName)
        {
            if (consequence.Tick < prior.Tick ||
                (consequence.Tick == prior.Tick &&
                    consequence.WaveIndex <= prior.WaveIndex))
            {
                throw new ArgumentException(
                    "An expectation consequence boundary must be later than " +
                    "the accepted self-model evidence boundary.",
                    parameterName);
            }
        }

        internal static ReadOnlyCollection<string> CopyIds(
            IEnumerable<string> source,
            string parameterName,
            bool requireAtLeastOne)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<string>();
            foreach (string id in source)
            {
                copy.Add(RequireId(id, parameterName));
            }

            if (requireAtLeastOne && copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one immutable evidence ID is required.",
                    parameterName);
            }

            copy.Sort(StringComparer.Ordinal);
            for (int index = 1; index < copy.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(
                    copy[index - 1], copy[index]))
                {
                    throw new ArgumentException(
                        "Expectation evidence IDs must be unique.",
                        parameterName);
                }
            }

            return new ReadOnlyCollection<string>(copy);
        }

        internal static ReadOnlyCollection<KLEPObserverExpectationAggregate>
            CopyAggregates(
                IEnumerable<KLEPObserverExpectationAggregate> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<KLEPObserverExpectationAggregate>();
            foreach (KLEPObserverExpectationAggregate aggregate in source)
            {
                copy.Add(aggregate ?? throw new ArgumentException(
                    "Expectation aggregates cannot contain null.",
                    nameof(source)));
            }

            return new ReadOnlyCollection<KLEPObserverExpectationAggregate>(copy);
        }

    }
}
