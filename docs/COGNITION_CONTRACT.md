# KLEP cognition composition contract

This contract defines the first portable production composition for KLEP's
higher-cognition subsystems. It connects already-approved parts; it does not add
a second decision cycle, a universal morality, or a way around Locks.

## Assembled arrangement

`KLEPCognitionComposition<TContext>` retains and consistently wires one explicit
arrangement:

```text
project context and observed causal moments
    -> KLEPEthics<TContext>
    -> KLEPEmotion
    -> KLEPMemory
    -> read-only Memory and Emotion evidence policies
    -> KLEPObserver
    -> next-Tick guidance for an already-eligible Agent target
```

The composition validates that Ethics, Emotion, and Memory use the same exact
designer-named axes. It retains the coordinator, both standard evidence
adapters, and the Observer. Additional Observer evidence sources are injected.
No axis name, preferred emotional position, ethical rule, reward sign, or
project meaning is supplied by this layer.

This is composition ownership, not an exclusive capability sandbox. The caller
still holds the injected public subsystem objects. A trusted host must treat
`Process` as their causal write boundary, must not independently advance its
Emotion or Memory, and must not share Agent-specific mutable cognition state
across several runners.

The assembly has no Unity or editor reference. It neither constructs nor Ticks
a Neuron or Agent. A host injects the composition's Observer through the ordinary
Agent construction boundary.

## Closing one causal experience

`KLEPCognitionCoordinator<TContext>.Process` accepts caller-owned immutable
evidence:

- explicit Ethics evaluation identity, context identity, cause origin, and Tick;
- one exactly consecutive Emotion Tick;
- one strictly increasing Memory Tick;
- ordered Prior, optional During, and Consequence moments copied from actual
  Neuron snapshots; and
- an optional factual Executable lifecycle outcome.

It performs exactly this order:

1. preflight clocks, moment ordering, causal timestamps, duplicate experience
   identity, and subsystem-axis compatibility;
2. evaluate project Ethics and retain its guarded trace;
3. advance Emotion once using that exact evaluated influence; and
4. construct and record one Memory experience containing the factual action
   outcome, Ethics trace, and produced Emotion consequence.

The immutable transition trace retains each subsystem's own Tick and provenance
ID. Their order is causal evidence, not a claim that Neuron, Ethics, Emotion,
Memory, and Observer share one global clock.

Caller-validatable errors are rejected before Emotion or Memory advances. After
that preflight, `Process` captures owner-bound checkpoints of the exact Emotion
and Memory instances before Ethics evaluation. If evaluation, Emotion advance,
Memory recording, or transition construction then throws, both subsystems are
restored in place, the prior `LastTransition` remains published, and the original
exception is rethrown. Snapshot histories and Memory continuation state are part
of that rollback, so the same causal request may be retried deterministically.
The checkpoints are internal composition authority, not public rewind or
persistence APIs. They cannot roll back side effects inside project-owned policy;
Ethics evaluators remain obligated to be pure for identical inputs.

## Observer evidence adapters

The Memory adapter receives only the Observer request's already-eligible root
targets. For each target, project policy may create a factual recall cue and may
interpret the returned recall as one signed, explained contribution. The adapter
calls only pure `KLEPMemory.Recall` and verifies that Memory Tick, snapshot, and
history did not change.

The Emotion adapter copies the current named axes, Tick, position, velocity,
unchanged-position duration, and latest net influence. Project policy may
interpret that copy as one signed, explained contribution per already-eligible
target. The adapter verifies that it did not advance or mutate Emotion.

Both adapters retain policy ID and version, structured evaluation details, and
evidence provenance. A policy may abstain. Neither adapter can expose a blocked
target, open a Lock, emit a Key, advance an Executable, or choose an emotional or
ethical meaning on the project's behalf.

Read-only verification also runs when project policy throws. If that callback
mutated its source and then failed, the adapter surfaces the boundary violation
as `InvalidOperationException`, retains the project exception as its inner
exception, and publishes no partial adapter evaluation trace. Detection is not
rollback: trusted policy is still obligated not to mutate the injected source.

## Candidate Unity Agent construction seam

This section describes Candidate KLEP-UNITY-005, not an approved pure-runtime
rule.

`KLEPNeuronRunner` still owns only observation, one Agent Tick, Unity effect
sinks, and diagnostics. Before initialization it may receive one programmatic
configuration/Observer pair, or discover exactly one owned active
`IKLEPAgentCompositionSource`. A second programmatic assignment is rejected.
The source returns a `KLEPAgentComposition` without receiving the new Neuron as
a mutation handle. Supplying both paths, multiple owned sources, an inactive
source, or a null composition is rejected before Executables are registered.

Runner initialization begins in its own `Awake`, and Unity does not guarantee a
sibling or child source's `Awake` ran first. A source's stable ID and builder
must therefore work from serialized or otherwise preconfigured state. The
runner caches the first successful source result before it asks any behavior
provider to build. A later initialization retry reuses that graph. If the source
call itself throws or returns null, a repaired retry may call it again, so the
failed call must be retry-safe. The engine-free resolver that owns this policy is
covered outside Play Mode for exact-one discovery, programmatic injection,
successful-build caching, failed/null retries, inactive sources, path conflicts,
and source disappearance, replacement, or rename.

The runner then constructs `KLEPAgent(neuron, configuration, observer)`. With no
source it preserves the default Agent and no-Observer behavior. Project-specific
hosts may implement the interface without adding Unity to the cognition layer.
The ZombieTest contains one concrete project-owned source as construction and
diagnostic evidence. That source deliberately has zero ethical rules and
abstaining Memory/Emotion evidence policies, so attaching it cannot polish or
change selection. Constructing actual project Ethics context and closing causal
episodes remain project adapter work rather than fabricated demo evidence.

## Still deferred

- automatic extraction of project Ethics context and complete causal episodes
  from arbitrary game events;
- an explicit external evidence-revision signal when Memory, Emotion, or project
  policy changes without a visible Key-payload change;
- a combiner for Planner, Agent-learning, and other proposal systems beyond the
  current Observer evidence path;
- whole-Agent persistence across Keys, lifecycle, learning, Emotion, Memory,
  Observer, and open experiences; and
- admission, validation, and capability control for model-invented definitions,
  Locks, Keys, or Executables.
