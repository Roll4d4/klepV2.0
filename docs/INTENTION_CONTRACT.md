# KLEP Intention contract

This contract defines the deliberately small first `KLEPIntentionState`.
Intention records which already-authored end the Agent actually adopted; it is
not another chooser, planner, Goal runtime, or source of attraction.

## Authority and order

Exactly one `KLEPAgent` constructs and owns its Intention state. The reducer
runs once after the Agent's decision runtime has committed the current boundary
and frozen its lifecycle evidence. It receives that immutable evidence only.

Intention cannot:

- read or mutate a Key store, Lock, catalog, Executable, or Goal runtime;
- change eligibility, score, certainty, retention, interruption, or selection;
- advance, retry, complete, or cancel an Executable;
- train or query navigation learning or `KLEPLearnedExpectations`;
- invoke or influence the Observer; or
- install an Observer proposal or an unauthored Goal recipe.

The public `KLEPIntentionState` surface exposes only its latest immutable
snapshot. All transition methods remain inside the Agent assembly.

## V1 domain

V1 tracks an actual advance of an already-registered root `KLEPGoal` whose
execution mode is `Solo`. The accepted structural map supplies the exact root
identity, registration tenure, and semantic Goal-recipe marker. The diagnostic
`KLEPExecutableKind.Goal` label alone does not create an intention.

These do not create intentions:

- a root Action, Sensor, or Router;
- an automatic root Tandem Goal;
- a Goal-owned child, including a nested Goal;
- an eligible or scored Goal that was not advanced;
- a structural dependency proposal or projected state; or
- a Patient Tick.

## Identity and state

An intention has a deterministic Agent-local sequence and an inspectable ID.
It binds one root Goal stable ID and one exact registration tenure. That
identity is distinct from Goal run identity so interruption and later
resumption can span different runtime runs.

The current open states are:

- `Active`: the adopted Goal is the currently advancing root Solo end; and
- `Suspended`: the end remains adopted while its current runtime has stopped.

The terminal states are `Completed` and `Abandoned`. Terminal records are
retained in the Tick that retired them and are not kept in the live open set.
A later actual advance therefore creates a new intention identity.

## Transition reduction

The first actual Solo advance for one Goal tenure emits `Adopted` and enters
`Active`. A continued `Running` result for that same intention and run changes
no state and emits no duplicate transition.

| Committed lifecycle evidence | Transition |
|---|---|
| cancellation with `Interrupted` | `Suspended` |
| cancellation with `LocksClosed` | `Suspended` |
| later advance of the same Goal ID and tenure | `Resumed` to `Active` |
| terminal `Succeeded` | `Completed` |
| terminal `Failed` or `Faulted` | `Abandoned` |
| cancellation with `BelowThreshold` or `Removed` | `Abandoned` |
| accepted map removes or replaces a suspended tenure | `Abandoned` |

Removal of an idle suspended Goal may have no cancellation step. The reducer
therefore compares every open intention's exact tenure with the active accepted
map. A rejected catalog proposal does not retire an intention because it never
replaced that active map.

The reducer preserves execution-step order. A single Tick may consequently
show, for example, one Goal suspended before another is adopted, or a
single-Tick Goal adopted and then completed. `WaveAborted` is outside V1 because
automatic Tandem Goals are outside the intention domain.

When output application faults after a Solo advance, Core evidence may retain
the attempted result followed by a corrected `Faulted` result for the same Goal
run. The attempted step still establishes adoption/resumption order, but only
the final same-run Solo result determines terminal Intention state.

## Clocks and immutable trace

Every transition retains:

- its deterministic transition sequence;
- intention ID, Goal stable ID, and root registration tenure;
- prior and resulting status;
- exact Goal run index;
- Agent Tick ordinal and Core cycle/wave;
- typed Intention reason and exact Executable exit reason when present; and
- related selected root identity and tenure when one exists.

Every `KLEPAgentTickTrace` freezes one `KLEPIntentionSnapshot` containing the
current open intentions, terminal records retired during that Tick, and only
that Tick's transitions. The same snapshot is reused if optional Observer
consultation rebuilds the surrounding Agent trace. Historical rendering reads
that frozen snapshot, never the Agent's current live state.

If a fault occurs after the Agent boundary and lifecycle changes committed,
the fault trace reduces those actual steps. A pre-boundary fault captures the
unchanged open state with an empty transition list instead of replaying the
previous Tick's transitions.

## Deferred policy

V1 intentionally defines no manual adoption/abandon command, maximum number of
suspended intentions, expiry, priority stack, commitment score, switching
resistance, persistence codec, Memory integration, Desire integration, or
Observer route-adoption policy. Adding any of those is a separate decision.
