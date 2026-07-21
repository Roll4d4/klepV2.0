using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Roll4d4.Klep.Desire
{
    /// <summary>
    /// The caller's explicit causal classification for one observed Desire
    /// transition. Desire records this provenance but does not infer it.
    /// </summary>
    public enum KLEPDesireEffectAttribution
    {
        ActionOwned,
        External,
        Mixed,
        Unknown
    }

    /// <summary>
    /// Immutable identity for the transient project context evaluated in one
    /// Desire observation. The live context itself is never retained.
    /// </summary>
    public sealed class KLEPDesireContextIdentity
    {
        public KLEPDesireContextIdentity(
            string contextId,
            string schemaId,
            string schemaVersion)
        {
            ContextId = KLEPDesireValidation.RequireId(
                contextId,
                nameof(contextId));
            SchemaId = KLEPDesireValidation.RequireId(
                schemaId,
                nameof(schemaId));
            SchemaVersion = KLEPDesireValidation.RequireId(
                schemaVersion,
                nameof(schemaVersion));
        }

        public string ContextId { get; }
        public string SchemaId { get; }
        public string SchemaVersion { get; }
    }

    /// <summary>
    /// One project evaluator's immutable answer for the current context.
    /// Satisfaction and Pressure are independent evidence; this type computes
    /// no reward, attraction, or aggregate utility.
    /// </summary>
    public sealed class KLEPDesireAssessment
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPDesireAssessment(
            float satisfaction,
            float pressure,
            string explanation,
            IReadOnlyList<string> evidenceIds = null)
        {
            Satisfaction = KLEPDesireValidation.RequireSatisfaction(
                satisfaction,
                nameof(satisfaction));
            Pressure = KLEPDesireValidation.RequireNonnegativeFinite(
                pressure,
                nameof(pressure));
            Explanation = KLEPDesireValidation.RequireText(
                explanation,
                nameof(explanation));
            this.evidenceIds = KLEPDesireValidation.CopyUniqueIds(
                evidenceIds,
                nameof(evidenceIds));
        }

        public float Satisfaction { get; }
        public float Pressure { get; }
        public string Explanation { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;
    }

    /// <summary>
    /// Project-owned pure policy for one preferred condition. Implementations
    /// must return the same assessment for identical immutable context and Tick.
    /// </summary>
    public interface IKLEPDesireEvaluator<TContext>
    {
        string EvaluatorId { get; }
        string EvaluatorVersion { get; }

        KLEPDesireAssessment Evaluate(TContext context, long desireTick);
    }

    /// <summary>
    /// One immutable preferred-condition definition and its guarded project
    /// evaluator. Weight is authored importance, not a multiplier applied by
    /// this Desire slice.
    /// </summary>
    public sealed class KLEPDesireDefinition<TContext>
    {
        private readonly IKLEPDesireEvaluator<TContext> evaluator;

        public KLEPDesireDefinition(
            string stableId,
            string version,
            float weight,
            IKLEPDesireEvaluator<TContext> evaluator)
        {
            StableId = KLEPDesireValidation.RequireId(
                stableId,
                nameof(stableId));
            Version = KLEPDesireValidation.RequireId(
                version,
                nameof(version));
            Weight = KLEPDesireValidation.RequireNonnegativeFinite(
                weight,
                nameof(weight));
            this.evaluator = evaluator ??
                throw new ArgumentNullException(nameof(evaluator));
            EvaluatorId = KLEPDesireValidation.RequireId(
                evaluator.EvaluatorId,
                nameof(evaluator));
            EvaluatorVersion = KLEPDesireValidation.RequireId(
                evaluator.EvaluatorVersion,
                nameof(evaluator));
        }

        public string StableId { get; }
        public string Version { get; }
        public float Weight { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }

        internal KLEPDesireAssessment Evaluate(
            TContext context,
            long desireTick)
        {
            return evaluator.Evaluate(context, desireTick);
        }

        internal void ValidateEvaluatorContract()
        {
            if (!string.Equals(
                    evaluator.EvaluatorId,
                    EvaluatorId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evaluator.EvaluatorVersion,
                    EvaluatorVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Desire evaluator for '{StableId}' cannot change identity " +
                    "or version after registration.");
            }
        }
    }

    /// <summary>
    /// Explicit input for one atomic Desire observation. Context must be an
    /// immutable project snapshot by contract; the completed result retains
    /// only ContextIdentity.
    /// </summary>
    public sealed class KLEPDesireObservationRequest<TContext>
    {
        public KLEPDesireObservationRequest(
            string snapshotId,
            long desireTick,
            string observedMomentId,
            KLEPDesireContextIdentity contextIdentity,
            TContext context)
        {
            SnapshotId = KLEPDesireValidation.RequireId(
                snapshotId,
                nameof(snapshotId));
            if (desireTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(desireTick));
            }

            ObservedMomentId = KLEPDesireValidation.RequireId(
                observedMomentId,
                nameof(observedMomentId));
            ContextIdentity = contextIdentity ??
                throw new ArgumentNullException(nameof(contextIdentity));
            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            DesireTick = desireTick;
            Context = context;
        }

        public string SnapshotId { get; }
        public long DesireTick { get; }
        public string ObservedMomentId { get; }
        public KLEPDesireContextIdentity ContextIdentity { get; }
        public TContext Context { get; }
    }

    /// <summary>
    /// Immutable state of one Desire at one explicit observation.
    /// </summary>
    public sealed class KLEPDesireStateSnapshot
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        internal KLEPDesireStateSnapshot(
            string desireStableId,
            string desireVersion,
            string evaluatorId,
            string evaluatorVersion,
            float weight,
            KLEPDesireAssessment assessment)
        {
            DesireStableId = KLEPDesireValidation.RequireId(
                desireStableId,
                nameof(desireStableId));
            DesireVersion = KLEPDesireValidation.RequireId(
                desireVersion,
                nameof(desireVersion));
            EvaluatorId = KLEPDesireValidation.RequireId(
                evaluatorId,
                nameof(evaluatorId));
            EvaluatorVersion = KLEPDesireValidation.RequireId(
                evaluatorVersion,
                nameof(evaluatorVersion));
            Weight = KLEPDesireValidation.RequireNonnegativeFinite(
                weight,
                nameof(weight));
            if (assessment == null)
            {
                throw new ArgumentNullException(nameof(assessment));
            }

            Satisfaction = assessment.Satisfaction;
            Deficit = 1f - Satisfaction;
            Pressure = assessment.Pressure;
            Explanation = assessment.Explanation;
            evidenceIds = new ReadOnlyCollection<string>(
                new List<string>(assessment.EvidenceIds));
        }

        public string DesireStableId { get; }
        public string DesireVersion { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public float Weight { get; }
        public float Satisfaction { get; }
        public float Deficit { get; }
        public float Pressure { get; }
        public bool IsDormant => Pressure == 0f;
        public string Explanation { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;
    }

    /// <summary>
    /// Complete immutable Desire state for one observed causal moment.
    /// </summary>
    public sealed class KLEPDesireSnapshot
    {
        private readonly ReadOnlyCollection<KLEPDesireStateSnapshot> desires;

        internal KLEPDesireSnapshot(
            string ownerId,
            string definitionFingerprint,
            KLEPDesireObservationRequestMarker request,
            IReadOnlyList<KLEPDesireStateSnapshot> desires)
        {
            OwnerId = KLEPDesireValidation.RequireId(
                ownerId,
                nameof(ownerId));
            DefinitionFingerprint = KLEPDesireValidation.RequireId(
                definitionFingerprint,
                nameof(definitionFingerprint));
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            SnapshotId = request.SnapshotId;
            DesireTick = request.DesireTick;
            ObservedMomentId = request.ObservedMomentId;
            ContextIdentity = request.ContextIdentity;
            this.desires = KLEPDesireValidation.CopyDesireStates(
                desires,
                nameof(desires));
        }

        public string OwnerId { get; }
        public string DefinitionFingerprint { get; }
        public string SnapshotId { get; }
        public long DesireTick { get; }
        public string ObservedMomentId { get; }
        public KLEPDesireContextIdentity ContextIdentity { get; }
        public IReadOnlyList<KLEPDesireStateSnapshot> Desires => desires;
    }

    /// <summary>
    /// Read-only access to one Desire system's latest immutable observation.
    /// Selection policies may inspect this boundary; they cannot evaluate a
    /// new Desire moment or mutate the owning system through it.
    /// </summary>
    public interface IKLEPDesireSnapshotView
    {
        KLEPDesireSnapshot CurrentSnapshot { get; }
    }

    /// <summary>
    /// Immutable caller-owned causal provenance. Only ActionOwned requires and
    /// permits an exact action identity and positive run index.
    /// </summary>
    public sealed class KLEPDesireAttributionEvidence
    {
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution kind,
            string provenanceId,
            string actionStableId = null,
            long? actionRunIndex = null,
            IReadOnlyList<string> evidenceIds = null)
        {
            KLEPDesireValidation.RequireAttribution(kind, nameof(kind));
            ProvenanceId = KLEPDesireValidation.RequireId(
                provenanceId,
                nameof(provenanceId));

            bool hasActionId = !string.IsNullOrWhiteSpace(actionStableId);
            bool hasRunIndex = actionRunIndex.HasValue;
            if (kind == KLEPDesireEffectAttribution.ActionOwned)
            {
                if (!hasActionId)
                {
                    throw new ArgumentException(
                        "ActionOwned attribution requires an exact action stable ID.",
                        nameof(actionStableId));
                }

                if (!hasRunIndex || actionRunIndex.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(actionRunIndex),
                        "ActionOwned attribution requires a positive exact run index.");
                }
            }
            else if (hasActionId || hasRunIndex)
            {
                throw new ArgumentException(
                    "Only ActionOwned attribution may claim one exact action run.",
                    nameof(actionStableId));
            }

            Kind = kind;
            ActionStableId = hasActionId ? actionStableId : string.Empty;
            ActionRunIndex = actionRunIndex;
            this.evidenceIds = KLEPDesireValidation.CopyUniqueIds(
                evidenceIds,
                nameof(evidenceIds));
        }

        public KLEPDesireEffectAttribution Kind { get; }
        public string ProvenanceId { get; }
        public string ActionStableId { get; }
        public long? ActionRunIndex { get; }
        public IReadOnlyList<string> EvidenceIds => evidenceIds;
        public bool IsEligibleForAutomaticExpectationLearning =>
            Kind == KLEPDesireEffectAttribution.ActionOwned;
    }

    /// <summary>
    /// Explicit request to compare two already-evaluated Desire snapshots.
    /// </summary>
    public sealed class KLEPDesireTransitionRequest
    {
        public KLEPDesireTransitionRequest(
            string transitionId,
            KLEPDesireSnapshot prior,
            KLEPDesireSnapshot consequence,
            KLEPDesireAttributionEvidence attribution)
        {
            TransitionId = KLEPDesireValidation.RequireId(
                transitionId,
                nameof(transitionId));
            Prior = prior ?? throw new ArgumentNullException(nameof(prior));
            Consequence = consequence ??
                throw new ArgumentNullException(nameof(consequence));
            Attribution = attribution ??
                throw new ArgumentNullException(nameof(attribution));
        }

        public string TransitionId { get; }
        public KLEPDesireSnapshot Prior { get; }
        public KLEPDesireSnapshot Consequence { get; }
        public KLEPDesireAttributionEvidence Attribution { get; }
    }

    /// <summary>
    /// One raw per-Desire experienced effect. Effect is not multiplied by
    /// Weight or Pressure and remains separate from Emotion and lifecycle truth.
    /// </summary>
    public sealed class KLEPDesireEffectTrace
    {
        private readonly ReadOnlyCollection<string> evidenceIdsBefore;
        private readonly ReadOnlyCollection<string> evidenceIdsAfter;

        internal KLEPDesireEffectTrace(
            KLEPDesireStateSnapshot prior,
            KLEPDesireStateSnapshot consequence,
            KLEPDesireAttributionEvidence attribution)
        {
            if (prior == null)
            {
                throw new ArgumentNullException(nameof(prior));
            }

            if (consequence == null)
            {
                throw new ArgumentNullException(nameof(consequence));
            }

            Attribution = attribution ??
                throw new ArgumentNullException(nameof(attribution));
            DesireStableId = prior.DesireStableId;
            DesireVersion = prior.DesireVersion;
            EvaluatorId = prior.EvaluatorId;
            EvaluatorVersion = prior.EvaluatorVersion;
            Weight = prior.Weight;
            SatisfactionBefore = prior.Satisfaction;
            SatisfactionAfter = consequence.Satisfaction;
            DeficitBefore = prior.Deficit;
            DeficitAfter = consequence.Deficit;
            PressureBefore = prior.Pressure;
            PressureAfter = consequence.Pressure;
            ExplanationBefore = prior.Explanation;
            ExplanationAfter = consequence.Explanation;
            evidenceIdsBefore = new ReadOnlyCollection<string>(
                new List<string>(prior.EvidenceIds));
            evidenceIdsAfter = new ReadOnlyCollection<string>(
                new List<string>(consequence.EvidenceIds));
            Effect = SatisfactionAfter - SatisfactionBefore;
        }

        public string DesireStableId { get; }
        public string DesireVersion { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public float Weight { get; }
        public float SatisfactionBefore { get; }
        public float SatisfactionAfter { get; }
        public float DeficitBefore { get; }
        public float DeficitAfter { get; }
        public float PressureBefore { get; }
        public float PressureAfter { get; }
        public string ExplanationBefore { get; }
        public string ExplanationAfter { get; }
        public IReadOnlyList<string> EvidenceIdsBefore => evidenceIdsBefore;
        public IReadOnlyList<string> EvidenceIdsAfter => evidenceIdsAfter;
        public float Effect { get; }
        public KLEPDesireAttributionEvidence Attribution { get; }
        public KLEPDesireEffectAttribution AttributionKind => Attribution.Kind;
        public bool IsEligibleForAutomaticExpectationLearning =>
            Attribution.IsEligibleForAutomaticExpectationLearning;
    }

    /// <summary>
    /// Complete ordered raw effect vector for one observed transition.
    /// Deliberately exposes no aggregate utility, reward, score, or selection.
    /// </summary>
    public sealed class KLEPDesireEffectVector
    {
        private readonly ReadOnlyCollection<KLEPDesireEffectTrace> effects;

        internal KLEPDesireEffectVector(
            KLEPDesireTransitionRequest request,
            IReadOnlyList<KLEPDesireEffectTrace> effects)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            TransitionId = request.TransitionId;
            OwnerId = request.Prior.OwnerId;
            DefinitionFingerprint = request.Prior.DefinitionFingerprint;
            PriorSnapshotId = request.Prior.SnapshotId;
            ConsequenceSnapshotId = request.Consequence.SnapshotId;
            PriorDesireTick = request.Prior.DesireTick;
            ConsequenceDesireTick = request.Consequence.DesireTick;
            PriorMomentId = request.Prior.ObservedMomentId;
            ConsequenceMomentId = request.Consequence.ObservedMomentId;
            PriorContextIdentity = request.Prior.ContextIdentity;
            ConsequenceContextIdentity = request.Consequence.ContextIdentity;
            Attribution = request.Attribution;
            this.effects = KLEPDesireValidation.CopyEffects(
                effects,
                nameof(effects));
        }

        public string TransitionId { get; }
        public string OwnerId { get; }
        public string DefinitionFingerprint { get; }
        public string PriorSnapshotId { get; }
        public string ConsequenceSnapshotId { get; }
        public long PriorDesireTick { get; }
        public long ConsequenceDesireTick { get; }
        public string PriorMomentId { get; }
        public string ConsequenceMomentId { get; }
        public KLEPDesireContextIdentity PriorContextIdentity { get; }
        public KLEPDesireContextIdentity ConsequenceContextIdentity { get; }
        public KLEPDesireAttributionEvidence Attribution { get; }
        public IReadOnlyList<KLEPDesireEffectTrace> Effects => effects;
    }

    /// <summary>
    /// Portable, deterministic owner of one Agent's Desire definitions and
    /// latest evaluated state. It has no execution or selection authority.
    /// </summary>
    public sealed class KLEPDesireSystem<TContext> : IKLEPDesireSnapshotView
    {
        private readonly ReadOnlyCollection<KLEPDesireDefinition<TContext>>
            definitions;
        private bool isObserving;

        public KLEPDesireSystem(
            string ownerId,
            IReadOnlyList<KLEPDesireDefinition<TContext>> definitions)
        {
            OwnerId = KLEPDesireValidation.RequireId(ownerId, nameof(ownerId));
            this.definitions = CopyDefinitions(definitions);
            DefinitionFingerprint = BuildDefinitionFingerprint(this.definitions);
        }

        public string OwnerId { get; }
        public string DefinitionFingerprint { get; }
        public IReadOnlyList<KLEPDesireDefinition<TContext>> Definitions =>
            definitions;
        public KLEPDesireSnapshot CurrentSnapshot { get; private set; }
        public long? CurrentTick => CurrentSnapshot == null
            ? (long?)null
            : CurrentSnapshot.DesireTick;

        public KLEPDesireSnapshot Observe(
            KLEPDesireObservationRequest<TContext> request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (isObserving)
            {
                throw new InvalidOperationException(
                    "A Desire system cannot observe recursively.");
            }

            if (CurrentSnapshot != null &&
                request.DesireTick <= CurrentSnapshot.DesireTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    "Desire Tick must be strictly increasing.");
            }

            isObserving = true;
            try
            {
                var states = new List<KLEPDesireStateSnapshot>(
                    definitions.Count);
                for (int i = 0; i < definitions.Count; i++)
                {
                    KLEPDesireDefinition<TContext> definition = definitions[i];
                    definition.ValidateEvaluatorContract();
                    KLEPDesireAssessment assessment = definition.Evaluate(
                        request.Context,
                        request.DesireTick);
                    definition.ValidateEvaluatorContract();
                    if (assessment == null)
                    {
                        throw new InvalidOperationException(
                            $"Desire evaluator '{definition.EvaluatorId}' for " +
                            $"'{definition.StableId}' returned no assessment.");
                    }

                    states.Add(new KLEPDesireStateSnapshot(
                        definition.StableId,
                        definition.Version,
                        definition.EvaluatorId,
                        definition.EvaluatorVersion,
                        definition.Weight,
                        assessment));
                }

                var marker = new KLEPDesireObservationRequestMarker(
                    request.SnapshotId,
                    request.DesireTick,
                    request.ObservedMomentId,
                    request.ContextIdentity);
                var snapshot = new KLEPDesireSnapshot(
                    OwnerId,
                    DefinitionFingerprint,
                    marker,
                    states);

                CurrentSnapshot = snapshot;
                return snapshot;
            }
            finally
            {
                isObserving = false;
            }
        }

        public KLEPDesireEffectVector EvaluateTransition(
            KLEPDesireTransitionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateCompatibleSnapshot(request.Prior, nameof(request.Prior));
            ValidateCompatibleSnapshot(
                request.Consequence,
                nameof(request.Consequence));

            if (request.Prior.DesireTick >= request.Consequence.DesireTick)
            {
                throw new ArgumentException(
                    "A Desire transition requires the prior Tick to precede the consequence Tick.",
                    nameof(request));
            }

            if (string.Equals(
                    request.Prior.ObservedMomentId,
                    request.Consequence.ObservedMomentId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A Desire transition requires distinct prior and consequence moment IDs.",
                    nameof(request));
            }

            if (!string.Equals(
                    request.Prior.ContextIdentity.SchemaId,
                    request.Consequence.ContextIdentity.SchemaId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.Prior.ContextIdentity.SchemaVersion,
                    request.Consequence.ContextIdentity.SchemaVersion,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A Desire transition requires compatible context schemas.",
                    nameof(request));
            }

            var effects = new List<KLEPDesireEffectTrace>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                KLEPDesireStateSnapshot prior = request.Prior.Desires[i];
                KLEPDesireStateSnapshot consequence =
                    request.Consequence.Desires[i];
                RequireMatchingDesireStates(prior, consequence, nameof(request));
                effects.Add(new KLEPDesireEffectTrace(
                    prior,
                    consequence,
                    request.Attribution));
            }

            return new KLEPDesireEffectVector(request, effects);
        }

        private void ValidateCompatibleSnapshot(
            KLEPDesireSnapshot snapshot,
            string parameterName)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!string.Equals(snapshot.OwnerId, OwnerId, StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.DefinitionFingerprint,
                    DefinitionFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The Desire snapshot belongs to another owner or definition set.",
                    parameterName);
            }

            if (snapshot.Desires.Count != definitions.Count)
            {
                throw new ArgumentException(
                    "The Desire snapshot does not contain the complete definition set.",
                    parameterName);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                KLEPDesireDefinition<TContext> definition = definitions[i];
                KLEPDesireStateSnapshot state = snapshot.Desires[i];
                if (!string.Equals(
                        state.DesireStableId,
                        definition.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        state.DesireVersion,
                        definition.Version,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        state.EvaluatorId,
                        definition.EvaluatorId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        state.EvaluatorVersion,
                        definition.EvaluatorVersion,
                        StringComparison.Ordinal) ||
                    !state.Weight.Equals(definition.Weight))
                {
                    throw new ArgumentException(
                        "The Desire snapshot definition evidence is incompatible.",
                        parameterName);
                }
            }
        }

        private static void RequireMatchingDesireStates(
            KLEPDesireStateSnapshot prior,
            KLEPDesireStateSnapshot consequence,
            string parameterName)
        {
            if (!string.Equals(
                    prior.DesireStableId,
                    consequence.DesireStableId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    prior.DesireVersion,
                    consequence.DesireVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    prior.EvaluatorId,
                    consequence.EvaluatorId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    prior.EvaluatorVersion,
                    consequence.EvaluatorVersion,
                    StringComparison.Ordinal) ||
                !prior.Weight.Equals(consequence.Weight))
            {
                throw new ArgumentException(
                    "A Desire changed identity, evaluator, version, or authored weight across the transition.",
                    parameterName);
            }
        }

        private static ReadOnlyCollection<KLEPDesireDefinition<TContext>>
            CopyDefinitions(
                IReadOnlyList<KLEPDesireDefinition<TContext>> source)
        {
            var copy = new List<KLEPDesireDefinition<TContext>>();
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    KLEPDesireDefinition<TContext> definition = source[i];
                    if (definition == null)
                    {
                        throw new ArgumentException(
                            "Desire definitions cannot contain null.",
                            nameof(source));
                    }

                    if (!stableIds.Add(definition.StableId))
                    {
                        throw new ArgumentException(
                            $"Desire definition ID '{definition.StableId}' occurs more than once.",
                            nameof(source));
                    }

                    copy.Add(definition);
                }
            }

            return new ReadOnlyCollection<KLEPDesireDefinition<TContext>>(copy);
        }

        private static string BuildDefinitionFingerprint(
            IReadOnlyList<KLEPDesireDefinition<TContext>> source)
        {
            var builder = new StringBuilder("klep.desire.definitions.v1");
            builder.Append('|').Append(source.Count.ToString(
                CultureInfo.InvariantCulture));
            for (int i = 0; i < source.Count; i++)
            {
                KLEPDesireDefinition<TContext> definition = source[i];
                builder.Append('|').Append(i.ToString(CultureInfo.InvariantCulture));
                AppendToken(builder, definition.StableId);
                AppendToken(builder, definition.Version);
                AppendToken(builder, definition.EvaluatorId);
                AppendToken(builder, definition.EvaluatorVersion);
                AppendToken(
                    builder,
                    definition.Weight.ToString("R", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            builder.Append('|')
                .Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }
    }

    /// <summary>
    /// Non-generic immutable request data used only while constructing a public
    /// snapshot, ensuring the transient project context cannot be retained.
    /// </summary>
    internal sealed class KLEPDesireObservationRequestMarker
    {
        internal KLEPDesireObservationRequestMarker(
            string snapshotId,
            long desireTick,
            string observedMomentId,
            KLEPDesireContextIdentity contextIdentity)
        {
            SnapshotId = snapshotId;
            DesireTick = desireTick;
            ObservedMomentId = observedMomentId;
            ContextIdentity = contextIdentity;
        }

        internal string SnapshotId { get; }
        internal long DesireTick { get; }
        internal string ObservedMomentId { get; }
        internal KLEPDesireContextIdentity ContextIdentity { get; }
    }

    internal static class KLEPDesireValidation
    {
        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable identity is required.",
                    parameterName);
            }

            return value;
        }

        internal static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "An inspectable explanation is required.",
                    parameterName);
            }

            return value;
        }

        internal static float RequireSatisfaction(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < 0f ||
                value > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Desire satisfaction must be finite and within [0, 1].");
            }

            return value == 0f ? 0f : value;
        }

        internal static float RequireNonnegativeFinite(
            float value,
            string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Desire weight and pressure must be finite and nonnegative.");
            }

            return value == 0f ? 0f : value;
        }

        internal static void RequireAttribution(
            KLEPDesireEffectAttribution value,
            string parameterName)
        {
            if (value != KLEPDesireEffectAttribution.ActionOwned &&
                value != KLEPDesireEffectAttribution.External &&
                value != KLEPDesireEffectAttribution.Mixed &&
                value != KLEPDesireEffectAttribution.Unknown)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        internal static ReadOnlyCollection<string> CopyUniqueIds(
            IReadOnlyList<string> source,
            string parameterName)
        {
            if (source == null)
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            var copy = new List<string>(source.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                string id = RequireId(source[i], parameterName);
                if (!seen.Add(id))
                {
                    throw new ArgumentException(
                        $"Evidence ID '{id}' occurs more than once.",
                        parameterName);
                }

                copy.Add(id);
            }

            return new ReadOnlyCollection<string>(copy);
        }

        internal static ReadOnlyCollection<KLEPDesireStateSnapshot>
            CopyDesireStates(
                IReadOnlyList<KLEPDesireStateSnapshot> source,
                string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<KLEPDesireStateSnapshot>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i] ?? throw new ArgumentException(
                    "Desire snapshots cannot contain null states.",
                    parameterName));
            }

            return new ReadOnlyCollection<KLEPDesireStateSnapshot>(copy);
        }

        internal static ReadOnlyCollection<KLEPDesireEffectTrace> CopyEffects(
            IReadOnlyList<KLEPDesireEffectTrace> source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<KLEPDesireEffectTrace>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i] ?? throw new ArgumentException(
                    "Desire effect vectors cannot contain null traces.",
                    parameterName));
            }

            return new ReadOnlyCollection<KLEPDesireEffectTrace>(copy);
        }
    }
}
