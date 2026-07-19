using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Roll4d4.Klep.Emotion;

namespace Roll4d4.Klep.Ethics
{
    /// <summary>
    /// Immutable identity for the project-owned context snapshot that was
    /// evaluated. KLEP retains this identity in the completed evaluation, not
    /// the arbitrary live TContext reference.
    /// </summary>
    public sealed class KLEPEthicsContextIdentity
    {
        public KLEPEthicsContextIdentity(
            string contextId,
            string schemaId,
            string schemaVersion)
        {
            ContextId = KLEPEthicsValidation.RequireId(
                contextId,
                nameof(contextId));
            SchemaId = KLEPEthicsValidation.RequireId(
                schemaId,
                nameof(schemaId));
            SchemaVersion = KLEPEthicsValidation.RequireId(
                schemaVersion,
                nameof(schemaVersion));
        }

        public string ContextId { get; }
        public string SchemaId { get; }
        public string SchemaVersion { get; }
    }

    /// <summary>
    /// Request to evaluate one project-owned context snapshot. EvaluationId
    /// identifies this exact appraisal and becomes the Emotion influence source
    /// ID. It must therefore be unique within the target Emotion Tick. TContext
    /// itself is transient evaluator input and must be immutable by project
    /// contract; the completed evaluation retains only ContextIdentity.
    /// </summary>
    public readonly struct KLEPEthicsRequest<TContext>
    {
        public KLEPEthicsRequest(
            string evaluationId,
            long evaluationTick,
            KLEPEmotionInfluenceOrigin causeOrigin,
            KLEPEmotionConfiguration emotionConfiguration,
            KLEPEthicsContextIdentity contextIdentity,
            TContext context)
        {
            EvaluationId = KLEPEthicsValidation.RequireId(
                evaluationId,
                nameof(evaluationId));

            if (evaluationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationTick));
            }

            KLEPEthicsValidation.RequireOrigin(causeOrigin, nameof(causeOrigin));
            if (emotionConfiguration == null)
            {
                throw new ArgumentNullException(nameof(emotionConfiguration));
            }

            if (contextIdentity == null)
            {
                throw new ArgumentNullException(nameof(contextIdentity));
            }

            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            EvaluationTick = evaluationTick;
            CauseOrigin = causeOrigin;
            EmotionConfiguration = emotionConfiguration;
            ContextIdentity = contextIdentity;
            Context = context;
        }

        public string EvaluationId { get; }
        public long EvaluationTick { get; }
        public KLEPEmotionInfluenceOrigin CauseOrigin { get; }
        public KLEPEmotionConfiguration EmotionConfiguration { get; }
        public KLEPEthicsContextIdentity ContextIdentity { get; }
        public TContext Context { get; }

        internal void Validate()
        {
            KLEPEthicsValidation.RequireId(EvaluationId, nameof(EvaluationId));
            if (EvaluationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(EvaluationTick));
            }

            KLEPEthicsValidation.RequireOrigin(CauseOrigin, nameof(CauseOrigin));
            if (EmotionConfiguration == null)
            {
                throw new ArgumentNullException(nameof(EmotionConfiguration));
            }

            if (ContextIdentity == null)
            {
                throw new ArgumentNullException(nameof(ContextIdentity));
            }

            if (ReferenceEquals(Context, null))
            {
                throw new ArgumentNullException(nameof(Context));
            }
        }
    }

    /// <summary>
    /// One inspectable step in an Ethics judgment. An unapplied step preserves
    /// what it proposed but contributes zero. Applied contribution is the
    /// proposed normalized impulse multiplied by a finite [0, 1] weight.
    /// </summary>
    public sealed class KLEPEthicsTraceEntry
    {
        public KLEPEthicsTraceEntry(
            string sourceId,
            bool applied,
            float weight,
            KLEPEmotionVector proposedImpulse,
            string reasonCode,
            IReadOnlyList<string> evidenceIds = null)
        {
            SourceId = KLEPEthicsValidation.RequireId(
                sourceId,
                nameof(sourceId));
            ReasonCode = KLEPEthicsValidation.RequireId(
                reasonCode,
                nameof(reasonCode));
            Weight = KLEPEthicsValidation.RequireWeight(weight, nameof(weight));
            Applied = applied;
            ProposedImpulse = proposedImpulse;
            EvidenceIds = KLEPEthicsValidation.CopyUniqueIds(
                evidenceIds,
                nameof(evidenceIds));
            ContributionX = applied ? (double)proposedImpulse.X * weight : 0d;
            ContributionY = applied ? (double)proposedImpulse.Y * weight : 0d;
        }

        public string SourceId { get; }
        public bool Applied { get; }
        public float Weight { get; }
        public KLEPEmotionVector ProposedImpulse { get; }
        public string ReasonCode { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
        public double ContributionX { get; }
        public double ContributionY { get; }
    }

    /// <summary>
    /// An evaluator's immutable, completely traced judgment. Raw totals are
    /// derived from the ordered trace, then clamped only once into the target
    /// normalized Emotion vector.
    /// </summary>
    public sealed class KLEPEthicsJudgment
    {
        private readonly ReadOnlyCollection<KLEPEthicsTraceEntry> trace;

        public KLEPEthicsJudgment(
            IReadOnlyList<KLEPEthicsTraceEntry> trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (trace.Count == 0)
            {
                throw new ArgumentException(
                    "An Ethics judgment requires at least one inspectable trace entry.",
                    nameof(trace));
            }

            var copied = new List<KLEPEthicsTraceEntry>(trace.Count);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            double rawX = 0d;
            double rawY = 0d;

            for (int i = 0; i < trace.Count; i++)
            {
                KLEPEthicsTraceEntry entry = trace[i];
                if (entry == null)
                {
                    throw new ArgumentException(
                        "An Ethics trace cannot contain null entries.",
                        nameof(trace));
                }

                if (!sourceIds.Add(entry.SourceId))
                {
                    throw new ArgumentException(
                        $"Ethics trace source '{entry.SourceId}' occurs more than once.",
                        nameof(trace));
                }

                rawX += entry.ContributionX;
                rawY += entry.ContributionY;
                if (double.IsNaN(rawX) ||
                    double.IsInfinity(rawX) ||
                    double.IsNaN(rawY) ||
                    double.IsInfinity(rawY))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(trace),
                        "The Ethics trace must produce finite raw totals.");
                }

                copied.Add(entry);
            }

            RawX = rawX;
            RawY = rawY;
            WasClamped = rawX < -1d || rawX > 1d || rawY < -1d || rawY > 1d;
            Impulse = new KLEPEmotionVector(
                KLEPEthicsValidation.ClampAxis(rawX),
                KLEPEthicsValidation.ClampAxis(rawY));
            this.trace = new ReadOnlyCollection<KLEPEthicsTraceEntry>(copied);
        }

        public IReadOnlyList<KLEPEthicsTraceEntry> Trace => trace;
        public double RawX { get; }
        public double RawY { get; }
        public bool WasClamped { get; }
        public KLEPEmotionVector Impulse { get; }
    }

    /// <summary>
    /// Project-owned Ethics policy. Implement this interface to replace the
    /// supplied weighted evaluator without changing the guarded KLEP envelope.
    /// Implementations must be pure for identical context, Tick, and Emotion
    /// configuration.
    /// </summary>
    public interface IKLEPEthicsEvaluator<TContext>
    {
        string EvaluatorId { get; }
        string EvaluatorVersion { get; }
        string ExpectedAxisXName { get; }
        string ExpectedAxisYName { get; }

        KLEPEthicsJudgment Evaluate(
            TContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration);
    }

    /// <summary>
    /// Guarded result containing evaluator identity and the caller-owned
    /// provenance that evaluator code is not allowed to replace.
    /// </summary>
    public sealed class KLEPEthicsEvaluation<TContext>
    {
        internal KLEPEthicsEvaluation(
            KLEPEthicsRequest<TContext> request,
            string evaluatorId,
            string evaluatorVersion,
            KLEPEthicsJudgment judgment)
        {
            EvaluationId = request.EvaluationId;
            EvaluationTick = request.EvaluationTick;
            CauseOrigin = request.CauseOrigin;
            EmotionConfiguration = request.EmotionConfiguration;
            ContextIdentity = request.ContextIdentity;
            EvaluatorId = evaluatorId;
            EvaluatorVersion = evaluatorVersion;
            Judgment = judgment ?? throw new ArgumentNullException(nameof(judgment));
            Influence = new KLEPEmotionInfluence(
                request.EvaluationId,
                request.CauseOrigin,
                judgment.Impulse);
        }

        public string EvaluationId { get; }
        public long EvaluationTick { get; }
        public KLEPEmotionInfluenceOrigin CauseOrigin { get; }
        public KLEPEmotionConfiguration EmotionConfiguration { get; }
        public KLEPEthicsContextIdentity ContextIdentity { get; }
        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public KLEPEthicsJudgment Judgment { get; }
        public KLEPEmotionInfluence Influence { get; }
    }

    /// <summary>
    /// Portable Ethics boundary. It validates one project evaluator and wraps
    /// every judgment into an Emotion influence using caller-owned provenance.
    /// It does not advance Emotion or inspect KLEP behavior selection.
    /// </summary>
    public sealed class KLEPEthics<TContext>
    {
        private readonly IKLEPEthicsEvaluator<TContext> evaluator;

        public KLEPEthics(IKLEPEthicsEvaluator<TContext> evaluator)
        {
            this.evaluator = evaluator ??
                throw new ArgumentNullException(nameof(evaluator));
            EvaluatorId = KLEPEthicsValidation.RequireId(
                evaluator.EvaluatorId,
                nameof(evaluator));
            EvaluatorVersion = KLEPEthicsValidation.RequireId(
                evaluator.EvaluatorVersion,
                nameof(evaluator));
            ExpectedAxisXName = KLEPEthicsValidation.RequireId(
                evaluator.ExpectedAxisXName,
                nameof(evaluator));
            ExpectedAxisYName = KLEPEthicsValidation.RequireId(
                evaluator.ExpectedAxisYName,
                nameof(evaluator));
            if (string.Equals(
                    ExpectedAxisXName,
                    ExpectedAxisYName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An Ethics evaluator requires distinct expected axis names.",
                    nameof(evaluator));
            }
        }

        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public string ExpectedAxisXName { get; }
        public string ExpectedAxisYName { get; }

        public KLEPEthicsEvaluation<TContext> Evaluate(
            KLEPEthicsRequest<TContext> request)
        {
            request.Validate();
            ValidateEvaluatorContract();
            KLEPEthicsValidation.RequireAxes(
                request.EmotionConfiguration,
                ExpectedAxisXName,
                ExpectedAxisYName,
                EvaluatorId);

            KLEPEthicsJudgment judgment = evaluator.Evaluate(
                request.Context,
                request.EvaluationTick,
                request.EmotionConfiguration);
            ValidateEvaluatorContract();
            if (judgment == null)
            {
                throw new InvalidOperationException(
                    $"Ethics evaluator '{EvaluatorId}' returned no judgment.");
            }

            return new KLEPEthicsEvaluation<TContext>(
                request,
                EvaluatorId,
                EvaluatorVersion,
                judgment);
        }

        private void ValidateEvaluatorContract()
        {
            if (!string.Equals(
                    evaluator.EvaluatorId,
                    EvaluatorId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evaluator.EvaluatorVersion,
                    EvaluatorVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evaluator.ExpectedAxisXName,
                    ExpectedAxisXName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evaluator.ExpectedAxisYName,
                    ExpectedAxisYName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "An Ethics evaluator cannot change identity, version, or expected axes after registration.");
            }
        }
    }

    /// <summary>
    /// Result from one simple weighted rule. Unapplied rules remain visible in
    /// the evaluator trace and contribute zero.
    /// </summary>
    public sealed class KLEPEthicsRuleMatch
    {
        public KLEPEthicsRuleMatch(
            bool applied,
            KLEPEmotionVector proposedImpulse,
            string reasonCode,
            IReadOnlyList<string> evidenceIds = null)
        {
            Applied = applied;
            ProposedImpulse = proposedImpulse;
            ReasonCode = KLEPEthicsValidation.RequireId(
                reasonCode,
                nameof(reasonCode));
            EvidenceIds = KLEPEthicsValidation.CopyUniqueIds(
                evidenceIds,
                nameof(evidenceIds));
        }

        public bool Applied { get; }
        public KLEPEmotionVector ProposedImpulse { get; }
        public string ReasonCode { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
    }

    /// <summary>
    /// Optional rule seam used by the built-in weighted evaluator. A rule and
    /// anything it captures must remain pure and stable after registration.
    /// </summary>
    public interface IKLEPWeightedEthicsRule<TContext>
    {
        string RuleId { get; }
        float Weight { get; }
        KLEPEthicsRuleMatch Evaluate(TContext context);
    }

    /// <summary>
    /// Small springboard rule: when its project predicate matches, it proposes
    /// one fixed impulse. An always-true predicate acts as a simple bias rule.
    /// The predicate may capture only immutable project data; hidden clock,
    /// random, or mutable closure state violates the deterministic contract.
    /// </summary>
    public sealed class KLEPWeightedEthicsRule<TContext> :
        IKLEPWeightedEthicsRule<TContext>
    {
        private readonly Func<TContext, bool> predicate;
        private readonly KLEPEmotionVector proposedImpulse;
        private readonly string appliedReasonCode;
        private readonly string notAppliedReasonCode;
        private readonly ReadOnlyCollection<string> evidenceIds;

        public KLEPWeightedEthicsRule(
            string ruleId,
            float weight,
            Func<TContext, bool> predicate,
            KLEPEmotionVector proposedImpulse,
            string appliedReasonCode,
            string notAppliedReasonCode = "rule.not-applied",
            IReadOnlyList<string> evidenceIds = null)
        {
            RuleId = KLEPEthicsValidation.RequireId(ruleId, nameof(ruleId));
            Weight = KLEPEthicsValidation.RequireWeight(weight, nameof(weight));
            this.predicate = predicate ??
                throw new ArgumentNullException(nameof(predicate));
            this.proposedImpulse = proposedImpulse;
            this.appliedReasonCode = KLEPEthicsValidation.RequireId(
                appliedReasonCode,
                nameof(appliedReasonCode));
            this.notAppliedReasonCode = KLEPEthicsValidation.RequireId(
                notAppliedReasonCode,
                nameof(notAppliedReasonCode));
            this.evidenceIds = KLEPEthicsValidation.CopyUniqueIds(
                evidenceIds,
                nameof(evidenceIds));
        }

        public string RuleId { get; }
        public float Weight { get; }

        public KLEPEthicsRuleMatch Evaluate(TContext context)
        {
            bool applied = predicate(context);
            return new KLEPEthicsRuleMatch(
                applied,
                proposedImpulse,
                applied ? appliedReasonCode : notAppliedReasonCode,
                evidenceIds);
        }
    }

    /// <summary>
    /// Reference implementation for projects that want simple ordered weighted
    /// rules and a bias. It is deterministic when project rules obey their
    /// purity contract. Projects may replace it by directly implementing
    /// IKLEPEthicsEvaluator.
    /// </summary>
    public sealed class KLEPWeightedEthicsEvaluator<TContext> :
        IKLEPEthicsEvaluator<TContext>
    {
        private readonly ReadOnlyCollection<RuleBinding> rules;

        public KLEPWeightedEthicsEvaluator(
            string evaluatorId,
            string evaluatorVersion,
            string expectedAxisXName,
            string expectedAxisYName,
            KLEPEmotionVector bias,
            IReadOnlyList<IKLEPWeightedEthicsRule<TContext>> rules = null)
        {
            EvaluatorId = KLEPEthicsValidation.RequireId(
                evaluatorId,
                nameof(evaluatorId));
            EvaluatorVersion = KLEPEthicsValidation.RequireId(
                evaluatorVersion,
                nameof(evaluatorVersion));
            ExpectedAxisXName = KLEPEthicsValidation.RequireId(
                expectedAxisXName,
                nameof(expectedAxisXName));
            ExpectedAxisYName = KLEPEthicsValidation.RequireId(
                expectedAxisYName,
                nameof(expectedAxisYName));

            if (string.Equals(
                    ExpectedAxisXName,
                    ExpectedAxisYName,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An Ethics evaluator requires distinct expected axis names.",
                    nameof(expectedAxisYName));
            }

            Bias = bias;
            this.rules = CopyRules(rules);
        }

        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }
        public string ExpectedAxisXName { get; }
        public string ExpectedAxisYName { get; }
        public KLEPEmotionVector Bias { get; }
        public int RuleCount => rules.Count;

        public KLEPEthicsJudgment Evaluate(
            TContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration)
        {
            if (ReferenceEquals(context, null))
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (evaluationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationTick));
            }

            if (emotionConfiguration == null)
            {
                throw new ArgumentNullException(nameof(emotionConfiguration));
            }

            KLEPEthicsValidation.RequireAxes(
                emotionConfiguration,
                ExpectedAxisXName,
                ExpectedAxisYName,
                EvaluatorId);

            var trace = new List<KLEPEthicsTraceEntry>(rules.Count + 1)
            {
                new KLEPEthicsTraceEntry(
                    "bias",
                    applied: true,
                    weight: 1f,
                    proposedImpulse: Bias,
                    reasonCode: "weighted.bias")
            };

            for (int i = 0; i < rules.Count; i++)
            {
                RuleBinding binding = rules[i];
                KLEPEthicsRuleMatch match = binding.Rule.Evaluate(context);
                if (match == null)
                {
                    throw new InvalidOperationException(
                        $"Ethics rule '{binding.RuleId}' returned no match result.");
                }

                trace.Add(new KLEPEthicsTraceEntry(
                    $"rule:{binding.RuleId}",
                    match.Applied,
                    binding.Weight,
                    match.ProposedImpulse,
                    match.ReasonCode,
                    match.EvidenceIds));
            }

            return new KLEPEthicsJudgment(trace);
        }

        private static ReadOnlyCollection<RuleBinding> CopyRules(
            IReadOnlyList<IKLEPWeightedEthicsRule<TContext>> source)
        {
            if (source == null)
            {
                return new ReadOnlyCollection<RuleBinding>(
                    Array.Empty<RuleBinding>());
            }

            var copied = new List<RuleBinding>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                IKLEPWeightedEthicsRule<TContext> rule = source[i];
                if (rule == null)
                {
                    throw new ArgumentException(
                        "An Ethics rule list cannot contain null entries.",
                        nameof(source));
                }

                string ruleId = KLEPEthicsValidation.RequireId(
                    rule.RuleId,
                    nameof(source));
                if (!ids.Add(ruleId))
                {
                    throw new ArgumentException(
                        $"Ethics rule ID '{ruleId}' occurs more than once.",
                        nameof(source));
                }

                float weight = KLEPEthicsValidation.RequireWeight(
                    rule.Weight,
                    nameof(source));
                copied.Add(new RuleBinding(ruleId, weight, rule));
            }

            return new ReadOnlyCollection<RuleBinding>(copied);
        }

        private sealed class RuleBinding
        {
            internal RuleBinding(
                string ruleId,
                float weight,
                IKLEPWeightedEthicsRule<TContext> rule)
            {
                RuleId = ruleId;
                Weight = weight;
                Rule = rule;
            }

            internal string RuleId { get; }
            internal float Weight { get; }
            internal IKLEPWeightedEthicsRule<TContext> Rule { get; }
        }
    }

    internal static class KLEPEthicsValidation
    {
        internal static ReadOnlyCollection<string> CopyUniqueIds(
            IReadOnlyList<string> source,
            string parameterName)
        {
            if (source == null)
            {
                return new ReadOnlyCollection<string>(Array.Empty<string>());
            }

            var copied = new List<string>(source.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                string id = RequireId(source[i], parameterName);
                if (!ids.Add(id))
                {
                    throw new ArgumentException(
                        $"Evidence ID '{id}' occurs more than once.",
                        parameterName);
                }

                copied.Add(id);
            }

            return new ReadOnlyCollection<string>(copied);
        }

        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Ethics identities and reason codes cannot be empty.",
                    parameterName);
            }

            return value;
        }

        internal static float RequireWeight(float value, string parameterName)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < 0f ||
                value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        internal static void RequireOrigin(
            KLEPEmotionInfluenceOrigin origin,
            string parameterName)
        {
            if (origin != KLEPEmotionInfluenceOrigin.Internal &&
                origin != KLEPEmotionInfluenceOrigin.External)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        internal static void RequireAxes(
            KLEPEmotionConfiguration configuration,
            string expectedAxisXName,
            string expectedAxisYName,
            string evaluatorId)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (!string.Equals(
                    configuration.AxisXName,
                    expectedAxisXName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    configuration.AxisYName,
                    expectedAxisYName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Ethics evaluator '{evaluatorId}' expects axes " +
                    $"'{expectedAxisXName}'/'{expectedAxisYName}', but received " +
                    $"'{configuration.AxisXName}'/'{configuration.AxisYName}'.");
            }
        }

        internal static float ClampAxis(double value)
        {
            if (value <= -1d)
            {
                return -1f;
            }

            if (value >= 1d)
            {
                return 1f;
            }

            return (float)value;
        }
    }
}
