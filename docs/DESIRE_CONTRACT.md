# KLEP Desire contract

This contract defines the first portable Desire slice. Desire gives digital
form to pull toward project-authored preferred conditions. It is deliberately
weak: it can say what the Agent currently wants, how strongly that want is
pressing, and whether an observed experience relieved or worsened it. It does
not decide what fires. Accepted KLEP-AGENT-011 lets an optional project policy
present one frozen Desire snapshot beside one frozen learned-effect snapshot
to the Agent, which remains the sole selector.

## Authority and separation

`KLEPDesireSystem<TContext>` owns one Agent's ordered Desire definitions and
its latest immutable evaluated snapshot. Each definition has stable identity,
version, authored importance, and one guarded project evaluator. The evaluator
receives transient project-owned immutable context and returns only the current
satisfaction, pressure, explanation, and evidence references. The live context
is never retained in a snapshot.

Desire owns no Key, Lock, Executable, Goal, Neuron, Agent runtime, Q-value,
Observer model, Ethics judgment, or Emotion state. In particular:

- Desire is the pull toward preferred conditions;
- Emotion is the separate felt two-axis state and motion; and
- an experienced Desire effect is satisfaction change, not an Emotion vector.

Neither subsystem automatically derives or rewrites the other. A project may
later connect them only through a separate explicit, inspectable policy.

Desire answers **which experienced conditions are beneficial to this
creature**. It does not assert that a particular action or Goal will produce
them. Independent `KLEPLearnedExpectations` may learn the raw effect that
followed an exact `ActionOwned` transition, while the Observer only queries a
read-only learned view beside its accepted structural map. The critic never
applies Desire Weight or Pressure. The Agent remains the only owner of live
selection and execution. Keeping preferred condition, structural possibility,
learned expectation, and observed consequence separate makes prediction error
inspectable.

The accepted learned-Desire selection seam does not transfer these
authorities. Desire still evaluates only present preferred-condition state;
the critic still learns only raw effects; and the policy is a read-only
comparison supplied to the Agent. The Agent may apply its validated result only
to already-eligible root Solos.

The historical Core type `KLEPAgentDesire` remains a compatibility name for
the existing projected-Key-satisfaction score policy. Its protected signed
behavior is unchanged, but it is not this subsystem and does not store a
creature's Desire state.

## Numeric meanings

For each evaluated Desire:

- `Satisfaction` is finite and normalized to `[0,1]`;
- `Deficit` is derived exactly as `1 - Satisfaction`;
- `Weight` is stable project-authored importance, finite and nonnegative;
- `Pressure` is finite nonnegative current urgency, with zero meaning dormant;
  and
- `Effect` is exactly `SatisfactionAfter - SatisfactionBefore`.

Weight and pressure remain evidence about the creature at each observation.
Desire itself does not multiply Effect into a reward. A transition returns the
complete ordered vector of raw per-Desire effects, including zeros. Desire has
no total utility, valence, Q update, or Goal-priority authority, and positive
or negative raw Effect means only improvement or worsening of the named
preferred condition.

Under KLEP-AGENT-011, an optional project-authored Agent policy may combine the
current frozen Weight and Pressure with an independently learned mean raw
Effect and Confidence for an explicitly bound factual effect action. Its
per-Desire term is:

```text
selectionScale * Weight * Pressure * Confidence * MeanRawEffect
```

`selectionScale` is finite, nonnegative project policy. `Deficit` is not a
multiplier. This is a candidate comparison performed for the Agent, not a
stored Desire value, a transition reward, or a mutation of the Desire system.

## Observation

An observation request supplies:

- an inspectable snapshot label and explicit strictly increasing Desire Tick;
  the stable observation identity is Desire owner plus Tick, and labels may
  repeat without requiring an unbounded lifetime registry;
- the exact observed causal moment ID;
- immutable project context identity and schema identity; and
- transient context input.

The system calls every registered evaluator exactly once in authored order. It
checks evaluator identity and version both before and after project code,
copies explanation/evidence, and validates all numeric results. A null, faulted,
mutated-identity, or invalid evaluator result publishes nothing: Tick and
`CurrentSnapshot` remain unchanged. Evaluation callbacks must be pure for
identical inputs.

The immutable snapshot retains system owner, definition fingerprint, snapshot
and moment identity, Desire Tick, context identity, and every evaluated Desire
state. It does not retain the arbitrary live context object.

## Experienced effects and attribution

`EvaluateTransition` compares two compatible snapshots from the same Desire
owner and exact definition fingerprint. The prior Tick must precede the
consequence Tick, and every Desire/evaluator identity, version, and authored
weight must match. It returns one immutable effect trace per Desire.

Every effect also copies the evaluator's before/after explanation and evidence
IDs so the numeric change remains inspectable after its source snapshots are
gone. It retains no live project context. Every effect carries one explicit
attribution:

- `ActionOwned`: the exact identified factual action run owns the effect;
- `External`: an identified external cause owns it;
- `Mixed`: more than one cause contributed and exclusive ownership is not
  claimed; or
- `Unknown`: the observed change is factual but its cause is not established.

Only `ActionOwned` is eligible for a learned-expectation critic update. That is
a derived property of attribution, not a second stored truth. The independent
critic still updates only when a trusted integration explicitly submits the
complete immutable vector with an owner-bound evidence sequence; Desire does
not train it automatically. The critic learns raw satisfaction effect and does
not apply the retained Weight or Pressure. Any later comparison uses the
current frozen Desire snapshot rather than retroactively weighting the trial.

Attribution is not inferred from Tick coincidence. When a vector is attached
to a Memory experience, its prior and consequence moment IDs must match the
experience boundaries. `ActionOwned` must additionally match the experience's
factual Executable stable ID and run index.

## Memory and cognition boundary

Memory stores a defensive archival copy of optional Desire effect evidence as
a fourth fact beside lifecycle outcome, Ethics, and Emotion. It retains raw
before/after satisfaction, weight, pressure, evaluator explanation/evidence,
effect, attribution, and provenance through detail fading and continuation
state.

Archived Desire evidence does not participate in Memory projector identity,
association, heat, repetition, emotional salience, trauma, recall strength,
eligibility, Observer influence, or learning. Cognition may validate, copy,
archive, and return an already-evaluated vector; it does not call or mutate the
Desire system and adds no state-producing phase to the accepted Ethics to
Emotion to Memory transaction. The Agent's optional KLEP-AGENT-011 comparison
reads the live frozen Desire and critic snapshots directly; it does not score
from a Memory archive.

## Determinism and portability

The Desire assembly has no Unity, Core, Emotion, Memory, Cognition, or learning
dependency. Definitions preserve authored order and snapshots expose immutable
copied collections. Given identical definitions, identity, context, Tick, and
pure evaluator behavior, observations and effect vectors are identical.
