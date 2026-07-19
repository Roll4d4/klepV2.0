# KLEP Observer contract

This contract defines the first deterministic Observer slice. The Observer is
higher reasoning that an uncertain Agent may consult for direction. It is not a
second scheduler and it does not command the Neuron.

## Position in the Tick path

The optional Observer is injected into one `KLEPAgent` through the Core
guidance interface. One completed Agent Tick may produce a
`KLEPGuidanceRequest`. Only then may the Agent call the Observer.

The Observer receives immutable evidence from that completed Tick and may
prepare advice for the following top-level Agent Tick. It cannot:

- call `KLEPAgent.Tick` or `KLEPNeuron.Tick`;
- alter the decision that produced the request;
- add, remove, replace, reserve, or consume a Key;
- mutate a Lock, Executable definition, Q-value, lifecycle, or registry; or
- make an ineligible behavior eligible.

The supplied context exposes no mutation API. V1 still treats an injected
Observer and its evidence sources as trusted pure code: a callback that has
captured some other mutation handle is contractually forbidden from using it.
The Agent detects Neuron cycle drift after a callback, but this check is not a
capability sandbox.

An Observer exception is outside the already committed Neuron decision. The
Agent queues no partial advice, records that consultation was attempted, and
rethrows the exception.

The Agent continues to emit a guidance request on every completed Tick whose
confidence is at or below its guidance threshold. The V1 frequency policy
consults the Observer once per unchanged uncertainty episode. It consults again
when the environment signature or guidance evidence fingerprint changes, the
ordered eligible-target set changes, prepared advice finds that its target
registration was replaced, a learning update commits, or confidence recovers
and later dips.

## Holistic direction

Observer V1 chooses a direction, not a multi-step action sequence. Its target
pool is the guidance request's currently eligible root Solo Goals and actions.
Tandems and Goal-owned children are not independent Observer targets.

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

The configured polish amount is a nudge, not a command. It may be too small to
cross certainty or beat another eligible target's authored score. Whether a
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
never participates in Tandem eligibility or ordering.

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

The present Executable contract declares Key IDs that an Executable may emit,
but it does not declare whether an expected effect adds, replaces, or removes a
Key. Therefore V1 does not invent a symbolic transition graph from
`DeclaredOutputs`.

Deferred work includes:

- explicit expected-effect semantics and multi-step route search;
- a separate Planner API;
- Aron/Nora breadth-versus-depth competition;
- model trust and resource allocation;
- persistence of Observer policy or polish; and
- neural or asynchronous reasoning implementations.

These may later live behind the same Observer boundary without changing the
Neuron's eligibility authority.

## Required trace

The inspectable record must show:

- whether guidance was requested and whether the Observer was consulted;
- ordered evidence sources and every contribution;
- every eligible target's holistic total;
- the selected direction or abstention reason;
- the request and advice evidence fingerprint and polished Lock IDs;
- next-Tick application, staleness, missing target, changed registration,
  ineligible rejection, or a Tick fault before application;
- authored score components and the final Observer contribution separately.

Identical definitions, snapshots, evidence, configuration, time, and seed must
produce identical advice and traces.
