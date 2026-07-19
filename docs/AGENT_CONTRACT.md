# KLEP Agent contract

This contract defines the deterministic Agent authority. A Neuron passively
stores Keys and its Executable catalog. Exactly one Agent exclusively owns that
Neuron's decision/fire Tick, Executable and Goal runtimes, Tandem settlement,
Solo arbitration, output-boundary choices, traces, learning, and optional use of
Observer evidence.

## Exclusive decision/fire Tick

`KLEPAgent` is a pure Core object that exclusively claims exactly one
`KLEPNeuron`. The Neuron has no independent decision Tick and a second Agent
cannot claim it. Key/catalog authoring may occur before the claim, but only the
owning Agent may enter a guarded Local boundary after it is claimed.

One Agent Tick performs one ordered decision cycle:

1. acquire the Neuron's non-reentrant storage boundary;
2. commit the next Local Key boundary and freeze immutable Key evidence;
3. freeze staged catalog changes and obtain a valid Observer graph assessment
   whenever the catalog revision changes or an explicit remap was requested;
4. reject an invalid proposed revision atomically, otherwise cancel removed
   runtimes and initialize committed registrations;
5. settle every eligible root Tandem, including Tandem Goals, in deterministic
   waves;
6. evaluate eligible root Solos, compose finite inspectable influence, and
   select, retain, interrupt, or advance at most one;
7. validate and stage outputs through the passive Neuron storage API;
8. freeze the decision/fault/runtime trace;
9. update learning and confidence, then optionally request additional Observer
   deliberation; and
10. release the guarded boundary.

The configured action certainty threshold defaults to `0` and is ordinary
arbitration configuration, not a learned confidence influence. The Agent has no
Unity `MonoBehaviour`, `Update`, `FixedUpdate`, event bridge, or second timing
path. Unity may host one call to `KLEPAgent.Tick`; it does not schedule Core.

The Agent owns root and Goal-descendant lifecycle records, current Solo identity,
Goal layer progress, candidate and wave state, and the immutable decision trace.
Executable and Goal objects stored in the Neuron are authored behavior/recipe
objects, not owners of live scheduler state. While learning or consulting the
Observer, the Agent cannot mutate snapshots, Keys, Locks, definitions,
lifecycle, or catalog. Given identical stores, catalog revision, Observer map,
configuration, time, seed, and prior Agent state, its results are deterministic.

## Catalog assessment and map use

The Neuron exposes a monotonically changing catalog revision and an immutable
recursive graph snapshot containing roots, Goal-owned descendants, execution
modes, Locks, guaranteed `DeclaredOutputs`, and stable registration tenures.
The Agent submits a proposed changed revision to the Observer before activating
it. It may also request an explicit remap of the unchanged revision.

Every Agent has a structural graph Observer. When a project injects no
higher-cognition implementation, KLEP supplies a deterministic baseline
validator/mapper; optional evidence sources and low-confidence deliberation may
still be absent.

The Observer returns an immutable validation/map assessment. The Agent:

- rejects an invalid proposed revision before any new runtime initializes;
- reuses an assessment only when its exact catalog revision and graph
  fingerprint match;
- may inspect reachability and candidate projections from a valid map; and
- remains the only authority that evaluates current Locks, selects, and fires.

Catalog mapping is mandatory when its revision changes and is independent of
confidence. An unchanged valid map need not be rebuilt every Tick. An Observer
projection is proposal evidence, never a current Key or permission to bypass a
closed Lock. `DeclaredOutputs` is a cumulative successful-run emission promise;
the Agent must not infer final persistence, removal, or coexistence by unioning
those IDs with current Keys.

Every decision trace freezes the structural-map decision used by that Tick:
structural Observer identity/version, why assessment was considered, whether it
was accepted, reused, rejected, faulted, or not reached, the exact requested
catalog revision/fingerprint, any attempted assessment, and the valid active
assessment retained for execution. Registration-boundary rollback is a distinct
trigger that retains both the rejected proposal and recovered active map. A
structural fault retains its exception type/message in a dedicated immutable
fault record while the ordinary Tick fault and original throw remain intact.
Later remaps cannot rewrite this historical map evidence.

## Default projected-satisfaction policy

A designer may give the Agent an immutable ordered set of desires. Each desire
contains:

- one desired Key/Lock expression evaluated purely as true (`1`) or false
  (`0`);
- one finite authored weight;
- an optional deterministic finite pressure, defaulting to `1`; and
- stable identity/version and an explanation for tracing.

For each already-eligible Solo candidate, the Agent asks an optional pure
Observer for a complete Key-presence state at the explicit
`SuccessfulRunCompletion` horizon. The request and response are bound to the
exact accepted catalog revision/fingerprint, root tenure, target ID, and current
evidence fingerprint and retain projector identity/version and provenance. A
stale or mismatched response faults before scoring. When no project projector
supports a complete state, the baseline explicitly abstains: projected truth is
unknown, the reason remains inspectable, and the contribution is exactly zero.

For a complete projection, the Agent evaluates the desire in the current
immutable snapshot and the projected state:

```text
desireContribution = weight
                     * pressure
                     * (projectedTruth - currentTruth)

projectedSatisfaction = checked finite sum(desireContribution)
```

The sum is one separate inspectable selection component. It cannot make a
blocked Solo eligible and does not apply to automatic Tandems. When every weight
and pressure is `1`, the result is exactly the projected net change in the count
of satisfied desires. Weights, pressures, expressions, and their project meaning
are designer policy; KLEP supplies no universal definition of benefit.

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
outcome. Its duration `d` is the number of top-level Agent Ticks occupied by
the run, with a minimum of one. It is not Neuron/world cycle-index subtraction:
one Agent Tick counts once even when a shared Global boundary
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

Low confidence is diagnostic and may request additional deliberation; it is not
the trigger for catalog validation or map maintenance. A familiar high-
confidence Agent still remaps a changed catalog, while an unchanged valid map
may be reused during a low-confidence episode.

Producing the request itself cannot:

- make an ineligible Executable eligible;
- change an authored score or certainty threshold;
- select, interrupt, or advance an Executable;
- add, remove, or replace a Key; or
- alter the completed decision that produced it.

After such a request, the Agent may ask the Observer for optional additional
deliberation and prepare one-use advice for the next top-level Tick. That advice
is an eligibility-gated score contribution governed by
`OBSERVER_CONTRACT.md`; it is not a second decision pass. It is distinct from
the revision-bound structural map and projected-satisfaction component.
Reactive Key/Lock/Executable chains continue normally while confidence is low.
Nora/Aron breadth-versus-depth competition is not required for an explicitly
mapped graph; it remains an optional future Observer search policy along with
model resource allocation, persistence, and neural implementations.

The request remains observable on every completed low-confidence Tick. The V1
consultation-frequency policy opens one Observer consultation episode for an
unchanged environment, evidence fingerprint, and eligible set. It consults
again only after the environment, fingerprint, or eligible set changes,
prepared advice detects a replaced root registration, a learning update is
committed, or confidence first rises above the guidance threshold and later
falls back. The request therefore remains diagnostic even when a repeated
Observer callback is suppressed.

If the Agent Tick faults, the Agent retires the faulted run, records a
non-completed fault trace, emits no guidance request, performs no visit or Q
update, and rethrows the original exception through the Agent's fault contract.
When the fault occurs before a new Key snapshot exists, that diagnostic retains
the last boundary environment instead of falsely reporting a novel empty state.
