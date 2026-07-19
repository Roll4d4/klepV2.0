# KLEP Ethics contract

This contract defines the first portable, deterministic Ethics springboard.
Ethics gives an action or event contextual meaning and converts that meaning
into an inspectable Emotion influence. It does not claim that any supplied
standard is objectively correct, observe the world, or choose behavior.

## Separation of responsibilities

The current higher-cognition direction is:

```text
Observer or project adapter supplies facts about an action or event
    -> project-owned Ethics policy evaluates those facts in context
    -> KLEP Ethics records the complete judgment and guards provenance
    -> Emotion integrates the resulting influence
    -> KLEPMemory associates experience, feeling, and consequence
    -> future ranking may influence only already-eligible behavior
```

The same physical action may receive different evaluations in different
contexts. KLEP therefore supplies no built-in actor taxonomy, duties, virtues,
victims, threats, factions, or universally good and bad actions. Those are
project semantics.

The contemplated Humans-versus-Zombies policies are intentionally not part of
this slice. Statements such as "a human killing a zombie is good," "allowing a
zombie to kill a human is bad," or "flee while badly injured" belong in a
future project policy and behavior layer. A different project can provide a
different policy or replace the supplied weighted evaluator entirely.

## Context and request

`KLEPEthicsRequest<TContext>` evaluates one project-owned context and records:

- a stable evaluation ID, which becomes the Emotion influence source ID;
- the supplied evaluation Tick;
- whether the evaluated cause occurred internally or externally;
- the target Emotion configuration; and
- an immutable context identity containing context ID, schema ID, and schema
  version; and
- the transient project-defined context supplied to policy code.

Internal and External describe where the cause occurred. They do not identify
the evaluator, grant authority, or change the arithmetic.

KLEP cannot make an arbitrary `TContext` deeply immutable. Projects must supply
an immutable snapshot or value whose evaluator-visible contents do not change
during evaluation. A completed `KLEPEthicsEvaluation` deliberately does not
retain or expose that live object; it retains the immutable context identity,
configuration, trace, and judgment. `KLEPMemory` may copy that stable identity
and complete judgment trace; a project evidence store may use the identity to
retain the complete project snapshot. Context must contain
observed or derived facts; an Ethics evaluator does not make an unobserved claim
true merely by judging it.

The caller owns the evaluation ID and cause origin. Custom evaluator code
cannot replace them. The ID must be unique among influences supplied to one
Emotion Tick; Emotion enforces that collection-level rule.

## Two policy seams

Projects may implement `IKLEPEthicsEvaluator<TContext>` directly. A custom
evaluator declares a stable ID, version, and exact target Emotion axis names,
then returns a fully traced judgment.
For identical immutable context, Tick, and Emotion configuration, it must
return an identical judgment without mutating runtime state.

`KLEPWeightedEthicsEvaluator<TContext>` is the supplied springboard for projects
that want smaller rules and weights. It declares the exact Emotion axis names
it was authored for, an optional bias, and an ordered list of project rules.
Each rule has:

- an ordinally unique stable rule ID;
- a finite weight in `[0, 1]`;
- a project predicate or evaluator;
- a proposed normalized two-axis Emotion impulse; and
- a stable reason code for its result; and
- zero or more stable evidence IDs supporting that result.

The simple `KLEPWeightedEthicsRule<TContext>` covers a predicate plus fixed
impulse. More involved projects may implement the weighted-rule interface or
replace the weighted evaluator.

A minimal project flow is:

```csharp
var protectRule = new KLEPWeightedEthicsRule<EventContext>(
    "protected-target", 1f,
    context => context.ProtectedTarget,
    new KLEPEmotionVector(0.75f, 0.25f),
    "target.was-protected",
    evidenceIds: new[] { "observation.target-outcome" });

var policy = new KLEPWeightedEthicsEvaluator<EventContext>(
    "my-project.ethics", "1", "Valence", "Activation",
    KLEPEmotionVector.Zero, new[] { protectRule });
var ethics = new KLEPEthics<EventContext>(policy);

KLEPEthicsEvaluation<EventContext> result = ethics.Evaluate(
    new KLEPEthicsRequest<EventContext>(
        "evaluation:42", 42, KLEPEmotionInfluenceOrigin.External,
        emotion.Configuration,
        new KLEPEthicsContextIdentity("event:42", "event-context", "1"),
        immutableEventContext));

emotion.Advance(42, new[] { result.Influence });
```

`EventContext` and all of its meaning belong to the project. The KLEP types in
the example supply only the envelope, ordered evaluation, trace, and handoff.

## Deterministic weighted evaluation

The authored rule order is semantic. Provided project rules obey the purity
contract, one evaluation:

1. rejects a target Emotion configuration whose axis names do not exactly
   match the weighted policy's declared axes;
2. traces the bias;
3. evaluates every rule exactly once in supplied order;
4. retains applied, unapplied, and zero-weight rule results in that order;
5. computes each contribution as `applied ? proposedImpulse * weight : zero`;
6. sums contributions in trace order using finite double-precision totals; and
7. clamps only the final two-axis Emotion impulse to `[-1, 1]`.

The judgment exposes the raw totals, whether final clamping occurred, the
bounded impulse, and the complete immutable trace. Each trace entry also copies
its stable evidence references. Conflicting contributions remain visible even
when they cancel to zero. Saturated raw totals remain visible even when the
Emotion impulse is bounded.

KLEP can validate identities, weights, results, and ordered arithmetic, but it
cannot prove that arbitrary project callbacks are pure. A mutable rule object,
delegate closure, clock read, random source, or mutation of evaluator input is a
policy contract violation. When such state is genuinely required, it must be
captured in immutable context and reflected in the evaluator version rather
than hidden in the callback.

## Guarded Emotion handoff

`KLEPEthics<TContext>` validates and invokes the selected evaluator. It rejects
target-axis mismatch before policy code runs, checks evaluator identity,
version, and axes both before and after the callback, and rejects a missing or
untraced judgment. KLEP then
constructs the final `KLEPEmotionInfluence` itself using the caller's evaluation
ID and cause origin plus the evaluator's bounded impulse.

Ethics does not advance Emotion. The owning higher-cognition host decides when
to evaluate context and when to supply the resulting influence to a consecutive
Emotion Tick. Evaluation Tick is retained for inspection; it is not silently
substituted for an observation timestamp supplied inside project context.

## Current authority boundary

This Ethics slice is an isolated pure runtime assembly. It references only the
Emotion assembly. It does not:

- observe or validate world facts;
- define a correct ethical system;
- contain Humans-versus-Zombies policy;
- read or mutate Keys or Locks;
- change eligibility, authored scores, or learned values;
- select, interrupt, or advance an Executable or Goal;
- advance Emotion;
- write long-term Memory; or
- invoke an Observer or Planner.

Any later effect on behavior must occur after ordinary eligibility and requires
an approved higher-cognition ranking contract.

## Still open

- The owning host and exact phase that evaluate Ethics and advance Emotion
  relative to a Neuron Tick.
- The project context schema and policy rules for the Humans-versus-Zombies
  experiment.
- The observation timestamp and causal evidence fields each project context
  needs in addition to the evaluation Tick.
- The host boundary that explicitly links an Ethics evaluation with a Memory
  experience, Emotion transition, and causal Executable without guessing from
  Tick coincidence.
- How competing Ethics, Emotion, Memory, Planner, Observer, and learned
  influences are combined and explained without changing eligibility.
