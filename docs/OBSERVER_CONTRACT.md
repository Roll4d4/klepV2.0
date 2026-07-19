# KLEP Observer contract

This contract defines the deterministic Observer boundary. The Observer has two
read-only responsibilities: it validates/maps the complete recursive Executable
catalog, and it may provide additional deliberative influence when an Agent
requests guidance. It is not a scheduler. It returns maps, projections, and
advice; the Agent alone decides and fires against Keys stored by the Neuron.

## Structural catalog validation and mapping

The Neuron exposes a monotonically changing catalog revision. Before the Agent
activates a proposed changed revision, or when the Agent explicitly requests a
remap, it supplies the Observer an immutable recursive graph snapshot containing:

- every registered root and stable registration tenure;
- every Goal-owned descendant and layer relationship;
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

## Optional deliberation in the Tick path

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
decision. The
Agent queues no partial advice, records that consultation was attempted, and
rethrows the exception.

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

For each desire and candidate, the inspectable evidence records current truth,
known projected truth or explicit unknown, projector/map/root-tenure
provenance, and any abstention reason. For a complete projection the Agent, not
the Observer, applies the finite weight and optional pressure and sums
`weight * pressure * (projected - current)`. A projection may therefore
inform attraction without mutating Lock attractiveness or declaring a proposal
to be current world truth.

## Holistic direction

Optional deliberation chooses a direction, not a second action sequence. Its
target pool is the guidance request's currently eligible root Solo Goals and
actions. Tandems, Tandem Goals, and Goal-owned children are not independent
polish targets, although all are present in the structural map.

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
translate a Memory recall into ordinary Observer evidence. Observer cannot
record, cool, archive, consolidate, or mutate memories. This keeps Memory usable
without Observer and allows other evidence models to replace it.

## Deferred reasoning

Structural planning is now the Observer's revision-bound mapping responsibility;
there is no required separate Planner authority. `DeclaredOutputs` supplies the
guaranteed successful-run Key-ID emission promise, and the Observer may retain
inspectable multi-step reachability and candidate projections. Complete
successful-completion presence still requires the explicit projection seam
above. The Agent evaluates current Locks and decides which eligible Solo to
advance.

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
configuration, time, and seed must produce identical assessments, projections,
advice, and traces.
