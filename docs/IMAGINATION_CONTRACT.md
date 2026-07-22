# KLEP Imagination contract

Status: approved V1 foundation and bounded route-admission demonstration under
`KLEP-IMAGINATION-001` through `KLEP-IMAGINATION-003`. This contract defines
proposal, capability, sandbox, and admission authority. It does not claim that
a language model is connected, trained, or trusted, and it does not authorize
generated source code.

## Purpose

Deterministic structural search answers questions over capabilities that the
creature already has. Imagination begins only when that accepted map is
insufficient or when an external reasoner can usefully suggest a different
binding of an existing physical primitive.

The distinction is deliberate:

- planning asks, "Which known path reaches this known end?";
- strong Imagination asks, "Can this known audited verb be bound in a new,
  closed way?"; and
- weak Imagination asks, "What concept, relation, or physical verb may be
  missing?"

The first two may eventually produce ordinary Executable work. The third may
produce research, authoring, training, or review material, but never live work.

## Two proposal channels

### Strong Manifest

A Strong Manifest is closed data. It contains:

- the exact request fingerprint;
- a display name and explanatory hypothesis;
- one admitted capability stable ID and version; and
- exactly the typed arguments declared by that capability.

It does **not** contain an Executable stable ID, kind, mode, score, Lock,
`DeclaredOutput`, callback, type name, verifier, source-code fragment, file
path, URL, reflection target, assembly, shell command, or arbitrary extension
property. Those facts belong to trusted project code.

A strict compiler rejects unknown JSON properties, duplicate properties,
unknown or missing arguments, non-finite numbers, out-of-range values, unknown
capabilities or versions, oversize documents, and malformed JSON. Canonical
content produces a deterministic proposal hash and derived Executable ID.

Compilation proves only that the proposal is a valid binding of an admitted
descriptor. It does not prove that the hypothesis is true or useful.

### Weak Conjecture

A Weak Conjecture may describe a new Key, Lock, relation, behavior recipe,
physical capability, success criterion, or other semantic gap. Its `details`
payload is intentionally open enough to preserve inventions that the current
strong schema cannot express.

Weak means non-authoritative, not unimportant. A Weak Conjecture:

- cannot be compiled into a `KLEPExecutableBase`;
- cannot be registered, scored, selected, leased, or fired;
- cannot declare a factual output or open a Lock;
- cannot promote itself or define its own validator; and
- remains immutable proposal/corpus evidence until a developer or separate
  trusted authoring process creates and admits the missing definition or
  capability.

This is the channel through which a model may genuinely propose new semantics
without KLEP pretending that language is already a physical ability.

## Capability catalog

One project-owned immutable catalog supplies admitted capabilities. A catalog
entry binds:

- stable capability ID and version;
- an explicit descriptor fingerprint;
- a complete trusted `KLEPExecutableDefinition` template;
- an ordinal, closed argument schema with Boolean, Int64, finite Double, or
  Text values and optional numeric/text bounds; and
- a trusted factory that creates one fresh capability runtime.

The descriptor owns the mandatory Locks, kind, execution mode, base score, and
guaranteed outputs. V1 descriptors may materialize only root Solo non-Goal
Executables. The model cannot weaken a guard or strengthen an output claim.

The catalog fingerprint is derived from the ordered capability identities,
versions, descriptor fingerprints, and argument schemas. A compiled Manifest
is bound to that exact catalog fingerprint and descriptor fingerprint.
Materialization against a different binding fails closed.

## Grounded runtime

Materialization creates an ordinary `KLEPImaginedExecutable` around a fresh
trusted capability runtime. The runtime receives only:

- the immutable Key snapshot;
- exact cycle, wave, and run identity; and
- the compiler-validated immutable arguments.

It receives no Neuron, Agent, Key store, catalog mutation API, Unity object,
file system, network client, reflection surface, or model callback.

A capability result may expose a bounded immutable intent for a trusted host.
The generic wrapper binds that intent to the exact capability, Executable,
cycle, and run. Physical Unity work remains a post-Agent host effect, just like
the accepted zombie movement and bite adapters.

On `Succeeded`, the capability result must provide exactly one payload for
every descriptor-owned `DeclaredOutput` and no other output. The wrapper emits
those definitions through the ordinary `KLEPExecutionContext`; Core performs
its existing exact-definition, scope, output, lifecycle, and visibility checks.
`Running` and `Failed` results cannot emit outputs.

This is an internal truthfulness guarantee: it proves that a successful
capability run kept its declared KLEP promise. It does not establish an
external-world claim unless the trusted capability's success rule grounded and
verified that claim.

## Authority and timing

The model adapter and proposal compiler operate outside `KLEPAgent.Tick`.
Parsing, rejection, caching, sandboxing, review, and model timeout mutate no
Neuron or Agent state.

The reusable foundation stops at fresh materialization. Under
`KLEP-IMAGINATION-003`, one project-owned demonstration host may take the
additional bounded sandbox and admission steps defined below. This does not
grant the compiler, materializer, sandbox, diagnostics, or any future model a
general catalog mutation capability.

The admission host stages an accepted fresh Executable only through the
existing guarded Neuron catalog API outside `KLEPAgent.Tick`. The following
Agent boundary validates the changed catalog normally. Observer mapping remains
evidence; the Agent remains the sole owner of eligibility, selection,
interruption, lifecycle, and firing.

No per-Tick model inference is required. A host should prefer deterministic
structural search whenever the accepted map already contains a solution and
deduplicate model requests by exact problem and catalog fingerprints.

## Bounded route sandbox and admission

Cover/Route Learning V1 deliberately uses one checked-in, hand-authored Strong
Manifest rather than a model adapter. The host creates an immutable request only
after an accepted structural map exists. That request identifies the exact
Neuron, accepted structural-map fingerprint, capability-catalog fingerprint,
route problem, and target `RouteCompleted` Key. The JSON fixture's request token
is bound to that exact request before compilation. After compilation, the host
must independently reject a Manifest whose `RequestFingerprint` or
`TargetKeyId` differs from the active request; the generic compiler validates
their shape but does not own that project comparison.

The admitted capability is a trusted project verb for following at most three
bounded planar waypoints toward the observed route target. Its descriptor, not
the Manifest, owns:

- root `Solo` Action kind and authored score;
- Ground, `RouteProblem`, and edge-safety Locks;
- the arena bounds, movement rate, collision radius, intent schema, and factual
  arrival rule;
- the finite waypoint, arrival-radius, and maximum-Tick argument bounds; and
- the one-cycle `RouteCompleted` successful output.

The Manifest may bind only its route label, waypoint coordinates, arrival
radius, and maximum Tick count. It cannot author collision handling, weaken a
Lock, select its own score, declare another output, choose its verifier, or
apply Unity movement.

Compilation is followed by exactly four deterministic sandbox trials, one per
authored central-wall scenario. Every trial receives a fresh disposable
materialization, scratch Neuron, and scratch Agent supplied with synthetic
project-declared Ground and `RouteProblem` observations. A deterministic
sandbox host applies each runtime's bounded intents to isolated planar state
using the authored arena and wall geometry expanded by the zombie's collision
radius. It records every exact cycle/run intent, collision or stall, path
length, terminal lifecycle result, declared output, and fitness. V1 fitness is
the project-authored finite combination of successful completion, route
efficiency, and absence of stalls.

The bounded admission ledger retains the four unique results and alone owns
the explicitly labeled admission measures. `Sandbox support` is the number of
unique retained trials and is capped at four. `Sandbox confidence` is
`support / (support + 4)`, so the complete four-trial suite displays `0.5`;
this bounded measure is not a probability of live success. The ledger uses a
finite Welford update for mean and sample variance over each trial's `[0,1]`
project fitness. Admission requires exactly four retained trials; every trial
must succeed collision- and stall-free with exact `RouteCompleted`
lifecycle/output evidence; minimum measured body-surface clearance must be at
least 90 percent of the descriptor-authored clearance; and mean fitness must
be at least 0.65. Those fields, thresholds, and every rejection reason are
explicit diagnostic evidence used only for the admission verdict.

The sandbox neither moves a Unity object nor mutates a live Neuron, Agent,
Memory, Desire, Emotion, Ethics, or learned-expectation owner. A rejected
proposal is retained as evidence and never registered live. An accepted
proposal does not promote a sandbox object: the host discards all stateful
sandbox runtimes, rechecks the immutable catalog binding, materializes a fresh
runtime, binds the already-registered trusted Unity sink, and stages the root
between complete live Tick boundaries. The next Agent structural assessment may still
reject the catalog change. Only an accepted live tenure can become eligible,
and only while its descriptor-owned Locks are true.

Sandbox acceptance predicts that the admitted binding is structurally valid
and succeeds in the bounded model. `Sandbox support` and `sandbox confidence`
belong only to that four-trial admission ledger. They are not a factual claim
about the Unity world, a Memory episode, learned-expectation evidence, Agent
confidence, or attraction, and they may not influence ordinary selection after
admission. A separately observed live route trial preserves its own problem,
action, run, cycle, target, outcome, and fitness identities and owns only that
factual live outcome and live fitness. Cover/Route Learning V1 does not update
`KLEPLearnedExpectations` from either sandbox or live route trials.

## Equipment relation

Equipment-provided behavior follows the same grounding principle but does not
need to be model-generated. A gun may own authored provider/factory data for
`Fire` and `Reload`; equipping contributes fresh root Executables to one
creature's Neuron. The gun does not become an Agent and does not own the
creature's Desire, Ethics, or selection.

Weapon state and line-of-fire are factual host observations. Fire produces a
typed intent. A verified discharge, impact, damage, and death are distinct
facts and must not be collapsed into one optimistic `DeclaredOutput`.
Friendly-fire prevention may close a Lock; Ethics evaluates any actual result
that nevertheless occurred. These are separate authorities.

## Required evidence

The diagnostic record for a proposal must retain:

- request and accepted structural-map fingerprints;
- proposal channel and raw/canonical hashes;
- model/adapter identity supplied by the host;
- exact capability catalog and descriptor bindings;
- every parse, schema, argument, staleness, and materialization rejection;
- the derived Executable definition for a Strong Manifest;
- each fresh materialized runtime identity/admission tenure;
- any sandbox or live trial evidence; and
- the factual result used by a later human or project promotion policy.

Diagnostics are outward-only and cannot retry, compile, admit, register,
select, or fire a proposal merely because it is being viewed.

## CoreContract acceptance

`KlepImaginationSmoke` must prove at minimum:

- property order does not change a Strong Manifest hash;
- unknown capability/version, extra properties, malformed or out-of-range
  arguments, non-finite numbers, and oversize documents reject atomically;
- a Weak Conjecture parses as immutable evidence but cannot compile;
- the model cannot author Locks, outputs, scores, callbacks, or verifiers;
- materialization rejects catalog/descriptor drift;
- two materializations create distinct Executable and runtime instances;
- closed Locks remain closed after materialization;
- `Running` and `Failed` cannot emit outputs;
- `Succeeded` must provide every descriptor-owned output and no extra output;
  and
- no parse, compile, rejection, or materialization step mutates a live Neuron.

Unity import, live model integration, sandbox quality, and Play Mode remain
separate validation levels.
