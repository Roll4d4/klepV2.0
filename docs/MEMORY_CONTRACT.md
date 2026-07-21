# KLEP Memory contract

This contract defines the first deterministic, inspectable `KLEPMemory`
vertical slice. It records perceived experience, discovers repeated patterns,
cools working memories, consolidates durable patterns, and returns factual
recall evidence.

It is not imagination, conjecture, a planner, or a second behavior selector.
It never declares a new fact about the world.

## Place in higher cognition

The intended information direction is:

```text
Sensor-observed and internally produced Keys
    -> Neuron snapshots and actual Executable lifecycle
    -> project-owned Ethics evaluation
    -> produced Emotion state and motion
    -> KLEPMemory experience and association
    -> project-policy Observer evidence for already-eligible Executables or Goals

already-evaluated Desire effect evidence
    -> optional copied fact archived with that KLEPMemory experience
```

Desire is the pull toward project-authored preferred conditions; Emotion is the
separate felt two-axis state and motion. The optional Desire vector is archival
evidence, not a fourth cognition phase and not an Emotion vector.

Memory is a pure sibling assembly with one-way references to Core, Ethics,
Emotion, and Desire. Core does not reference Memory. There is no `SLASHBridge`,
Unity lifecycle method, wall clock, random source, static global state, or
mutable ScriptableObject runtime state in this layer.

## One experience

`KLEPMemoryExperience` is one ordered perceived episode. It requires:

1. one `Prior` moment;
2. zero or more `During` moments; and
3. one `Consequence` moment.

Every moment copies an immutable Neuron Key snapshot. A moment retains every
observed occurrence, including stable Key ID, scope, lifetime, occurrence
authority, payload, issued Tick, activated Tick, and source. Thus two
occurrences of one Key remain two pieces of full-detail evidence.

The experience also may retain:

- one factual terminal Executable outcome;
- copied Ethics evaluation identities and complete traces;
- the resulting Emotion position, velocity, integrated velocity, net
  influence, unchanged-position duration, and emotional swing; and
- one copied, already-evaluated Desire effect vector containing raw before and
  after satisfaction, authored weight, prior and consequence pressure,
  evaluator explanations and evidence IDs, raw satisfaction effect,
  attribution, and provenance for each Desire.

All evidence must fit inside the declared experience timeline. A caller cannot
attach an action that completed outside the Prior-to-Consequence interval or an
Ethics evaluation from an unrelated Tick and silently call it causal. A
produced Emotion consequence must advance to a later Emotion Tick, and Ethics
evidence associated with that consequence cannot postdate the produced Emotion
state it is claimed to have influenced. A Desire vector's prior and consequence
moment identities must exactly match the experience boundaries. An
`ActionOwned` effect must also identify the experience's factual Executable
stable ID and run index; Tick coincidence cannot manufacture attribution.

When several completed experiences are delivered on one Memory Tick, Memory
applies them by Consequence Tick and wave, with ordinal experience ID used only
as an exact-time tie-break. Names therefore cannot reverse which event is
"most recent" or reheat an earlier event after a later trauma.

### Four facts, not competing truths

Action outcome, Ethics, Emotion, and Desire answer different questions:

- the Core lifecycle says what the Executable actually did;
- Ethics records how that event was evaluated in its supplied context;
- Emotion records the felt two-axis state and motion that evaluation helped
  produce; and
- Desire records pull toward named preferred conditions and how their
  satisfaction changed across the experience.

Only `KLEPExecutableState.Succeeded` with the matching `Succeeded` exit reason
is success. Failed, Cancelled, and Faulted remain distinct non-success outcomes.
A favorable ethical judgment or desirable emotional result cannot rewrite that
fact. An unpleasant result likewise cannot turn an actual success into a
failure. Desire does not alias, derive, or substitute for Emotion: a raw
satisfaction change is not a felt-state vector, and neither subsystem
automatically rewrites the other.

## Projector slides and association

The transparent-projector metaphor is represented as a sorted set of cells:

```text
(Prior | During | Consequence, Key scope, stable Key ID)
```

Duplicate occurrences collapse only for this projector comparison. The full
episode continues to retain every occurrence and payload.

Keeping causal role in the cell is necessary. `Hungry -> Fed` and
`Fed -> Hungry` contain the same two Key identities but are opposite
experiences. They must not become one pattern.

Each cluster retains a hit count for every phase-aware cell. A cell becomes a
core cell when its hit frequency reaches the configurable core-frequency
threshold. This is the stacked-transparency effect: recurring details become
dark and stable; incidental details remain faint.

The initial association policy is:

- an experience can reinforce only a cluster with the same factual action
  stable ID;
- similarity is Jaccard overlap across the complete phase-aware projector;
- an explicit reversal guard rejects a pattern whose Prior and Consequence
  cores swap direction, even when much of the surrounding context is shared;
- the configurable similarity threshold decides whether the experience joins;
  and
- equal matches resolve by ordinal cluster ID.

Recall still compares a current cue only with Prior cells, because current state
is the premise from which an action may be considered. During and Consequence
cells remain accumulated outcome evidence rather than current-state premises.

Desire evidence does not enter projector cells, cluster identity, similarity,
the reversal guard, or association. It remains a separate fact attached to the
retained episode.

## Heat, repetition, and cooling

Heat has exactly two meanings in this slice:

- freshness; and
- repetition.

Emotional intensity is salience, not heat.

Desire pressure and effect are also not heat, repetition, or emotional
salience. Recording Desire evidence cannot heat, consolidate, archive, evict,
or make a cluster indelible in this slice.

The caller advances Memory with explicit strictly increasing integer Ticks.
There is no `Time.deltaTime`. Elapsed Ticks cool each retained cluster at its
configured rate. Recording a first experience supplies the configured initial
freshness heat; reinforcing an existing pattern supplies freshness plus the
configured repetition increment. Heat is clamped to the configured maximum.
If a positive elapsed cooling amount cannot produce a smaller representable
Single value, heat snaps to exact zero and the transition records the actual
removed heat. This is the same finite-progress rule used by Emotion friction;
a valid positive rate cannot create an immortal hot memory through rounding.

When heat reaches zero:

- an ordinary unqualified cluster may be forgotten;
- a repeated or sufficiently salient cluster remains in deep storage; and
- an already archived cluster loses only its hot working presence.

Working-memory and archive capacities are explicit policy. Pressure resolves
deterministically and emits a transition identifying what was displaced or
evicted. Archive capacity is a soft limit only when every excess archived
cluster is indelible; working capacity is always a hard limit.

## Salience and trauma

`SwingMagnitude` is the unsigned distance between the starting and produced
Emotion positions. It deliberately does not call either direction morally good
or bad. Equal positive and negative swings receive equal retention treatment.

A swing at or above the configurable trauma threshold immediately archives its
cluster. This does not add heat. A one-off trauma therefore remains available
to a matching recall without being held perpetually in hot working memory.

Repeated trauma increments a separate count. At the configured repetition
threshold, the cluster becomes indelible under automatic archive eviction.
"Indelible" is scoped to the lifetime and persistence of that Agent's Memory;
explicitly deleting the Agent or save still deletes the mind that held it.

## Detail fading

New experiences preserve full Key occurrence and payload context. Retained
episodes older than the configured detail window are replaced by immutable
gists containing stable phase, Key scope, and Key identity. Payload,
occurrence provenance, timestamps, source, and lifetime are absent rather than
replaced with invented defaults.

Fading never erases the episode's factual lifecycle outcome, Ethics trace,
produced Emotion consequence, or copied Desire effect vector. Desire's raw
before/after satisfaction, weight, pressure, evaluator explanation/evidence,
effect, attribution, and provenance remain intact even when Key occurrence and
payload detail fades. Every episode
has already contributed to the cluster's aggregate projector frequencies,
outcome counts, emotional sums, first/last Tick, repetition count, and trauma
count; Desire evidence contributes to none of those clustering, heat, salience,
or recall calculations in this slice.

Each cluster keeps bounded concrete examples in two independent views:

- recent episodes, newest first; and
- memorable episodes, strongest emotional swing first.

This supports the intended ice-cream-shop shape: the newest visits remain the
easiest concrete examples to retrieve, an unusually intense visit remains
memorable, and the other visits survive as accumulated knowledge of the
pattern and its consequences.

## Emotional preference

An emotional preference is caller-owned policy with:

- exact designer-named axes;
- a desired position;
- a position stability radius; and
- a maximum speed considered stable.

This is not a universal pull toward `(0,0)`. A project may prefer `(0.4, 0.1)`,
`(-0.1, 0)`, or any other valid point. Position alone is not called stable: an
emotional body moving rapidly through the desired point receives lower
stability affinity than one resting there.

Preference affinity is computed from the root-mean-square distance of the
cluster's actual produced Emotion positions to the desired point, plus the
mean of their non-cancelling speeds. It is deliberately not computed only from
the mean position or mean velocity: opposite outcomes must not cancel into a
fictional memory of repeatedly arriving at the preferred state at rest.

## Recall

`Recall` is a pure associative query. A cue contains current perceived Key
identities, an optional exact action stable ID, and an optional emotional
preference. Recall compares cue Keys with a cluster's recurring Prior cells and
returns inspectable components for:

- cue similarity;
- repetition strength;
- current freshness;
- emotional salience;
- optional produced-state stability affinity; and
- the resulting candidate recall strength.

The current candidate score exposes each component separately. Repetition is
`N / (N + configured scale)`; freshness is normalized retained heat; emotional
salience is normalized peak unsigned swing; and optional preference affinity
combines the non-cancelling position-distance and speed evidence described
above. The candidate recall strength is their arithmetic mean with cue
similarity. These weights are recall policy, not evidence and not eligibility.
If a cluster recorded no produced Emotion consequence, a preference cue adds
no component at all; absence of evidence is not treated as a neutral result.

Recall does not change heat, recency, repetition, snapshots, or persistence
state. Looking at Memory through a debugger or visualizer cannot strengthen it.

Associative recall is non-authoritative. It may return a historical action that
is not currently eligible. The cognition/Observer evidence seam therefore
receives the ordinary Lock-eligible action set first, requests Memory evidence
only for that set, and delegates the meaning of recall strength to explicit
project policy. Memory may help rank valid choices; it may never open a Lock or
make an invalid action eligible.

A read-only project adapter may also translate exact completed experience
evidence into an explicit `KLEPLearnedExpectations` trial. Memory remains the
owner of remembered facts; it does not estimate likelihood, raw Desire effect,
variance, prediction error, or confidence and it does not mutate the learned
authority. Existing faded phase marginals are not a
complete replay log for acquired-Key or payload-transition trials, so this slice
does not claim that Memory can reconstruct every expectation after restart.

Memory returns no `KLEPKeyFact`, stages no output, and produces no Key. External
world observations enter through Sensors; accepted initialization and
Executable output boundaries may also produce ordinary Key occurrences.
Memory itself uses neither route. If a project later wants "remembering" to
become internal stimulus, an explicit Sensor adapter must observe a recall
result and publish an ordinary Key at a normal Key boundary.

## Persistence and lifetime

`CaptureState()` returns versioned, owner-bound immutable Memory-subsystem
state. `Restore(state)` validates and resumes:

- owner ID and Tick;
- exact configuration;
- next deterministic cluster sequence;
- all retained clusters and phase frequencies;
- working/archive/indelible state;
- outcome and emotional accumulator state;
- retained exemplars;
- each exemplar's optional copied Desire effect evidence;
- every seen experience ID;
- the current Tick's last immutable transition trace; and
- the bounded immutable snapshot-history window used by diagnostics and
  visualization.

Restore validates current and historical cluster identities, retention
capacities, causal exemplars, trauma/indelibility relationships, non-cancelling
Emotion aggregates, transition traces, and snapshot chronology. The history
tail is canonicalized from the same current clusters and transition trace, so
the public current snapshot and its history tail cannot disagree.

Emotion vectors and their magnitude enter this boundary as IEEE-754 Single
values, while the cluster's consistency proof uses Double aggregates. The
proof therefore permits one relative Single representation step when checking
non-cancelling position and speed bounds. This is representation tolerance,
not semantic slack: zero or another materially impossible aggregate still
cannot impersonate the retained vectors.

Restore does not emit Keys, rerun Ethics, advance Emotion, resume an Executable,
or alter Agent learning. Offline wall time causes no decay unless the host
explicitly advances KLEP Tick after restore.

The pure assembly does not choose a path or file format. A project-owned
adapter may encode the state as binary, XML, JSON, or another format and should
write it atomically. Public validated constructors expose every Memory-owned
layer needed for an adapter to reconstruct a decoded state graph before calling
`Restore`. A decoded `KLEPMemoryState` must supply its schema version explicitly;
construction never assumes that an unversioned payload is current. A decoded
Desire effect vector must also carry the complete ordered effect records. Its
definition fingerprint is reconstructed from that manifest, so dropping,
reordering, or substituting a Desire definition is rejected before restore.
This first slice supplies exact continuation state and a rehydration boundary,
not a built-in general file codec.

`KLEPMemoryState.CurrentSchemaVersion` is `2` because the retained episode shape
now includes optional Desire evidence. Restore keeps its exact-version boundary:
version 1 continuation state is intentionally rejected rather than guessed
forward. This schema change adds neither a file codec nor an implicit migration.

Restoring the whole Agent requires host composition with current Keys,
Executable lifecycle, Emotion body, and Agent learning. Logging out may persist
all of those systems. Removing a dead Agent may deliberately persist none of
them.

## Deferred boundaries

The following are intentionally not invented by this slice:

- persistence of an experience that has a Prior or During moment but has not
  yet received a factual Consequence;
- isolated causality for one Tandem inside a shared wave;
- the file codec and atomic save transaction for a whole Agent;
- a proposal combiner beyond the current eligibility-gated Memory and Emotion
  Observer evidence adapters, including Planner and learned proposals;
- mind-wandering policy or autonomous recall scheduling;
- a recall Sensor that turns selected historical evidence into a new internal
  stimulus; and
- automatic deletion policy for indelible memories beyond explicit Agent/save
  destruction.

These are higher-cognition contracts, not implementation details to hide in a
Memory container.
