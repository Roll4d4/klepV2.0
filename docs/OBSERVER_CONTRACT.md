# KLEP Observer contract

This contract defines the deterministic Observer boundary. The Observer is a
non-authoritative introspective reasoning service. It validates and maps the
complete recursive Executable catalog, constructs and maintains immutable
evidence-bound self-models of a Neuron, answers higher-level read-only questions
over them, and may provide influence only through accepted eligibility-gated
seams. It is not a scheduler. It returns models, maps, dependency proposals,
projections, and advice; the Agent alone evaluates current Locks, decides, and
fires against Keys stored by the Neuron.

## Structural catalog validation and mapping

The Neuron exposes a monotonically changing catalog revision. Before the Agent
activates a proposed changed revision, or when the Agent explicitly requests a
remap, it supplies the Observer an immutable recursive graph snapshot containing:

- every registered root and stable registration tenure;
- every Goal-owned descendant and layer relationship;
- whether each node is an actual `KLEPGoal` recipe independently from its
  diagnostic Executable kind;
- stable Executable and Lock identities;
- Solo/Tandem modes; and
- each Executable's guaranteed `DeclaredOutputs`.

This structural service exists for every Agent. A deterministic baseline
Observer validates/maps the graph when no project higher-cognition Observer is
injected. Evidence-driven low-confidence deliberation remains optional.

The Observer deterministically validates the authored graph and returns one
immutable assessment bound to the exact revision and graph fingerprint. The
assessment contains validity, diagnostics, an inspectable Key/Lock/Executable
map, and any candidate projections supported by that map. An unchanged exact
assessment may be reused. A changed revision always requires a fresh assessment;
an invalid proposed revision is rejected by the Agent before new runtimes
initialize or fire.

Because every `DeclaredOutput` is guaranteed to have been emitted during a run
accepted as `Succeeded`, no separate completion-effect list exists. The map may
use those IDs as producer/route evidence. It does not assert that an emitted Key
remains present at completion, that several emissions coexist, that a current
Key remains present, that a current Lock is open, or that a run is selected.

Catalog validation is independent of learning confidence. It occurs on revision
change or explicit request in familiar and unfamiliar environments alike.

## Evidence-bound self-models and explicit queries

The Observer may retain an immutable model of a Neuron from a completed Agent
decision. That model is bound to:

- Observer stable identity and version;
- the completed Agent cycle and immutable Key snapshot Tick/wave;
- the exact active accepted catalog revision and graph fingerprint;
- the completed snapshot's deterministic evidence fingerprint;
- the immutable Key evidence supplied for that decision; and
- the exact active accepted structural assessment recorded by the Agent.

An attempted catalog assessment that the Agent rejected is diagnostic evidence,
not the catalog in the self-model. The previous accepted assessment remains the
active structural assessment until a later valid revision is accepted. A retained
older model remains historical evidence; it does not silently become a model of
a newer catalog or Key snapshot.

Explicit deterministic queries over a bound model are independent of confidence
and do not require a low-confidence guidance episode. A query may answer such
questions as which definitions can produce a target Key and which Lock
dependencies those definitions declare. It may return an immutable structural
dependency proposal that preserves, rather than silently resolves:

- every known producer alternative in deterministic order;
- the authored `All`, `Any`, and `Not` Lock-expression structure;
- which positive prerequisites are present in the supplied Key evidence;
- missing producers, negative requirements, and dependency cycles; and
- whether a producer is a root or a Goal-owned descendant that cannot be
  scheduled as an independent root action.

This proposal is an explanation of structural possibility, not an executable
recipe or a claim that a route will succeed. It does not choose one `Any` branch,
assign route cost, order executions, prove that emitted Keys persist or coexist,
or convert Goal-owned children into independent candidates. A query cannot call
`KLEPAgent.Tick`, alter a completed decision, mutate live state or authored Goal
layers, install its result into a Goal, or create a second selection/fire path.
Any later policy that selects, adopts, materializes, or executes a proposal
requires a separate accepted contract.

## Goal Structural Solution V1

The accepted Goal Structural Solution query is the first narrow route-selection
policy over that mapped evidence. A project associates one explicit target Key
with one authored root Solo `KLEPGoal`. That target is desired structural
metadata; it is not inferred from or added to the Goal's `DeclaredOutputs`.
The requesting Goal has no authored layers and no declared outputs, so the query
cannot silently replace or combine an authored child recipe.

The pure solver reads only the exact accepted structural map. Current Keys,
payloads, evidence fingerprints, Lock truth, Emotion, Desire, Memory, learned
expectations, Agent scores, and runtime progress do not participate in route
choice. It searches backward from the target through guaranteed successful
`DeclaredOutputs` and applies this deterministic V1 policy:

- every positive dependency under `All` is retained;
- `Any` chooses a complete alternative with the fewest distinct delegated root
  executions, then the ordinal canonical route as its tie-break;
- one delegated root shared by several dependencies is counted and scheduled
  once in the structural solution;
- `Not` expressions remain explicit runtime conditions because the map has no
  guaranteed Key-removal relation;
- a positive leaf with no mapped producer remains an explicit external runtime
  condition rather than becoming an invented effect; and
- a cyclic path or a producer that is Goal-owned, Tandem, a root Goal, or the
  requesting Goal itself is not a delegable V1 step.

If no complete route through delegable roots exists, the provider returns no
solution. It does not weaken Locks or manufacture a fallback. A solved route is
an immutable dependency-first ordering of exact existing root Solo non-Goal
identities and registration tenures, plus its target, retained runtime
conditions, cost, canonical identity, and diagnostics. It is structural
evidence only: it neither reparents nor clones an Executable, mutates a Goal,
opens a Lock, selects a root, or advances a runtime.

The solution binds the provider identity/version, exact accepted catalog
revision and fingerprint, requesting Goal identity and tenure, target Key, and
every delegated root identity and tenure. Because route choice reads no Key
evidence, the same exact solution may be cached across Key additions, removal,
replacement, payload changes, and Tick/wave changes. A provider identity/version,
catalog revision/fingerprint, requesting-Goal tenure, or delegated-root tenure
change invalidates it before adoption or continued use. `DeclaredOutputs`
remain cumulative successful-run emission guarantees; the solution does not
claim that emitted Keys persist until a later step or coexist with one another.

Only the Agent may adopt this evidence through the separately accepted
structural-Goal execution boundary in `AGENT_CONTRACT.md`.

## Read-only learned expectation boundary

The independent `KLEPLearnedExpectations` authority may maintain a derived
in-memory expectation ledger over explicit factual trials. The Observer may be
injected with only `IKLEPLearnedExpectationsView` and may call the read-only
`QueryLearnedExpectation` boundary while comparing mapped possibilities. It
does not construct, own, record into, reset,
or persist the learned state. The full mutable and critic contract lives in
`LEARNED_EXPECTATIONS_CONTRACT.md`.

The ledger annotates the structural self-model; it never edits that map. A
`DeclaredOutput` remains a categorical successful-run emission guarantee and
is never converted into a probability-one trial, weakened by a failed run, or
confused with final Key persistence. A trial and every query must use a retained
self-model bound to the same Observer identity and version as the learned view;
structurally similar evidence from another modeler cannot be mixed accidentally.

One trial asks whether one named Key was observed following one named mapped
Executable within an exact project-defined context and horizon. It records a
temporal association such as `Wander -> later NearbyHuman`, not a claim that
Wander produced the Sensor Key. Context identity is opaque and exact: a project
may deliberately use one global context or divide evidence as narrowly as its
own vocabulary requires. LearnedExpectations performs no implicit similarity
or generalization; the Observer merely consumes the exact result.

For the zombie example, two different forms of knowledge can therefore sit
beside one another without being collapsed:

```text
empirical:   Wander -- later NearbyHuman: 7/10, confidence 10/14 --> NearbyHuman
structural:  NearbyHuman -- satisfies authored Lock --> EatHuman -- successful
             declared output --> AteHuman
```

The first line is learned association. The second line is authored structure.

The explicit-trial adapter is a trusted factual boundary. Core Key snapshots
currently carry exact Tick, wave, Facts, and payload evidence but no Neuron
subject token, and `SourceRunId` is opaque provenance rather than lifecycle
authority. The adapter must therefore submit prior and consequence evidence
from the same Agent-specific stream and must not share one mutable learned
authority across Agents in this slice. `EvidenceSequence` is the canonical replay
identity; `TrialId`, `SourceRunId`, and evidence IDs explain provenance but do
not form an unbounded in-ledger duplicate registry. Subject-token enforcement,
automatic lifecycle binding, and cross-Agent sharing remain deferred policies.

Trials are `Observed`, `NotObserved`, or `Censored`. Censored evidence advances
the owner-wide evidence sequence and is traced but does not enter the completed
trial count. For `N` completed trials and `H` observed trials:

```text
likelihood = H / N
expectation confidence = N / (N + confidenceScale)
```

The confidence scale is positive and finite and defaults to `4`. Likelihood and
confidence are separate. One observed trial can therefore yield likelihood `1`
with low confidence; many misses can yield likelihood `0` with high confidence
in that estimate. Zero completed trials is explicit unknown, not likelihood
zero. Because finite evidence cannot be mathematical certainty, floating-point
rounding at extreme scales is saturated to the greatest representable value
below `1`. Expectation confidence is not Agent navigation confidence.

Every ledger bucket binds the exact accepted canonical catalog fingerprint,
source Executable identity and root tenure, target Key, context
identity/schema/version/fingerprint, and horizon identity/version. Immutable
results additionally retain the evidence revision. The evidence sequence is
strictly increasing across the owner. A trial is fully validated before the
sequence or any count advances; replayed, out-of-order, owner-mismatched, or
malformed evidence publishes
nothing. Evidence from different accepted catalog fingerprints or source
tenures remains in separate buckets instead of contaminating one estimate.

Expectation is per-edge evidence. It may be displayed beside the existing
recursive structural dependency graph, including for blocked downstream
definitions, but this slice does not multiply edge probabilities into one route
certainty, choose an `Any` branch, rank routes, or install a Goal recipe. A
project policy may translate an immutable expectation result into ordinary
Observer evidence only through the existing already-eligible root-Solo seam.
Expectation itself supplies no score and the Agent remains the only selector.

The ledger is derived runtime state. LearnedExpectations has no Memory
dependency and owns no raw experience. This slice supplies neither a file codec nor independent
persistence; a host that wants continuity must replay the same canonical ordered
trial stream. The owner must submit that stream serially; concurrent Record or
query calls are outside this deterministic first-slice contract. The ledger
retains constant evidence per exact bucket plus only its latest update, but the
number of distinct catalog/context/horizon buckets can grow until a future
project-owned decay or retention policy exists. Context generalization, decay,
cross-Agent sharing, automatic trial discovery, and route-wide probability
remain deferred. The independent raw Desire-effect critic is defined in
`LEARNED_EXPECTATIONS_CONTRACT.md`; it does not alter this exact Key ledger.

## Optional deliberation and influence in the Tick path

After a completed Agent Tick, low confidence may produce a
`KLEPGuidanceRequest`. The Agent may then call the Observer for optional extra
deliberation and prepare one-use advice for the following Agent Tick. This
guidance path is separate from mandatory revision-bound graph assessment.

The Observer receives immutable evidence from that completed Tick and may
prepare advice for the following top-level Agent Tick. It cannot:

- call `KLEPAgent.Tick` or advance a Neuron storage boundary;
- alter the decision that produced the request;
- add, remove, replace, reserve, or consume a Key;
- mutate a Lock, Executable definition, Q-value, lifecycle, or registry; or
- make an ineligible behavior eligible, select an Executable, or fire it.

The supplied context exposes no mutation API. V1 still treats an injected
Observer and its evidence sources as trusted pure code: a callback that has
captured some other mutation handle is contractually forbidden from using it.
The Agent detects boundary/catalog drift after a callback, but this check is not
a capability sandbox.

An optional-deliberation exception is outside the already committed Agent
decision. The Agent queues no partial advice, records that consultation was
attempted, and rethrows the exception.

The Agent continues to emit a guidance request on every completed Tick whose
confidence is at or below its guidance threshold. The V1 frequency policy
consults the Observer once per unchanged uncertainty episode. It consults again
when the environment signature or guidance evidence fingerprint changes, the
ordered eligible-target set changes, prepared advice finds that its target
registration was replaced, a learning update commits, or confidence recovers
and later dips.

## Projection evidence and satisfaction

A project Observer may supply a complete Key-presence state at the explicit
`SuccessfulRunCompletion` horizon. Each request/result is bound to the exact
accepted catalog revision/fingerprint, root tenure, target ID, current evidence
fingerprint, and horizon. Complete results retain immutable presence state and
projector identity/version/provenance; omission therefore means known absent
and can represent removal. Projection is evidence, not selection.

The baseline and unsupported Observers explicitly abstain. Abstention records
unknown projected truth and a reason and contributes exactly zero.
`DeclaredOutputs` alone cannot become a complete final state: an emission may
expire or be removed before success, and emissions at different times need not
coexist. Stale or mismatched results fault before scoring. The Agent applies the
designer policy only after ordinary Locks make a Solo candidate eligible.

For each desired Key/Lock expression and candidate, the inspectable evidence
records current truth, known projected truth or explicit unknown,
projector/map/root-tenure provenance, and any abstention reason. This historical
projected-satisfaction seam uses the compatibility `KLEPAgentDesire` name but is
not the independent Desire subsystem. For a complete projection the Agent, not
the Observer, applies the finite weight and optional pressure and sums
`weight * pressure * (projected - current)`. A projection may therefore
inform attraction without mutating Lock attractiveness or declaring a proposal
to be current world truth.

## Holistic direction

Optional low-confidence deliberation chooses a direction for one-use influence,
not a second action sequence. This restriction governs the Tick-path advice
seam; it does not remove the explicit read-only query service above. The advice
target pool is the guidance request's currently eligible root Solo Goals and
actions. Tandems, Tandem Goals, and Goal-owned children are not independent
polish targets, although all are present in the structural map and may appear in
a dependency explanation.

Ordered, injected evidence sources may contribute one signed finite net value
per target. Evidence is project vocabulary and may represent, for example:

- remembered success, failure, or emotional consequence;
- current emotional stability or an authored mental-health preference;
- physical needs represented by project Keys or another read-only model;
- authored urgency, responsibility, risk, or opportunity.

Core and Observer do not hardcode what health, happiness, fear, hunger, or
morality mean. Each contribution retains its source ID, version, target, value,
and explanation. Sources are evaluated in ordinal stable-ID order; their values
are summed with checked finite arithmetic. The greatest total strictly above a
configurable nonnegative beneficial-direction floor wins; that floor defaults
to zero, and root stable ID breaks a tie. With no supported direction above the
floor the Observer abstains, leaving the Agent free to remain patient or follow
its ordinary local arbitration.

This means that if five eligible Goals have remembered evidence and only one
has a beneficial history, that Goal normally becomes the Observer's direction.
A sufficiently strong, explicitly supplied physical need or authored urgency
may outweigh that memory. Reachability remains absolute: evidence for an
ineligible target cannot polish it.

## Polishing and tarnishing

Advice identifies exactly one root Solo target and records the target's authored
Lock IDs for provenance. Polishing is a separate positive score component; the
authored Lock objects and their attractiveness never change.

The target's pre-Observer score is already complete before polishing. For a
Goal, it may include the optional intrinsic-attraction contribution governed by
`EXECUTABLE_CONTRACT.md`. The Observer neither invokes nor changes that
evaluator; it appends only its existing one-use contribution after eligibility.

The configured polish amount is a nudge, not a command. It may be too small to
cross certainty or beat another eligible target's pre-Observer score. Whether a
future policy should guarantee the Observer's reachable direction is still an
owner decision.

Advice is bound to both the exact environment signature and the exact guidance
evidence fingerprint in the request, and is offered to the next Agent Tick
once. At that Tick:

1. Tandems settle normally without Observer influence.
2. The final environment signature and evidence fingerprint are calculated.
3. Every root Solo is filtered through its Locks.
4. If the signature and fingerprint still match, the exact root registration
   still owns the target ID, and the target remains eligible, the polish is
   appended to that target's score trace.
5. Certainty, current-Solo retention, strict-higher interruption, and stable-ID
   tie rules operate on the resulting effective scores.
6. The advice is consumed whether applied or rejected; the polish has tarnished.

A changed environment is traced as `StaleEnvironment`; changed payload evidence
is traced separately as `StaleEvidence`. Missing, same-ID replacement, and
ineligible targets are also traced and receive no score. Observer influence
never participates in Tandem or Tandem-Goal eligibility or ordering.

The accepted Agent environment signature intentionally remains the unique
visible `(scope, Key ID)` presence set used by Locks and learning. Guidance's
separate fingerprint canonically includes scope, stable Key ID, payload field
names, types, values, and duplicate payload occurrences. It excludes occurrence
authority, provenance, lifetime, and timing metadata. An HP or ammo-observation
payload change therefore stales advice and opens a fresh consultation without
fragmenting the Q table. External Memory or Emotion evidence that changes
without becoming visible Key payload evidence still requires a future explicit
evidence-revision seam.

## Memory boundary

Observer has no compile-time dependency on KLEP Memory. A read-only adapter may
translate a Memory recall into ordinary Observer evidence or submit an explicit
completed expectation trial to `KLEPLearnedExpectations`. Such an adapter owns
the factual trial boundary; neither Observer nor LearnedExpectations infers one
from coincidence or mutates Memory. Observer cannot record, cool, archive,
consolidate, or mutate memories. This keeps Memory usable without Observer and
allows other evidence models to replace it.

## Deferred reasoning

Structural planning is the Observer's revision-bound mapping and query
responsibility; there is no required separate Planner authority.
`DeclaredOutputs` supplies the guaranteed successful-run Key-ID emission
promise. The Observer may retain the accepted map with completed Key evidence
and expose inspectable multi-step reachability, dependency proposals, candidate
projections, and the narrow accepted structural solution above. Complete
successful-completion presence still requires the explicit projection seam.
The Agent evaluates current Locks and remains the only live decision owner.

V1 now defines one Key-target, distinct-root-count, canonical-tie route policy
for empty authored root Solo Goals. Other target forms and costs, expectation-
weighted routes, payload conditions, active deletion planning, guarantees about
Key lifetime or coexistence, cyclic execution, Goal/Tandem delegation, dynamic
recipe manufacture, and routes that invent definitions remain open. A project
policy may reason beyond V1, but it gains no live execution authority by doing
so.

Nora/Aron breadth-versus-depth competition is not required when the complete
graph is explicitly mapped. It remains an optional future search/resource
strategy behind this same boundary, as do model trust/resource allocation,
persistence, neural/asynchronous implementations, and proposal of genuinely new
Keys, Locks, Executables, or physical capabilities. Generated definitions must
pass a separate admission/capability contract before entering a Neuron catalog.

## Required trace

The inspectable record must show:

- catalog revision and graph fingerprint;
- whether validation was revision-triggered, explicitly requested, or reused;
- every validation fault, mapped relation, route/projection, and unknown reason;
- the exact assessment the Agent accepted or rejected;
- each retained self-model's Observer identity/version, completed cycle,
  snapshot Tick/wave, accepted catalog binding, and evidence fingerprint;
- each explicit query kind and target, its exact model binding, ordered producer
  alternatives and preserved Lock structure, current-evidence annotations, and
  missing-producer, negative-requirement, cycle, or ownership diagnostics;
- current/projected desired-expression truth and the map provenance supplied to
  the Agent's weighted-satisfaction calculation;
- whether guidance was requested and whether the Observer was consulted;
- ordered evidence sources and every contribution;
- every eligible target's holistic total;
- the selected direction or abstention reason;
- the request and advice evidence fingerprint and polished Lock IDs;
- next-Tick application, staleness, missing target, changed registration,
  ineligible rejection, or a Tick fault before application;
- pre-Observer score components and the final Observer contribution separately.

Identical catalog revision/graph, definitions, snapshots, evidence,
configuration, time, and seed must produce identical models, query results,
dependency proposals, assessments, projections, advice, and traces.
