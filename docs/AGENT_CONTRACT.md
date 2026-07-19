# KLEP Agent contract

This contract defines the first deterministic Agent learning slice. The Agent
observes one Neuron and estimates whether it has repeatedly navigated the
current Key environment successfully. It does not plan and it does not alter
the Neuron's accepted arbitration semantics.

## Pure Tick facade

`KLEPAgent` is a pure Core object associated with exactly one `KLEPNeuron`.
One Agent Tick advances that Neuron exactly once through `KLEPNeuron.Tick`,
passing the configured action certainty threshold (default `0`), then observes
the returned immutable decision trace. That threshold is ordinary arbitration
configuration, not a learned confidence influence. The Agent has no Unity
`MonoBehaviour`, `Update`, `FixedUpdate`, event bridge, or second timing path.

The Agent may attach after earlier patient or terminal history, but it rejects
attachment while a Solo run is already Running because that run's true start
state is unavailable. Once attached, `KLEPAgent.Tick` is the sole Tick path for
that Neuron. The Agent checks cycle continuity before every advance and rejects
an outside or competing Neuron Tick before causing another mutation.

The Agent owns its learning tables. It cannot mutate snapshots, Keys, Locks,
Executable definitions, lifecycle state, or the Neuron registry while learning.
Given the same initial tables and the same ordered Tick traces, its results are
deterministic.

## Environment signature

Learning uses the final post-Tandem snapshot used for Solo arbitration. Its
environment signature is the ordinally sorted unique set of:

```text
(Key scope, stable Key ID)
```

Both Local and visible Global Keys participate. Tick and wave indices, payload
values, occurrence IDs, source IDs, lifetimes, and duplicate occurrence counts
do not participate. Consequently, injecting a previously absent stable Key ID
creates a different environment, while replacing a payload or adding another
occurrence of an already-present ID does not.

This abstraction matches the current Lock language, which observes Key
presence. If payload-aware Lock predicates are introduced later, changing the
Agent signature is a separate protected decision.

## Guidance evidence fingerprint

Guidance uses a separate deterministic evidence fingerprint so payload changes
can reopen reasoning without changing the presence-only learning state. The
fingerprint is the ordinal canonical multiset of every visible fact's:

```text
(Key scope, stable Key ID, ordered payload field names/types/values)
```

Duplicate payload occurrences participate. Occurrence IDs, source IDs,
lifetimes, issued/activated Ticks, snapshot Tick, and wave metadata do not.
Consequently, replacing Health `hp = 10` with `hp = 9`, or changing a copied
ammo fact's project-authored `observedWorldTick` payload, changes guidance
evidence while leaving the Q-learning environment identity unchanged. A new
occurrence carrying the same semantic payload does not create false novelty.

## State, action, and transition

The state `s` is an environment signature. The action `a` is the stable ID of a
root Solo Executable. A Goal is one action under its root Goal ID; Goal-owned
children never receive independent root-Agent entries. Tandem Executables
shape the settled environment but are not Agent actions.

A transition begins when a Solo run enters. It retains that starting signature,
root action ID, run ID, and starting Tick until the run produces a sampled
outcome. Its duration `d` is the number of top-level Neuron Ticks occupied by
the run, with a minimum of one. It is not Neuron/world cycle-index subtraction:
one Agent-owned Neuron Tick counts once even when a shared Global boundary
jumps forward by several world indices, and a pre-boundary fault counts zero.

An outcome is queued during the Tick that reports it. The next state `s'` is
the final post-Tandem environment from the following successfully completed
Agent Tick; pending outcomes are finalized before that observation is counted.
`maxEligibleNextQ` considers only root Solo Executables reported eligible in
that next decision trace, and it is zero when none are eligible. Eligibility is
always filtered before a learned value is read. A faulted Agent Tick neither
finalizes nor discards an already pending outcome.

## Q update

Every unseen `(s, a)` pair starts at zero. A sampled transition applies the
semi-Markov update once. The parameters are configurable; their accepted
defaults are:

```text
alpha = 0.2
gamma = 0.9

Q(s, a) <- Q(s, a)
           + alpha * (
               reward
               + gamma^d * maxEligibleNextQ
               - Q(s, a))
```

The ordered state/action table is inspectable. Repeated success by the same Goal
in the same Key environment raises its Q-value naturally; no hidden streak
bonus is added.

## Outcome rewards

Only these lifecycle outcomes create samples. Rewards are configurable; the
accepted defaults are:

| Outcome | Reward |
|---|---:|
| `Succeeded` | `+1` |
| `Failed` | `-1` |
| Cancelled with `Interrupted` | `-0.25` |

Other cancellation reasons, explicit removal, and execution faults do not
create learning samples. In particular, an implementation or teardown fault is
reported through the existing fault contract rather than converted into a
behavioral reward. If a current Solo was already validly interrupted before its
same-Tick challenger faulted, that committed interruption remains a pending
sample; the challenger fault itself still supplies none.

## Familiarity and confidence

`N(s)` is the number of completed prior Agent-Tick observations of signature
`s`. The current observation is recorded only after the current confidence is
calculated, so a never-before-seen environment begins with zero familiarity.
The familiarity scale is configurable and defaults to `4`.

```text
familiarity(s) = N(s) / (N(s) + familiarityScale)

eligiblePositiveQ(s) = max(0, max Q(s, a) over eligible root Solos)
positiveQBound       = successReward / (1 - gamma)

confidence(s) = familiarity(s)
                * clamp01(eligiblePositiveQ(s) / positiveQBound)
```

With the default success reward `1` and `gamma = 0.9`, the positive Q bound is
`10`.

Familiarity and Q-value answer different questions. `N` measures how often the
Agent has seen the environment; Q estimates discounted successful navigation.
An unfamiliar environment and a familiar environment with no successful route
can therefore both have low navigation confidence for different inspectable
reasons.

## Guidance boundary

When confidence is at or below the configured guidance threshold, the Agent
emits an immutable guidance request describing the cycle, observed environment,
evidence fingerprint, confidence, threshold, novelty, and ordered eligible Solo
IDs. The comparison is inclusive:

```text
needsGuidance = confidence <= guidanceConfidenceThreshold
```

The guidance threshold defaults to `0`. A never-before-seen environment has
zero familiarity and zero confidence, so it requests guidance even under the
default threshold. A previously seen environment with zero confidence also
requests guidance, with a different diagnostic reason.

Producing the request itself cannot:

- make an ineligible Executable eligible;
- change an authored score or certainty threshold;
- select, interrupt, or advance an Executable;
- add, remove, or replace a Key; or
- alter the completed decision that produced it.

When an optional Observer is injected, the Agent may consult it after that
decision and prepare one-use advice for the next top-level Tick. The advice is
an eligibility-gated score contribution governed by
`OBSERVER_CONTRACT.md`; it is not a second decision pass. Reactive
Key/Lock/Executable chains continue normally while confidence is low. Full
multi-step planning, Nora/Aron breadth-versus-depth competition, model trust
and resource allocation, persistence, and neural implementations remain
deferred.

The request remains observable on every completed low-confidence Tick. The V1
consultation-frequency policy opens one Observer consultation episode for an
unchanged environment, evidence fingerprint, and eligible set. It consults
again only after the environment, fingerprint, or eligible set changes,
prepared advice detects a replaced root registration, a learning update is
committed, or confidence first rises above the guidance threshold and later
falls back. The request therefore remains diagnostic even when a repeated
Observer callback is suppressed.

If the Neuron Tick faults, the Agent retires the faulted run, records a
non-completed fault trace, emits no guidance request, performs no visit or Q
update, and rethrows the original exception through the Neuron's fault contract.
When the fault occurs before a new Key snapshot exists, that diagnostic retains
the last boundary environment instead of falsely reporting a novel empty state.
