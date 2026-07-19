using System;
using System.Collections.Generic;
using Roll4d4.Klep.Emotion;
using Roll4d4.Klep.Ethics;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyWeightedBiasOnlyEvaluation();
        VerifySameActionCanReceiveDifferentContextualMeaning();
        VerifyEveryRuleRunsOnceInAuthoredOrder();
        VerifyUnappliedAndZeroWeightRulesRemainVisible();
        VerifyRawTotalsClampingAndCancellationAreInspectable();
        VerifyCustomEvaluatorUsesTheGuardedEnvelope();
        VerifyAxisMismatchFailsBeforeEvaluation();
        VerifyCustomAxisContractIsGuarded();
        VerifyEvaluatorDriftDuringCallbackIsRejected();
        VerifyPublishedEvaluationDoesNotRetainLiveContext();
        VerifyEvidenceReferencesAreImmutable();
        VerifyEvaluationFeedsEmotionWithoutAdvancingIt();
        VerifyInvalidConfigurationAndResultsAreRejected();
        VerifyTraceAndRuleInputsAreDefensivelyCopied();
        VerifyEquivalentEvaluationsAreDeterministic();

        Console.WriteLine($"KLEP Ethics smoke passed: {assertions} assertions.");
    }

    private static void VerifyWeightedBiasOnlyEvaluation()
    {
        KLEPEmotionConfiguration emotionConfiguration = Configuration();
        var evaluator = new KLEPWeightedEthicsEvaluator<ScenarioContext>(
            "ethics.weighted",
            "1.0.0",
            "Valence",
            "Activation",
            new KLEPEmotionVector(0.2f, -0.1f));
        var ethics = new KLEPEthics<ScenarioContext>(evaluator);
        KLEPEthicsEvaluation<ScenarioContext> evaluation = ethics.Evaluate(
            Request("evaluation.bias", new ScenarioContext("act.wait"),
                emotionConfiguration));

        Expect(evaluation.Judgment.Impulse ==
               new KLEPEmotionVector(0.2f, -0.1f),
            "A project may begin with only a small weighted Ethics bias");
        Expect(evaluation.Judgment.Trace.Count == 1 &&
               evaluation.Judgment.Trace[0].SourceId == "bias" &&
               evaluation.Judgment.Trace[0].Applied,
            "Bias-only evaluation remains explicitly traced");
        Expect(evaluation.EvaluatorId == "ethics.weighted" &&
               evaluation.EvaluatorVersion == "1.0.0",
            "An Ethics result preserves evaluator identity and version");
    }

    private static void VerifySameActionCanReceiveDifferentContextualMeaning()
    {
        var protectRule = new KLEPWeightedEthicsRule<ScenarioContext>(
            "context.protected",
            1f,
            context => context.WasProtected,
            new KLEPEmotionVector(1f, 0f),
            "context.protected.applied");
        var harmedRule = new KLEPWeightedEthicsRule<ScenarioContext>(
            "context.harmed",
            1f,
            context => context.WasHarmed,
            new KLEPEmotionVector(-1f, 0f),
            "context.harmed.applied");
        KLEPEthics<ScenarioContext> ethics = Weighted(
            protectRule,
            harmedRule);

        var protectedContext = new ScenarioContext(
            "act.same-physical-action",
            wasProtected: true);
        var harmedContext = new ScenarioContext(
            "act.same-physical-action",
            wasHarmed: true);

        KLEPEthicsEvaluation<ScenarioContext> protectedEvaluation =
            ethics.Evaluate(Request("evaluation.protected", protectedContext));
        KLEPEthicsEvaluation<ScenarioContext> harmedEvaluation =
            ethics.Evaluate(Request("evaluation.harmed", harmedContext));

        Expect(protectedContext.ActionId == harmedContext.ActionId,
            "The two Ethics contexts may describe the same physical action");
        Expect(protectedEvaluation.Judgment.Impulse ==
                   new KLEPEmotionVector(1f, 0f) &&
               harmedEvaluation.Judgment.Impulse ==
                   new KLEPEmotionVector(-1f, 0f),
            "Contextual evaluation, not the action name, supplies emotional meaning");
    }

    private static void VerifyEveryRuleRunsOnceInAuthoredOrder()
    {
        var calls = new List<string>();
        var first = new ProbeRule("z.authored-first", calls, 0.2f);
        var second = new ProbeRule("a.authored-second", calls, 0.3f);
        KLEPEthics<ScenarioContext> ethics = Weighted(first, second);

        KLEPEthicsEvaluation<ScenarioContext> evaluation = ethics.Evaluate(
            Request("evaluation.order", new ScenarioContext("act.order")));

        Expect(calls.Count == 2 &&
               calls[0] == "z.authored-first" &&
               calls[1] == "a.authored-second",
            "Weighted Ethics evaluates every rule once in authored order");
        Expect(evaluation.Judgment.Trace[1].SourceId ==
                   "rule:z.authored-first" &&
               evaluation.Judgment.Trace[2].SourceId ==
                   "rule:a.authored-second",
            "The immutable trace preserves authored rule order");
    }

    private static void VerifyUnappliedAndZeroWeightRulesRemainVisible()
    {
        var never = new KLEPWeightedEthicsRule<ScenarioContext>(
            "never",
            1f,
            context => false,
            new KLEPEmotionVector(-1f, 0f),
            "never.applied",
            "never.not-applied");
        var zeroWeight = new KLEPWeightedEthicsRule<ScenarioContext>(
            "zero-weight",
            0f,
            context => true,
            new KLEPEmotionVector(1f, 1f),
            "zero.applied");
        KLEPEthicsEvaluation<ScenarioContext> evaluation = Weighted(
            never,
            zeroWeight).Evaluate(
                Request("evaluation.visible-zero",
                    new ScenarioContext("act.none")));

        KLEPEthicsTraceEntry neverTrace = evaluation.Judgment.Trace[1];
        KLEPEthicsTraceEntry zeroTrace = evaluation.Judgment.Trace[2];
        Expect(!neverTrace.Applied &&
               neverTrace.ContributionX == 0d &&
               neverTrace.ReasonCode == "never.not-applied",
            "An unapplied ethical rule remains inspectable and contributes zero");
        Expect(zeroTrace.Applied && zeroTrace.Weight == 0f &&
               zeroTrace.ContributionX == 0d &&
               zeroTrace.ContributionY == 0d,
            "A zero-weight rule records that it applied without changing the result");
    }

    private static void VerifyRawTotalsClampingAndCancellationAreInspectable()
    {
        var positiveA = AlwaysRule("positive-a", 1f, 1f, 0f);
        var positiveB = AlwaysRule("positive-b", 1f, 1f, 0f);
        KLEPEthicsEvaluation<ScenarioContext> clamped = Weighted(
            positiveA,
            positiveB).Evaluate(
                Request("evaluation.clamped", new ScenarioContext("act.stack")));

        Expect(clamped.Judgment.RawX == 2d &&
               clamped.Judgment.Impulse.X == 1f &&
               clamped.Judgment.WasClamped,
            "Ethics preserves saturated raw totals while bounding Emotion output");

        var negative = AlwaysRule("negative", 1f, -1f, 0f);
        KLEPEthicsEvaluation<ScenarioContext> cancelled = Weighted(
            positiveA,
            negative).Evaluate(
                Request("evaluation.cancelled", new ScenarioContext("act.conflict")));
        Expect(cancelled.Judgment.RawX == 0d &&
               cancelled.Judgment.Impulse == KLEPEmotionVector.Zero &&
               !cancelled.Judgment.WasClamped,
            "Conflicting ethical contributions remain visible even when net influence is neutral");
    }

    private static void VerifyCustomEvaluatorUsesTheGuardedEnvelope()
    {
        var custom = new CustomEvaluator();
        var ethics = new KLEPEthics<ScenarioContext>(custom);
        KLEPEthicsRequest<ScenarioContext> request = new KLEPEthicsRequest<ScenarioContext>(
            "evaluation.custom",
            7,
            KLEPEmotionInfluenceOrigin.External,
            Configuration(),
            ContextIdentity("context.custom"),
            new ScenarioContext("act.custom"));
        KLEPEthicsEvaluation<ScenarioContext> evaluation = ethics.Evaluate(request);

        Expect(evaluation.Judgment.Impulse ==
               new KLEPEmotionVector(0.25f, 0.5f),
            "A project may replace weighted rules with a custom evaluator");
        Expect(evaluation.Influence.SourceId == "evaluation.custom" &&
               evaluation.Influence.Origin ==
                   KLEPEmotionInfluenceOrigin.External,
            "The guarded wrapper, not custom evaluator code, owns final influence provenance");
        Expect(evaluation.EvaluationTick == 7 &&
               evaluation.EvaluatorVersion == "custom-v2",
            "Custom evaluation retains caller Tick and evaluator version");
    }

    private static void VerifyAxisMismatchFailsBeforeEvaluation()
    {
        var calls = new List<string>();
        var rule = new ProbeRule("should-not-run", calls, 1f);
        KLEPEthics<ScenarioContext> ethics = Weighted(rule);
        var wrongAxes = new KLEPEmotionConfiguration("Trust", "Fear");

        Exception mismatch = Catch(() => ethics.Evaluate(
            Request(
                "evaluation.wrong-axes",
                new ScenarioContext("act.axes"),
                wrongAxes)));
        Expect(mismatch is InvalidOperationException && calls.Count == 0,
            "An Ethics model rejects silent reinterpretation on different Emotion axes");
    }

    private static void VerifyCustomAxisContractIsGuarded()
    {
        var evaluator = new CustomEvaluator();
        var ethics = new KLEPEthics<ScenarioContext>(evaluator);
        var wrongAxes = new KLEPEmotionConfiguration("Trust", "Fear");

        Exception mismatch = Catch(() => ethics.Evaluate(
            Request(
                "evaluation.custom-wrong-axes",
                new ScenarioContext("act.axes"),
                wrongAxes)));
        Expect(mismatch is InvalidOperationException && evaluator.Calls == 0,
            "The guarded boundary rejects custom evaluator axis mismatch before policy code runs");
    }

    private static void VerifyEvaluatorDriftDuringCallbackIsRejected()
    {
        var evaluator = new DriftingEvaluator();
        var ethics = new KLEPEthics<ScenarioContext>(evaluator);

        Exception drift = Catch(() => ethics.Evaluate(
            Request(
                "evaluation.drifting-version",
                new ScenarioContext("act.drift"))));
        Expect(drift is InvalidOperationException &&
               evaluator.EvaluatorVersion == "2",
            "Evaluator identity and schema are rechecked after custom policy code returns");
    }

    private static void VerifyPublishedEvaluationDoesNotRetainLiveContext()
    {
        var context = new ScenarioContext("act.before", wasProtected: true);
        KLEPEthicsEvaluation<ScenarioContext> evaluation = Weighted(
            AlwaysRule("context-copy", 1f, 0.25f, 0f)).Evaluate(
                Request("evaluation.context-copy", context));

        context.ActionId = "act.after";
        Expect(evaluation.ContextIdentity.ContextId ==
                   "context:evaluation.context-copy" &&
               evaluation.Judgment.Impulse.X == 0.25f,
            "Completed Ethics results retain immutable context identity and judgment after caller mutation");
        Expect(typeof(KLEPEthicsEvaluation<ScenarioContext>)
                   .GetProperty("Context") == null &&
               typeof(KLEPEthicsEvaluation<ScenarioContext>)
                   .GetProperty("Request") == null,
            "A published evaluation does not expose the transient live context or request");
    }

    private static void VerifyEvidenceReferencesAreImmutable()
    {
        var evidenceIds = new List<string> { "observation.protected" };
        var rule = new KLEPWeightedEthicsRule<ScenarioContext>(
            "evidence",
            1f,
            context => true,
            new KLEPEmotionVector(0.5f, 0f),
            "evidence.applied",
            evidenceIds: evidenceIds);
        evidenceIds.Clear();

        KLEPEthicsTraceEntry trace = Weighted(rule).Evaluate(
            Request(
                "evaluation.evidence",
                new ScenarioContext("act.evidence")))
            .Judgment.Trace[1];
        var mutableEvidence = trace.EvidenceIds as IList<string>;
        Expect(trace.EvidenceIds.Count == 1 &&
               trace.EvidenceIds[0] == "observation.protected" &&
               mutableEvidence != null && mutableEvidence.IsReadOnly,
            "Rule traces retain immutable stable references to the evidence they used");
        Expect(Catch(() => new KLEPEthicsRuleMatch(
                   true,
                   KLEPEmotionVector.Zero,
                   "duplicate-evidence",
                   new[] { "same", "same" })) is ArgumentException,
            "An Ethics trace cannot ambiguously cite the same evidence twice");
    }

    private static void VerifyEvaluationFeedsEmotionWithoutAdvancingIt()
    {
        KLEPEmotionConfiguration configuration = Configuration();
        KLEPEthicsEvaluation<ScenarioContext> evaluation = Weighted(
            AlwaysRule("positive", 0.5f, 1f, 0f)).Evaluate(
                Request(
                    "evaluation.to-emotion",
                    new ScenarioContext("act.evaluated"),
                    configuration));
        var emotion = new KLEPEmotion(configuration);

        Expect(emotion.Tick == 0,
            "Ethics evaluation does not advance Emotion");
        KLEPEmotionSnapshot snapshot = emotion.Advance(
            1,
            new[] { evaluation.Influence });
        Expect(snapshot.Influences.Count == 1 &&
               snapshot.Influences[0].SourceId == "evaluation.to-emotion" &&
               snapshot.Influences[0].Impulse ==
                   evaluation.Judgment.Impulse,
            "Emotion preserves the exact guarded Ethics influence");
    }

    private static void VerifyInvalidConfigurationAndResultsAreRejected()
    {
        Expect(Catch(() => new KLEPEthicsRequest<ScenarioContext>(
                   "",
                   0,
                   KLEPEmotionInfluenceOrigin.Internal,
                   Configuration(),
                   ContextIdentity("context.invalid"),
                   new ScenarioContext("act.invalid"))) is ArgumentException,
            "An Ethics request requires a unique evaluation ID");
        Expect(Catch(() => new KLEPWeightedEthicsRule<ScenarioContext>(
                   "invalid-weight",
                   float.NaN,
                   context => true,
                   KLEPEmotionVector.Zero,
                   "invalid")) is ArgumentOutOfRangeException,
            "Weighted Ethics rejects a non-finite rule weight");
        Expect(Catch(() => new KLEPWeightedEthicsRule<ScenarioContext>(
                   "invalid-weight",
                   1.1f,
                   context => true,
                   KLEPEmotionVector.Zero,
                   "invalid")) is ArgumentOutOfRangeException,
            "Weighted Ethics rejects weights above one");

        IKLEPWeightedEthicsRule<ScenarioContext> duplicate =
            AlwaysRule("duplicate", 1f, 0f, 0f);
        Expect(Catch(() => NewWeightedEvaluator(duplicate, duplicate))
               is ArgumentException,
            "Weighted Ethics rejects duplicate ordinal rule IDs");
        Expect(Catch(() => new KLEPEthics<ScenarioContext>(
                   new NullJudgmentEvaluator())) is null,
            "A valid custom evaluator may register before its first evaluation");

        var nullEthics = new KLEPEthics<ScenarioContext>(
            new NullJudgmentEvaluator());
        Expect(Catch(() => nullEthics.Evaluate(
                   Request("evaluation.null", new ScenarioContext("act.null"))))
               is InvalidOperationException,
            "A custom evaluator failure cannot silently become moral neutrality");
        Expect(Catch(() => new KLEPEthicsJudgment(
                   Array.Empty<KLEPEthicsTraceEntry>())) is ArgumentException,
            "A custom judgment must provide an inspectable explanation trace");

        var duplicateTrace = new[]
        {
            Trace("same", 0.1f),
            Trace("same", 0.2f)
        };
        Expect(Catch(() => new KLEPEthicsJudgment(duplicateTrace))
               is ArgumentException,
            "A judgment rejects duplicate explanation source IDs");
    }

    private static void VerifyTraceAndRuleInputsAreDefensivelyCopied()
    {
        var rules = new List<IKLEPWeightedEthicsRule<ScenarioContext>>
        {
            AlwaysRule("copied-rule", 1f, 0.2f, 0f)
        };
        KLEPWeightedEthicsEvaluator<ScenarioContext> evaluator =
            NewWeightedEvaluator(rules.ToArray());
        rules.Clear();

        KLEPEthicsJudgment judgment = evaluator.Evaluate(
            new ScenarioContext("act.copy"),
            1,
            Configuration());
        Expect(evaluator.RuleCount == 1 && judgment.Trace.Count == 2,
            "A weighted evaluator owns an immutable copy of authored rules");

        IReadOnlyList<KLEPEthicsTraceEntry> trace = judgment.Trace;
        var mutableTrace = trace as IList<KLEPEthicsTraceEntry>;
        Expect(mutableTrace != null && mutableTrace.IsReadOnly,
            "A published Ethics trace cannot be modified by its consumer");
    }

    private static void VerifyEquivalentEvaluationsAreDeterministic()
    {
        KLEPEthics<ScenarioContext> first = Weighted(
            AlwaysRule("a", 0.25f, 0.5f, -0.5f),
            AlwaysRule("b", 0.75f, -0.25f, 0.5f));
        KLEPEthics<ScenarioContext> second = Weighted(
            AlwaysRule("a", 0.25f, 0.5f, -0.5f),
            AlwaysRule("b", 0.75f, -0.25f, 0.5f));
        KLEPEthicsRequest<ScenarioContext> request = Request(
            "evaluation.deterministic",
            new ScenarioContext("act.deterministic"));

        KLEPEthicsEvaluation<ScenarioContext> left = first.Evaluate(request);
        KLEPEthicsEvaluation<ScenarioContext> right = second.Evaluate(request);
        Expect(Equivalent(left, right),
            "Equivalent Ethics inputs produce the same complete evaluation and trace");
    }

    private static bool Equivalent(
        KLEPEthicsEvaluation<ScenarioContext> left,
        KLEPEthicsEvaluation<ScenarioContext> right)
    {
        if (left.EvaluationId != right.EvaluationId ||
            left.EvaluationTick != right.EvaluationTick ||
            left.EvaluatorId != right.EvaluatorId ||
            left.EvaluatorVersion != right.EvaluatorVersion ||
            left.Judgment.RawX != right.Judgment.RawX ||
            left.Judgment.RawY != right.Judgment.RawY ||
            left.Judgment.WasClamped != right.Judgment.WasClamped ||
            left.Judgment.Impulse != right.Judgment.Impulse ||
            left.Influence.SourceId != right.Influence.SourceId ||
            left.Influence.Origin != right.Influence.Origin ||
            left.Judgment.Trace.Count != right.Judgment.Trace.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Judgment.Trace.Count; i++)
        {
            KLEPEthicsTraceEntry a = left.Judgment.Trace[i];
            KLEPEthicsTraceEntry b = right.Judgment.Trace[i];
            if (a.SourceId != b.SourceId ||
                a.Applied != b.Applied ||
                a.Weight != b.Weight ||
                a.ProposedImpulse != b.ProposedImpulse ||
                a.ReasonCode != b.ReasonCode ||
                a.ContributionX != b.ContributionX ||
                a.ContributionY != b.ContributionY)
            {
                return false;
            }
        }

        return true;
    }

    private static KLEPEthics<ScenarioContext> Weighted(
        params IKLEPWeightedEthicsRule<ScenarioContext>[] rules)
    {
        return new KLEPEthics<ScenarioContext>(NewWeightedEvaluator(rules));
    }

    private static KLEPWeightedEthicsEvaluator<ScenarioContext>
        NewWeightedEvaluator(
            params IKLEPWeightedEthicsRule<ScenarioContext>[] rules)
    {
        return new KLEPWeightedEthicsEvaluator<ScenarioContext>(
            "ethics.weighted",
            "1.0.0",
            "Valence",
            "Activation",
            KLEPEmotionVector.Zero,
            rules);
    }

    private static KLEPWeightedEthicsRule<ScenarioContext> AlwaysRule(
        string id,
        float weight,
        float x,
        float y)
    {
        return new KLEPWeightedEthicsRule<ScenarioContext>(
            id,
            weight,
            context => true,
            new KLEPEmotionVector(x, y),
            $"{id}.applied");
    }

    private static KLEPEthicsTraceEntry Trace(string id, float x)
    {
        return new KLEPEthicsTraceEntry(
            id,
            true,
            1f,
            new KLEPEmotionVector(x, 0f),
            $"{id}.reason");
    }

    private static KLEPEthicsRequest<ScenarioContext> Request(
        string id,
        ScenarioContext context,
        KLEPEmotionConfiguration configuration = null)
    {
        return new KLEPEthicsRequest<ScenarioContext>(
            id,
            1,
            KLEPEmotionInfluenceOrigin.External,
            configuration ?? Configuration(),
            ContextIdentity($"context:{id}"),
            context);
    }

    private static KLEPEthicsContextIdentity ContextIdentity(string contextId)
    {
        return new KLEPEthicsContextIdentity(
            contextId,
            "scenario-context",
            "1");
    }

    private static KLEPEmotionConfiguration Configuration()
    {
        return new KLEPEmotionConfiguration("Valence", "Activation");
    }

    private static Exception Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ScenarioContext
    {
        internal ScenarioContext(
            string actionId,
            bool wasProtected = false,
            bool wasHarmed = false)
        {
            ActionId = actionId;
            WasProtected = wasProtected;
            WasHarmed = wasHarmed;
        }

        internal string ActionId { get; set; }
        internal bool WasProtected { get; }
        internal bool WasHarmed { get; }
    }

    private sealed class ProbeRule : IKLEPWeightedEthicsRule<ScenarioContext>
    {
        private readonly List<string> calls;
        private readonly float x;

        internal ProbeRule(string ruleId, List<string> calls, float x)
        {
            RuleId = ruleId;
            this.calls = calls;
            this.x = x;
        }

        public string RuleId { get; }
        public float Weight => 1f;

        public KLEPEthicsRuleMatch Evaluate(ScenarioContext context)
        {
            calls.Add(RuleId);
            return new KLEPEthicsRuleMatch(
                true,
                new KLEPEmotionVector(x, 0f),
                $"{RuleId}.applied");
        }
    }

    private sealed class CustomEvaluator : IKLEPEthicsEvaluator<ScenarioContext>
    {
        public int Calls { get; private set; }
        public string EvaluatorId => "ethics.custom";
        public string EvaluatorVersion => "custom-v2";
        public string ExpectedAxisXName => "Valence";
        public string ExpectedAxisYName => "Activation";

        public KLEPEthicsJudgment Evaluate(
            ScenarioContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration)
        {
            Calls++;
            return new KLEPEthicsJudgment(new[]
            {
                new KLEPEthicsTraceEntry(
                    "custom.final-output",
                    true,
                    1f,
                    new KLEPEmotionVector(0.25f, 0.5f),
                    "custom.context-evaluated")
            });
        }
    }

    private sealed class DriftingEvaluator :
        IKLEPEthicsEvaluator<ScenarioContext>
    {
        private string version = "1";

        public string EvaluatorId => "ethics.drifting";
        public string EvaluatorVersion => version;
        public string ExpectedAxisXName => "Valence";
        public string ExpectedAxisYName => "Activation";

        public KLEPEthicsJudgment Evaluate(
            ScenarioContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration)
        {
            version = "2";
            return new KLEPEthicsJudgment(new[]
            {
                new KLEPEthicsTraceEntry(
                    "drift.result",
                    true,
                    1f,
                    KLEPEmotionVector.Zero,
                    "drifted-during-evaluation")
            });
        }
    }

    private sealed class NullJudgmentEvaluator :
        IKLEPEthicsEvaluator<ScenarioContext>
    {
        public string EvaluatorId => "ethics.null";
        public string EvaluatorVersion => "1";
        public string ExpectedAxisXName => "Valence";
        public string ExpectedAxisYName => "Activation";

        public KLEPEthicsJudgment Evaluate(
            ScenarioContext context,
            long evaluationTick,
            KLEPEmotionConfiguration emotionConfiguration)
        {
            return null;
        }
    }
}
