using System;
using System.Collections.Generic;
using System.Reflection;
using Roll4d4.Klep.Desire;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        VerifyDefinitionAndAssessmentValidation();
        VerifyObservationOwnsOrderedImmutableState();
        VerifyObservationIsAtomicAndTicksAreExplicit();
        VerifyEvaluatorIdentityIsGuarded();
        VerifyRawEffectVectorPreservesDesireMeanings();
        VerifyAttributionKindsAndExactActionOwnership();
        VerifyTransitionCompatibilityIsGuarded();
        VerifyEquivalentRunsAreDeterministic();

        Console.WriteLine(
            $"KLEP Desire smoke passed: {assertions} assertions.");
    }

    private static void VerifyDefinitionAndAssessmentValidation()
    {
        var evaluator = new ContextEvaluator("eval.need", "1", "need");
        Expect(Catch(() => new KLEPDesireDefinition<DesireContext>(
                   "", "1", 1f, evaluator)) is ArgumentException,
            "Desire definitions require stable identity");
        Expect(Catch(() => new KLEPDesireDefinition<DesireContext>(
                   "desire.need", "", 1f, evaluator)) is ArgumentException,
            "Desire definitions require a version");
        Expect(Catch(() => new KLEPDesireDefinition<DesireContext>(
                   "desire.need", "1", -0.01f, evaluator))
               is ArgumentOutOfRangeException,
            "Desire importance cannot be negative");
        Expect(Catch(() => new KLEPDesireDefinition<DesireContext>(
                   "desire.need", "1", float.NaN, evaluator))
               is ArgumentOutOfRangeException,
            "Desire importance must be finite");
        Expect(Catch(() => new KLEPDesireDefinition<DesireContext>(
                   "desire.need", "1", 1f, null))
               is ArgumentNullException,
            "Desire definitions require a project evaluator");

        Expect(Catch(() => new KLEPDesireAssessment(
                   -0.01f, 1f, "invalid")) is ArgumentOutOfRangeException,
            "Satisfaction cannot be below zero");
        Expect(Catch(() => new KLEPDesireAssessment(
                   1.01f, 1f, "invalid")) is ArgumentOutOfRangeException,
            "Satisfaction cannot exceed one");
        Expect(Catch(() => new KLEPDesireAssessment(
                   float.PositiveInfinity, 1f, "invalid"))
               is ArgumentOutOfRangeException,
            "Satisfaction must be finite");
        Expect(Catch(() => new KLEPDesireAssessment(
                   0.5f, -0.01f, "invalid"))
               is ArgumentOutOfRangeException,
            "Desire pressure cannot be negative");
        Expect(Catch(() => new KLEPDesireAssessment(
                   0.5f, float.NaN, "invalid"))
               is ArgumentOutOfRangeException,
            "Desire pressure must be finite");
        Expect(Catch(() => new KLEPDesireAssessment(
                   0.5f, 1f, " ")) is ArgumentException,
            "Every project assessment remains explainable");

        var sourceEvidence = new List<string> { "fact.one" };
        var valid = new KLEPDesireAssessment(
            0.25f,
            0f,
            "One quarter satisfied.",
            sourceEvidence);
        sourceEvidence[0] = "mutated";
        Expect(valid.Satisfaction == 0.25f &&
               valid.Pressure == 0f &&
               valid.EvidenceIds.Count == 1 &&
               valid.EvidenceIds[0] == "fact.one",
            "Assessments defensively copy project evidence");
        Expect(Catch(() => new KLEPDesireAssessment(
                   0.5f,
                   1f,
                   "duplicate evidence",
                   new[] { "same", "same" })) is ArgumentException,
            "Assessment evidence identities are unique");

        Expect(Catch(() => new KLEPDesireContextIdentity("", "schema", "1"))
               is ArgumentException,
            "Desire context identity is inspectable");
        Expect(Catch(() => new KLEPDesireObservationRequest<DesireContext>(
                   "snapshot",
                   -1,
                   "moment",
                   ContextIdentity("context"),
                   new DesireContext())) is ArgumentOutOfRangeException,
            "Desire observation Tick cannot be negative");
        Expect(Catch(() => new KLEPDesireObservationRequest<DesireContext>(
                   "snapshot",
                   0,
                   "moment",
                   ContextIdentity("context"),
                   null)) is ArgumentNullException,
            "Desire evaluators cannot receive missing context");

        var duplicate = new KLEPDesireDefinition<DesireContext>(
            "desire.duplicate", "1", 1f, evaluator);
        Expect(Catch(() => new KLEPDesireSystem<DesireContext>(
                   "owner",
                   new[] { duplicate, duplicate })) is ArgumentException,
            "One Desire system rejects duplicate definition IDs");
    }

    private static void VerifyObservationOwnsOrderedImmutableState()
    {
        var calls = new List<string>();
        var definitions = new List<KLEPDesireDefinition<DesireContext>>
        {
            Definition("desire.food", 2f,
                new ContextEvaluator("eval.food", "1", "food", calls)),
            Definition("desire.safety", 0.5f,
                new ContextEvaluator("eval.safety", "2", "safety", calls))
        };
        var system = new KLEPDesireSystem<DesireContext>(
            "neuron.one",
            definitions);
        string fingerprint = system.DefinitionFingerprint;
        definitions.Clear();

        var context = new DesireContext();
        context.Set("food", 0.25f, 3f, "fact.food");
        context.Set("safety", 1f, 0f, "fact.safety");
        KLEPDesireSnapshot snapshot = system.Observe(Request(
            "snapshot.one", 0, "moment.prior", "context.prior", context));

        Expect(calls.Count == 2 &&
               calls[0] == "food" && calls[1] == "safety",
            "Desire evaluators run exactly once in authored order");
        Expect(system.Definitions.Count == 2 &&
               system.CurrentSnapshot == snapshot &&
               system.CurrentTick == 0 &&
               snapshot.DefinitionFingerprint == fingerprint,
            "The system owns copied definitions and publishes its current snapshot");
        Expect(snapshot.OwnerId == "neuron.one" &&
               snapshot.SnapshotId == "snapshot.one" &&
               snapshot.DesireTick == 0 &&
               snapshot.ObservedMomentId == "moment.prior" &&
               snapshot.ContextIdentity.ContextId == "context.prior",
            "A snapshot retains exact owner, Tick, moment, and context identity");

        KLEPDesireStateSnapshot food = snapshot.Desires[0];
        KLEPDesireStateSnapshot safety = snapshot.Desires[1];
        Expect(food.DesireStableId == "desire.food" &&
               food.DesireVersion == "1" &&
               food.EvaluatorId == "eval.food" &&
               food.Weight == 2f,
            "A Desire state preserves guarded definition and evaluator identity");
        Expect(food.Satisfaction == 0.25f &&
               food.Deficit == 1f - food.Satisfaction &&
               food.Pressure == 3f &&
               !food.IsDormant,
            "Satisfaction, exact derived deficit, and current pressure remain separate");
        Expect(safety.Satisfaction == 1f &&
               safety.Deficit == 0f &&
               safety.Pressure == 0f &&
               safety.IsDormant,
            "Zero pressure marks a Desire dormant without changing satisfaction");

        context.Set("food", 1f, 99f, "fact.changed");
        Expect(food.Satisfaction == 0.25f &&
               food.Pressure == 3f &&
               food.EvidenceIds[0] == "fact.food",
            "A completed Desire snapshot retains no mutable project state");
        Expect(typeof(KLEPDesireSnapshot).GetProperty("Context") == null,
            "The public Desire snapshot does not expose transient live context");
    }

    private static void VerifyObservationIsAtomicAndTicksAreExplicit()
    {
        var first = new ContextEvaluator("eval.first", "1", "first");
        var second = new ControlledEvaluator("eval.second", "1");
        var system = new KLEPDesireSystem<DesireContext>(
            "owner.atomic",
            new[]
            {
                Definition("desire.first", 1f, first),
                Definition("desire.second", 1f, second)
            });
        var context = new DesireContext();
        context.Set("first", 0f, 1f, "fact.first");

        second.Throw = true;
        Exception fault = Catch(() => system.Observe(Request(
            "snapshot.retry", 0, "moment.zero", "context.zero", context)));
        Expect(fault is ProbeException &&
               system.CurrentSnapshot == null &&
               system.CurrentTick == null,
            "A fault after an earlier evaluator publishes no partial state");

        second.Throw = false;
        KLEPDesireSnapshot recovered = system.Observe(Request(
            "snapshot.retry", 0, "moment.zero", "context.zero", context));
        Expect(recovered.DesireTick == 0 && recovered.Desires.Count == 2,
            "A failed snapshot identity and Tick can be retried exactly");
        Expect(Catch(() => system.Observe(Request(
                   "snapshot.same-tick",
                   0,
                   "moment.same",
                   "context.same",
                   context))) is ArgumentOutOfRangeException,
            "Published Desire Ticks must strictly increase");

        KLEPDesireSnapshot next = system.Observe(Request(
            "snapshot.next", 2, "moment.next", "context.next", context));
        Expect(next.DesireTick == 2,
            "Desire Tick is explicit and increasing rather than wall-clock driven");
        KLEPDesireSnapshot repeatedLabel = system.Observe(Request(
            "snapshot.retry",
            3,
            "moment.repeated-label",
            "context.repeated-label",
            context));
        Expect(repeatedLabel.SnapshotId == "snapshot.retry" &&
               repeatedLabel.DesireTick == 3 &&
               repeatedLabel.OwnerId == "owner.atomic",
            "Snapshot labels may repeat because owner plus Desire Tick is observation identity");

        for (long tick = 4; tick <= 1027; tick++)
        {
            system.Observe(Request(
                "reusable-label",
                tick,
                $"moment.sustained.{tick}",
                $"context.sustained.{tick}",
                context));
        }

        FieldInfo[] privateFields = typeof(KLEPDesireSystem<DesireContext>)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        bool retainsLifetimeSnapshotIds = false;
        for (int i = 0; i < privateFields.Length; i++)
        {
            if (privateFields[i].FieldType == typeof(HashSet<string>))
            {
                retainsLifetimeSnapshotIds = true;
                break;
            }
        }

        Expect(system.CurrentTick == 1027 &&
               system.CurrentSnapshot.SnapshotId == "reusable-label" &&
               !retainsLifetimeSnapshotIds,
            "Sustained observations retain only current Desire state, not lifetime label history");

        var nullEvaluator = new ControlledEvaluator("eval.null", "1")
        {
            ReturnNull = true
        };
        var nullSystem = new KLEPDesireSystem<DesireContext>(
            "owner.null",
            new[] { Definition("desire.null", 1f, nullEvaluator) });
        Expect(Catch(() => nullSystem.Observe(Request(
                   "snapshot.null", 1, "moment.null", "context.null", context)))
               is InvalidOperationException &&
               nullSystem.CurrentSnapshot == null,
            "A missing evaluator assessment publishes nothing");
    }

    private static void VerifyEvaluatorIdentityIsGuarded()
    {
        var evaluator = new ControlledEvaluator("eval.guard", "1");
        var system = new KLEPDesireSystem<DesireContext>(
            "owner.guard",
            new[] { Definition("desire.guard", 1f, evaluator) });
        var context = new DesireContext();
        KLEPDesireSnapshot prior = system.Observe(Request(
            "snapshot.guard.prior",
            1,
            "moment.guard.prior",
            "context.guard.prior",
            context));

        evaluator.DriftVersionDuringEvaluation = true;
        Exception drift = Catch(() => system.Observe(Request(
            "snapshot.guard.drift",
            2,
            "moment.guard.drift",
            "context.guard.drift",
            context)));
        Expect(drift is InvalidOperationException &&
               ReferenceEquals(system.CurrentSnapshot, prior) &&
               system.CurrentTick == 1,
            "Evaluator identity drift is rejected after callback without publication");

        int callCount = evaluator.CallCount;
        Expect(Catch(() => system.Observe(Request(
                   "snapshot.guard.preflight",
                   2,
                   "moment.guard.preflight",
                   "context.guard.preflight",
                   context))) is InvalidOperationException &&
               evaluator.CallCount == callCount,
            "Already-drifted evaluator identity fails before project code runs");
    }

    private static void VerifyRawEffectVectorPreservesDesireMeanings()
    {
        var system = StandardSystem("owner.effects");
        var priorContext = new DesireContext();
        priorContext.Set("food", 0.2f, 10f, "food.before");
        priorContext.Set("safety", 0.8f, 0f, "safety.before");
        priorContext.Set("belonging", 0.5f, 2f, "belonging.before");
        KLEPDesireSnapshot prior = system.Observe(Request(
            "snapshot.effects.prior",
            10,
            "moment.effects.prior",
            "context.effects.prior",
            priorContext));

        var afterContext = new DesireContext();
        afterContext.Set("food", 0.7f, 1f, "food.after");
        afterContext.Set("safety", 0.3f, 5f, "safety.after");
        afterContext.Set("belonging", 0.5f, 99f, "belonging.after");
        KLEPDesireSnapshot after = system.Observe(Request(
            "snapshot.effects.after",
            11,
            "moment.effects.after",
            "context.effects.after",
            afterContext));

        var attribution = new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.ActionOwned,
            "causal.agent-trace:11",
            "goal.eat",
            7,
            new[] { "agent.result:goal.eat:7" });
        KLEPDesireEffectVector vector = system.EvaluateTransition(
            new KLEPDesireTransitionRequest(
                "transition.effects",
                prior,
                after,
                attribution));

        Expect(vector.Effects.Count == 3 &&
               vector.Effects[0].DesireStableId == "desire.food" &&
               vector.Effects[1].DesireStableId == "desire.safety" &&
               vector.Effects[2].DesireStableId == "desire.belonging",
            "The complete effect vector includes every Desire in authored order");
        KLEPDesireEffectTrace food = vector.Effects[0];
        KLEPDesireEffectTrace safety = vector.Effects[1];
        KLEPDesireEffectTrace belonging = vector.Effects[2];
        Expect(food.Effect == food.SatisfactionAfter - food.SatisfactionBefore &&
               food.Effect == 0.7f - 0.2f &&
               food.Weight == 100f &&
               food.PressureBefore == 10f &&
               food.PressureAfter == 1f,
            "Raw Desire Effect is after minus before without weight or pressure multiplication");
        Expect(safety.Effect == 0.3f - 0.8f &&
               safety.DeficitBefore == 1f - 0.8f &&
               safety.DeficitAfter == 1f - 0.3f,
            "Worsening remains a raw negative satisfaction change");
        Expect(belonging.Effect == 0f &&
               belonging.PressureBefore == 2f &&
               belonging.PressureAfter == 99f,
            "A zero effect remains present even when current urgency changed");
        Expect(food.ExplanationBefore ==
                   "food evaluated at Desire Tick 10." &&
               food.ExplanationAfter ==
                   "food evaluated at Desire Tick 11." &&
               food.EvidenceIdsBefore.Count == 1 &&
               food.EvidenceIdsBefore[0] == "food.before" &&
               food.EvidenceIdsAfter.Count == 1 &&
               food.EvidenceIdsAfter[0] == "food.after",
            "An effect retains both evaluators' explanations and evidence references");
        IList<string> immutableBeforeEvidence =
            (IList<string>)food.EvidenceIdsBefore;
        IList<string> immutableAfterEvidence =
            (IList<string>)food.EvidenceIdsAfter;
        Expect(Catch(() => immutableBeforeEvidence[0] = "mutated")
                   is NotSupportedException &&
               Catch(() => immutableAfterEvidence[0] = "mutated")
                   is NotSupportedException &&
               food.EvidenceIdsBefore[0] == "food.before" &&
               food.EvidenceIdsAfter[0] == "food.after",
            "Before and after effect evidence collections are immutable defensive copies");
        Expect(typeof(KLEPDesireEffectTrace).GetProperty("Prior") == null &&
               typeof(KLEPDesireEffectTrace).GetProperty("Consequence") == null &&
               typeof(KLEPDesireEffectTrace).GetProperty("Context") == null,
            "Self-contained effect reasoning does not retain snapshots or project context");

        Expect(vector.OwnerId == "owner.effects" &&
               vector.PriorSnapshotId == "snapshot.effects.prior" &&
               vector.ConsequenceSnapshotId == "snapshot.effects.after" &&
               vector.PriorDesireTick == 10 &&
               vector.ConsequenceDesireTick == 11 &&
               vector.PriorMomentId == "moment.effects.prior" &&
               vector.ConsequenceMomentId == "moment.effects.after",
            "Effect vectors preserve exact observation boundary provenance");
        Expect(typeof(KLEPDesireEffectVector).GetProperty("Total") == null &&
               typeof(KLEPDesireEffectVector).GetProperty("Reward") == null &&
               typeof(KLEPDesireEffectVector).GetProperty("Score") == null &&
               typeof(KLEPDesireEffectVector).GetProperty("Attraction") == null,
            "The first Desire vector exposes no aggregate reward or selection value");
    }

    private static void VerifyAttributionKindsAndExactActionOwnership()
    {
        Expect(Catch(() => new KLEPDesireAttributionEvidence(
                   (KLEPDesireEffectAttribution)99,
                   "invalid")) is ArgumentOutOfRangeException,
            "Unknown attribution enum values are rejected");
        Expect(Catch(() => new KLEPDesireAttributionEvidence(
                   KLEPDesireEffectAttribution.ActionOwned,
                   "trace",
                   actionStableId: null,
                   actionRunIndex: 1)) is ArgumentException,
            "ActionOwned attribution requires exact action identity");
        Expect(Catch(() => new KLEPDesireAttributionEvidence(
                   KLEPDesireEffectAttribution.ActionOwned,
                   "trace",
                   "goal.eat",
                   0)) is ArgumentOutOfRangeException,
            "ActionOwned attribution requires a positive exact run index");
        Expect(Catch(() => new KLEPDesireAttributionEvidence(
                   KLEPDesireEffectAttribution.External,
                   "world.event",
                   "goal.claimed",
                   1)) is ArgumentException,
            "Non-action attribution cannot claim exclusive action ownership");

        var action = new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.ActionOwned,
            "trace.action",
            "goal.eat",
            4);
        var external = new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.External,
            "world.storm");
        var mixed = new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.Mixed,
            "causality.mixed");
        var unknown = new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.Unknown,
            "causality.unknown");
        Expect(action.ActionStableId == "goal.eat" &&
               action.ActionRunIndex == 4 &&
               action.IsEligibleForAutomaticExpectationLearning,
            "Only exact ActionOwned provenance is marked eligible for future learning");
        Expect(!external.IsEligibleForAutomaticExpectationLearning &&
               !mixed.IsEligibleForAutomaticExpectationLearning &&
               !unknown.IsEligibleForAutomaticExpectationLearning,
            "External, Mixed, and Unknown effects remain factual but not automatically learnable");
    }

    private static void VerifyTransitionCompatibilityIsGuarded()
    {
        var system = StandardSystem("owner.compatible");
        KLEPDesireSnapshot prior = system.Observe(Request(
            "snapshot.compatible.prior",
            1,
            "moment.compatible.prior",
            "context.compatible.prior",
            FilledContext(0.1f)));
        KLEPDesireSnapshot after = system.Observe(Request(
            "snapshot.compatible.after",
            2,
            "moment.compatible.after",
            "context.compatible.after",
            FilledContext(0.2f)));
        KLEPDesireAttributionEvidence unknown = UnknownAttribution();

        Expect(Catch(() => system.EvaluateTransition(
                   new KLEPDesireTransitionRequest(
                       "transition.reversed", after, prior, unknown)))
               is ArgumentException,
            "A Desire transition cannot run backward in Tick time");

        var repeatedLabelSystem = StandardSystem("owner.repeated-label");
        KLEPDesireSnapshot repeatedLabelPrior = repeatedLabelSystem.Observe(Request(
            "same-readable-label",
            1,
            "moment.repeated-label.prior",
            "context.repeated-label.prior",
            FilledContext(0.1f)));
        KLEPDesireSnapshot repeatedLabelAfter = repeatedLabelSystem.Observe(Request(
            "same-readable-label",
            2,
            "moment.repeated-label.after",
            "context.repeated-label.after",
            FilledContext(0.2f)));
        KLEPDesireEffectVector repeatedLabelVector =
            repeatedLabelSystem.EvaluateTransition(
                new KLEPDesireTransitionRequest(
                    "transition.repeated-label",
                    repeatedLabelPrior,
                    repeatedLabelAfter,
                    unknown));
        Expect(repeatedLabelVector.PriorSnapshotId == "same-readable-label" &&
               repeatedLabelVector.ConsequenceSnapshotId ==
                   "same-readable-label" &&
               repeatedLabelVector.PriorDesireTick == 1 &&
               repeatedLabelVector.ConsequenceDesireTick == 2,
            "Tick ordering distinguishes transition observations with the same readable label");

        var foreignOwner = StandardSystem("owner.foreign");
        KLEPDesireSnapshot foreign = foreignOwner.Observe(Request(
            "snapshot.foreign",
            3,
            "moment.foreign",
            "context.foreign",
            FilledContext(0.4f)));
        Expect(Catch(() => system.EvaluateTransition(
                   new KLEPDesireTransitionRequest(
                       "transition.foreign", prior, foreign, unknown)))
               is ArgumentException,
            "Effect evaluation rejects another Desire owner");

        var changedDefinitions = new KLEPDesireSystem<DesireContext>(
            "owner.compatible",
            new[]
            {
                Definition("desire.food", 101f,
                    new ContextEvaluator("eval.food", "1", "food"))
            });
        KLEPDesireSnapshot changed = changedDefinitions.Observe(Request(
            "snapshot.changed",
            3,
            "moment.changed",
            "context.changed",
            FilledContext(0.4f)));
        Expect(Catch(() => system.EvaluateTransition(
                   new KLEPDesireTransitionRequest(
                       "transition.changed", prior, changed, unknown)))
               is ArgumentException,
            "Effect evaluation rejects a changed definition fingerprint");

        var sameMomentSystem = StandardSystem("owner.same-moment");
        KLEPDesireSnapshot sameMomentPrior = sameMomentSystem.Observe(Request(
            "snapshot.same-moment.prior",
            1,
            "moment.same",
            "context.same.prior",
            FilledContext(0.1f)));
        KLEPDesireSnapshot sameMomentAfter = sameMomentSystem.Observe(Request(
            "snapshot.same-moment.after",
            2,
            "moment.same",
            "context.same.after",
            FilledContext(0.2f)));
        Expect(Catch(() => sameMomentSystem.EvaluateTransition(
                   new KLEPDesireTransitionRequest(
                       "transition.same-moment",
                       sameMomentPrior,
                       sameMomentAfter,
                       unknown))) is ArgumentException,
            "Effect evidence requires exact distinct causal moment boundaries");

        var schemaSystem = StandardSystem("owner.schema");
        KLEPDesireSnapshot schemaPrior = schemaSystem.Observe(
            new KLEPDesireObservationRequest<DesireContext>(
                "snapshot.schema.prior",
                1,
                "moment.schema.prior",
                new KLEPDesireContextIdentity("context.one", "schema.one", "1"),
                FilledContext(0.1f)));
        KLEPDesireSnapshot schemaAfter = schemaSystem.Observe(
            new KLEPDesireObservationRequest<DesireContext>(
                "snapshot.schema.after",
                2,
                "moment.schema.after",
                new KLEPDesireContextIdentity("context.two", "schema.two", "1"),
                FilledContext(0.2f)));
        Expect(Catch(() => schemaSystem.EvaluateTransition(
                   new KLEPDesireTransitionRequest(
                       "transition.schema",
                       schemaPrior,
                       schemaAfter,
                       unknown))) is ArgumentException,
            "Effect evaluation rejects incompatible project context schemas");

        var equivalentSystem = StandardSystem("owner.compatible");
        KLEPDesireSnapshot equivalentAfter = equivalentSystem.Observe(Request(
            "snapshot.equivalent",
            3,
            "moment.equivalent",
            "context.equivalent",
            FilledContext(0.4f)));
        KLEPDesireEffectVector compatible = system.EvaluateTransition(
            new KLEPDesireTransitionRequest(
                "transition.same-owner-definition",
                prior,
                equivalentAfter,
                unknown));
        Expect(compatible.Effects.Count == prior.Desires.Count,
            "Compatible snapshots from the same owner and exact definitions may be compared");
    }

    private static void VerifyEquivalentRunsAreDeterministic()
    {
        KLEPDesireSystem<DesireContext> left = StandardSystem("owner.same");
        KLEPDesireSystem<DesireContext> right = StandardSystem("owner.same");
        DesireContext before = FilledContext(0.25f);
        DesireContext after = FilledContext(0.75f);
        KLEPDesireSnapshot leftBefore = left.Observe(Request(
            "snapshot.before", 4, "moment.before", "context.before", before));
        KLEPDesireSnapshot rightBefore = right.Observe(Request(
            "snapshot.before", 4, "moment.before", "context.before", before));
        KLEPDesireSnapshot leftAfter = left.Observe(Request(
            "snapshot.after", 5, "moment.after", "context.after", after));
        KLEPDesireSnapshot rightAfter = right.Observe(Request(
            "snapshot.after", 5, "moment.after", "context.after", after));
        KLEPDesireAttributionEvidence attribution = UnknownAttribution();
        KLEPDesireEffectVector leftVector = left.EvaluateTransition(
            new KLEPDesireTransitionRequest(
                "transition.same", leftBefore, leftAfter, attribution));
        KLEPDesireEffectVector rightVector = right.EvaluateTransition(
            new KLEPDesireTransitionRequest(
                "transition.same", rightBefore, rightAfter, attribution));

        bool equal = left.DefinitionFingerprint == right.DefinitionFingerprint &&
            leftBefore.Desires.Count == rightBefore.Desires.Count &&
            leftVector.Effects.Count == rightVector.Effects.Count;
        for (int i = 0; i < leftVector.Effects.Count && equal; i++)
        {
            KLEPDesireEffectTrace a = leftVector.Effects[i];
            KLEPDesireEffectTrace b = rightVector.Effects[i];
            equal = a.DesireStableId == b.DesireStableId &&
                a.Weight == b.Weight &&
                a.SatisfactionBefore == b.SatisfactionBefore &&
                a.SatisfactionAfter == b.SatisfactionAfter &&
                a.PressureBefore == b.PressureBefore &&
                a.PressureAfter == b.PressureAfter &&
                a.Effect == b.Effect;
        }

        Expect(equal,
            "Identical definitions and immutable observations produce identical Desire evidence");
    }

    private static KLEPDesireSystem<DesireContext> StandardSystem(string ownerId)
    {
        return new KLEPDesireSystem<DesireContext>(
            ownerId,
            new[]
            {
                Definition("desire.food", 100f,
                    new ContextEvaluator("eval.food", "1", "food")),
                Definition("desire.safety", 2f,
                    new ContextEvaluator("eval.safety", "1", "safety")),
                Definition("desire.belonging", 0.5f,
                    new ContextEvaluator("eval.belonging", "1", "belonging"))
            });
    }

    private static KLEPDesireDefinition<DesireContext> Definition(
        string id,
        float weight,
        IKLEPDesireEvaluator<DesireContext> evaluator)
    {
        return new KLEPDesireDefinition<DesireContext>(id, "1", weight, evaluator);
    }

    private static KLEPDesireObservationRequest<DesireContext> Request(
        string snapshotId,
        long tick,
        string momentId,
        string contextId,
        DesireContext context)
    {
        return new KLEPDesireObservationRequest<DesireContext>(
            snapshotId,
            tick,
            momentId,
            ContextIdentity(contextId),
            context);
    }

    private static KLEPDesireContextIdentity ContextIdentity(string contextId)
    {
        return new KLEPDesireContextIdentity(contextId, "desire.context", "1");
    }

    private static DesireContext FilledContext(float satisfaction)
    {
        var context = new DesireContext();
        context.Set("food", satisfaction, 1f, "food.evidence");
        context.Set("safety", satisfaction, 2f, "safety.evidence");
        context.Set("belonging", satisfaction, 3f, "belonging.evidence");
        return context;
    }

    private static KLEPDesireAttributionEvidence UnknownAttribution()
    {
        return new KLEPDesireAttributionEvidence(
            KLEPDesireEffectAttribution.Unknown,
            "causality.not-established");
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
            throw new InvalidOperationException(
                $"Assertion {assertions} failed: {message}");
        }
    }

    private sealed class DesireContext
    {
        private readonly Dictionary<string, Entry> entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        internal void Set(
            string key,
            float satisfaction,
            float pressure,
            string evidenceId)
        {
            entries[key] = new Entry(satisfaction, pressure, evidenceId);
        }

        internal Entry Get(string key)
        {
            Entry entry;
            return entries.TryGetValue(key, out entry)
                ? entry
                : new Entry(0f, 0f, $"{key}.absent");
        }
    }

    private readonly struct Entry
    {
        internal Entry(float satisfaction, float pressure, string evidenceId)
        {
            Satisfaction = satisfaction;
            Pressure = pressure;
            EvidenceId = evidenceId;
        }

        internal float Satisfaction { get; }
        internal float Pressure { get; }
        internal string EvidenceId { get; }
    }

    private sealed class ContextEvaluator : IKLEPDesireEvaluator<DesireContext>
    {
        private readonly string key;
        private readonly List<string> calls;

        internal ContextEvaluator(
            string evaluatorId,
            string evaluatorVersion,
            string key,
            List<string> calls = null)
        {
            EvaluatorId = evaluatorId;
            EvaluatorVersion = evaluatorVersion;
            this.key = key;
            this.calls = calls;
        }

        public string EvaluatorId { get; }
        public string EvaluatorVersion { get; }

        public KLEPDesireAssessment Evaluate(
            DesireContext context,
            long desireTick)
        {
            calls?.Add(key);
            Entry entry = context.Get(key);
            return new KLEPDesireAssessment(
                entry.Satisfaction,
                entry.Pressure,
                $"{key} evaluated at Desire Tick {desireTick}.",
                new[] { entry.EvidenceId });
        }
    }

    private sealed class ControlledEvaluator :
        IKLEPDesireEvaluator<DesireContext>
    {
        internal ControlledEvaluator(string evaluatorId, string evaluatorVersion)
        {
            EvaluatorId = evaluatorId;
            EvaluatorVersion = evaluatorVersion;
        }

        public string EvaluatorId { get; private set; }
        public string EvaluatorVersion { get; private set; }
        internal bool Throw { get; set; }
        internal bool ReturnNull { get; set; }
        internal bool DriftVersionDuringEvaluation { get; set; }
        internal int CallCount { get; private set; }

        public KLEPDesireAssessment Evaluate(
            DesireContext context,
            long desireTick)
        {
            CallCount++;
            if (Throw)
            {
                throw new ProbeException();
            }

            if (DriftVersionDuringEvaluation)
            {
                EvaluatorVersion = "drifted";
            }

            if (ReturnNull)
            {
                return null;
            }

            return new KLEPDesireAssessment(
                0.5f,
                1f,
                "Controlled valid assessment.",
                new[] { "controlled.evidence" });
        }
    }

    private sealed class ProbeException : Exception
    {
    }
}
