# KLEP Learned Expectations contract

Status: approved pure-runtime contract under accepted `KLEP-EXPECT-001`,
`KLEP-EXPECT-002`, and `KLEP-AGENT-011`, plus the unchanged evidence meanings
of `KLEP-OBS-013` through `KLEP-OBS-015`.

## Authority

`KLEPLearnedExpectations` is the one mutable authority for learned outcome
knowledge in this slice. It is engine-free and its mutable ownership is
independent from Observer, Desire, Memory, Agent execution, and selection. Its
assembly consumes immutable Core, Observer-evidence, and Desire-effect contract
types; that compile-time vocabulary grants none of those systems mutation
authority over the critic and grants the critic no authority over them. It may
be constructed and retained by a project composition root, but no Observer
constructs, resets, records into, or persists it.

An Observer may receive only `IKLEPLearnedExpectationsView`. That interface can
query exact later-Key associations and capture immutable snapshots. It exposes
no record, reset, replay, or persistence operation. The injected view must be
bound to the same stable modeler identity and version as the Observer because
its exact buckets refer to that Observer's accepted structural self-model.
Sharing this identity binds evidence; it does not transfer mutable ownership.

The Unity Observatory may retain only
`IKLEPLearnedExpectationsDiagnosticView`, which combines the two read-only
query/snapshot interfaces and exposes no writer method. The project composition
root retains the concrete authority. Drawing captures immutable snapshots and
never trains the critic or invokes Observer reasoning.

## Exact later-Key associations

The existing empirical ledger semantics are preserved. One explicit factual
trial asks only whether a named Key was observed following a named mapped
Executable within an exact project context and horizon. It is temporal
association evidence, not proof of causation or a replacement for
`DeclaredOutputs`.

Buckets bind the exact accepted catalog fingerprint, source Executable and
root tenure, target Key, observation meaning, context identity, and horizon.
Trials are `Observed`, `NotObserved`, or `Censored`. With `N` completed trials
and `H` observed trials:

```text
likelihood = H / N
confidence = N / (N + confidenceScale)
```

Zero completed trials is explicit unknown. Censored evidence advances the
sequence and revision but not completed counts. The positive finite confidence
scale defaults to `4` and belongs to `KLEPLearnedExpectations`, not
`KLEPObserverConfiguration`.

The owner-bound evidence sequence is strictly increasing. A trial is fully
validated before mutation. Replay, out-of-order evidence, owner mismatch,
malformed evidence, or a self-model bound to another identity publishes
nothing. Canonical replay reconstructs the same derived state. There is no file
codec in this slice.

## Raw Desire-effect critic

The critic accepts only an explicit `KLEPLearnedDesireEffectTrial` containing a
complete immutable `KLEPDesireEffectVector` whose attribution is
`ActionOwned`. External, Mixed, and Unknown effects are rejected. The factual
integration that establishes the exact completed action/run and evaluates the
Desire transition remains outside this critic; the critic never invents causal
ownership.

One bucket is the exact tuple:

```text
Desire owner ID
Desire definition fingerprint
action stable ID
Desire stable ID and version
evaluator ID and version
prior context ID, schema ID, and schema version
```

The prior context is the conditioning context: it means "when this action was
taken from this exact project context." Consequence context is retained in the
factual Desire vector but is not silently merged into the bucket identity.
Context similarity or generalization is not inferred.

For every raw per-Desire `Effect = SatisfactionAfter - SatisfactionBefore`,
the critic performs a deterministic Welford online update and retains:

- support `N`;
- mean raw effect;
- sample variance `M2 / (N - 1)`, or zero while `N < 2`;
- the latest prediction error `observedEffect - priorMean`;
- confidence `N / (N + confidenceScale)`;
- latest owner-bound evidence sequence and critic revision.

The complete vector is preflighted before any bucket mutates. Duplicate exact
buckets in one vector, replay, out-of-order sequence, owner mismatch, non-finite
math, or non-ActionOwned attribution rejects the whole update. The later-Key
ledger and Desire-effect critic use separate revisions and evidence sequences;
training either cannot advance the other.

The critic consumes the raw effect only. It never multiplies by the Desire's
authored Weight or current Pressure, invents terminal value, aggregates effects
into reward, changes Emotion, opens a Lock, marks behavior eligible, ranks a
candidate, or selects/executes anything. Desire says what matters; learned
expectations estimate what followed. Accepted KLEP-AGENT-011 permits an
optional Agent policy to combine one frozen read-only critic snapshot with one
frozen current Desire snapshot only after eligibility; that consumer policy
does not become critic authority.

## Read-only learned-Desire selection consumption

One Agent Tick may freeze one immutable Desire-critic snapshot for an optional
project-authored learned-Desire policy. The policy is invoked once over the
already-eligible ordered root Solo set. Tandems and ineligible roots are never
queried. The policy receives no mutable critic handle and cannot record a
trial, reset evidence, advance an evidence sequence, or cause the critic to
capture a newer snapshot during that comparison.

The project supplies an explicit candidate-root stable ID to factual
effect-action stable ID binding. A Goal descendant is not inferred as its
root's effect action. The bound action ID is the same semantic identity stored
in the KLEP-EXPECT-002 bucket; when the meaning of that action's effects
changes, the project must issue a new stable ID rather than reuse incompatible
evidence.

For each authored Desire entry, selection looks up one exact bucket using the
bound action ID and the current snapshot's Desire owner, definition
fingerprint, Desire/evaluator identities and versions, and context
identity/schema/version. Unknown or support-zero evidence abstains with a
traced zero. Current zero Pressure also abstains at zero without requiring a
bucket lookup. Known evidence contributes its retained mean raw Effect and
Confidence to the Agent formula:

```text
selectionScale
* current Weight
* current Pressure
* learned Confidence
* learned MeanRawEffect
```

`selectionScale` is authored, finite, and nonnegative. `Deficit` and sample
variance are not multipliers in this slice. Variance remains visible evidence
for a future risk/cost policy. Negative learned means remain negative; the
critic and policy do not clamp them into positive attraction.

The Agent validates exact owner, fingerprint, context, action, Desire,
evaluator, policy, snapshot-revision, candidate-tenure, and catalog bindings
before applying the checked finite sum. Any mismatch or fault aborts before
selection. Full applied and abstaining bucket evidence remains in the Agent
decision/fault trace. The learned component is placed after existing projected
satisfaction and before final one-use Observer polish. The Agent alone applies
it and keeps the existing equal-score retention and strict-higher interruption
rules.

## Bounded meaning and retention

Each exact bucket retains constant aggregate state rather than unbounded trial
history. The number of exact buckets can still grow until a project-owned
retention, decay, generalization, or persistence policy is accepted. This slice
does not define cross-Agent learned-model sharing, route-wide probability,
conditional causal discovery, automatic Memory replay, or critic-based multi-
step route scoring. KLEP-AGENT-011 defines only the direct explicitly
bound candidate contribution; variance/risk costs, added commitment math,
automatic root-to-effect-action inference, and critic-driven eligibility remain
deferred.

## Regression evidence

`KlepLearnedExpectationsSmoke` proves independent ownership, the Observer's
read-only seam, deterministic raw-effect mean/sample-variance/prediction-error
math, exact context isolation, separate ledgers, immutable snapshots, and
atomic rejection. `KlepObserverSmoke` preserves the accepted exact later-Key
association, replay, owner binding, structural separation, and
non-authoritative behavior tests.
