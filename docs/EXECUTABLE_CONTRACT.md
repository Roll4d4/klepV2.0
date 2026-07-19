# KLEP Executable contract

This contract records the owner-approved lifecycle, scheduling, output, and
Goal rules. The Neuron stores Keys and the Executable catalog; the exclusive
Agent owns runtimes, scheduling, firing, Goal progress, and decision traces.
Agent learning and projected-satisfaction policy are specified separately in
`AGENT_CONTRACT.md`.

## Definition and pure selection

An Executable has immutable authored definition data:

- one non-empty stable ID;
- a display name and diagnostic kind (`Action`, `Goal`, `Router`, or `Sensor`);
- a `Solo` or `Tandem` execution mode;
- one finite base-attractiveness score;
- ordered Validation and Execution Lock groups;
- one ordered `DeclaredOutputs` list containing every stable Key ID it may Add
  or Replace and must have emitted before a run is accepted as `Succeeded`.

`DeclaredOutputs` is both permission and successful-completion promise. There
is no second completion-effect, possible-output, or conditional-output list. An
empty declaration is valid. The Agent tracks cumulative Add/Replace emissions
for each run; Enter and earlier Running emissions count. A run that attempts to
succeed before every declared ID has appeared, or an Add/Replace of an undeclared
ID, is a programming fault and its terminal success is not accepted.

Every Lock in both groups must pass against the same immutable snapshot before
the Executable is available. Evaluation and scoring cannot mutate Keys, Locks,
stores, lifecycle, Goal progress, eligibility, or behavior state. For an
ordinary Solo Executable, the pre-Observer score is `BaseAttractiveness` plus each
Validation and Execution Lock's attractiveness. An eligible root Solo Goal may
also supply the one intrinsic-attraction contribution defined below. Key
attractiveness remains deferred. Agent confidence is diagnostic and is not a
score component. An inspectable projected-satisfaction component and optional
one-use Observer component may be appended only after eligibility and intrinsic
Goal evaluation under the separate Agent and Observer contracts. Tandems are
never scored.

If an intrinsic-attraction evaluator faults or returns a non-finite or otherwise
invalid result, the top-level Tick aborts. The fault is retained in the
immutable trace and rethrown; no failed evaluation can make the Goal eligible or
advance its lifecycle.

## Lifecycle

Registration changes commit only at a top-level `KLEPAgent.Tick` boundary after
the proposed recursive catalog revision has passed Observer validation.
Initialization runs once per committed registration tenure and before that
Executable's first eligibility evaluation.

One run follows this state sequence:

```text
Idle -> Enter -> Running -> Succeeded | Failed | Cancelled -> Exit -> Cleanup
```

- Enter and Tick occur in the same Agent Tick when an Idle Executable starts.
- A Running Executable ticks without re-entering.
- Exit and Cleanup run exactly once when a run terminates or is interrupted.
- A terminal state remains observable for the rest of its Agent Tick.
- It rearms to Idle at the following top-level Tick and may then run again.
- Removal of an active Executable cancels and tears it down before removal.
- A lifecycle exception is recorded, teardown is attempted once, and the
  exception is rethrown.

There are no Core `Update`, `FixedUpdate`, `ExecutableUpdates`,
`ExecutableFixedUpdate`, or `FixedExecute` paths. Exactly one Agent exclusively
claims a passive Neuron, and `KLEPAgent.Tick` is the sole Core decision/fire
clock. The Neuron may commit a guarded storage boundary only for that Agent; it
does not initialize, select, advance, interrupt, or trace an Executable.

## Output boundary

Lifecycle callbacks return ordered Key operations; they never receive a Neuron
or KeyStore reference. Add and Replace operations must target a Key ID declared
by that Executable. Remove targets one exact visible fact. An undeclared or
invalid operation is a programming fault.

On attempted `Succeeded`, the Agent verifies the cumulative emitted-ID record
for that run before accepting the terminal result. This guarantee applies to
Actions, Routers, Sensors, Solo Goals, and Tandem Goals. An Enter or Running
emission can satisfy the promise and remains governed by its ordinary output
boundary. Failed or Cancelled runs have no completion guarantee; failed-advance
outputs remain discarded under the rule below.

This slice does not expose an Executable lifetime override: Add uses the Key
definition's default lifetime, and Replace preserves the exact fact's lifetime.
Whether a later authoring API permits explicit overrides remains open.

- Tandem Local outputs from one wave are validated and published together at
  the next same-Tick wave barrier.
- A nonterminal advance with no actual output does not cause another wave.
- Solo Local outputs remain staged until the following top-level Tick.
- Global outputs remain staged until the world owner commits the Global store.

The complete batch is preflighted before staging. Local and Global pending
state is restored if any operation fails, and an Add cannot create the same
stable Key ID in the opposite scope. An invalid initialization-output batch
rejects that registration boundary.

If an Executable Tick returns `Failed`, every output queued in that advance's
Enter/Tick context is discarded before output application. The terminal result
contains no outputs and stages no Key mutation. Failure still performs
exact-once Exit and Cleanup, remains observable for the rest of the Tick, and
does not trigger a same-Tick Solo fallback.

## Tandem settlement

The Agent freezes the validated registered set in stable-ID order for the Tick.
Every Tandem
Executable may advance at most once:

1. Evaluate all not-yet-fired Tandems against one immutable wave snapshot.
2. Enter/Tick every eligible Tandem in stable-ID order using that same snapshot.
3. Collect actual operations without exposing one peer's output to another.
4. Publish Local operations together at the wave barrier.
5. If visible Local state changed, retry previously blocked Tandems against the
   new immutable snapshot.
6. Stop when no eligible Tandem remains, no actual Local change occurred, or
   all Tandems advanced.

This settles sensor/router facts before Solo selection without behavior-order
races. Tandems, including Tandem Goals, are eligibility-gated but not scored,
polished, or attraction-ranked. Their ordering is stable ID, never benefit.

A Tandem that already advanced is processed for that top-level Tick. It is not
reevaluated, advanced, or cancelled in a later same-Tick wave if another
Tandem's publication then closes its Locks. If the processed Tandem remains
Running, the following top-level Tick reevaluates it before advancement and
closed Locks cancel it with `LocksClosed` through the ordinary exact-once
teardown path.

An exception aborts the uncommitted wave. Earlier peers that returned Running
are cancelled with `WaveAborted` and clean up once, so they can re-enter next
Tick and reproduce output that the aborted wave did not publish. Earlier peers
that already terminated remain terminal for the current Tick and rearm on the
next one.

## Solo arbitration and patient state

After Tandem settlement, Solo candidates are evaluated and scored from the
final snapshot. At most one Solo advances.

- With no current Solo, highest score strictly above the certainty threshold
  wins; stable ID breaks a tie.
- A valid Running Solo retains an equal-score tie regardless of stable ID.
- Only a strictly higher challenger interrupts it in this slice.
- Closed Locks or a score at/below threshold cancel the current Solo.
- Success or failure cannot trigger same-Tick fallback or re-entry.
- Patient means no Solo advanced and no Solo remains Running. Tandem work may
  still have occurred; patient simply means the Agent has no selected Solo and
  is waiting for stimuli.

The Agent owns this arbitration and uses its explicitly configured action
certainty threshold (default `0`). It may add the finite, inspectable
projected-satisfaction component defined in `AGENT_CONTRACT.md`, followed by a
valid optional one-use Observer polish. Learned confidence does not replace
strict comparison, change certainty, or open eligibility. Low-confidence
guidance remains a separate optional deliberation request.

## Goals

`KLEPGoal` inherits `KLEPExecutableBase`, may be authored as Solo or Tandem,
uses its own Locks, and owns its immutable child recipe exclusively. Goal
children are not root Neuron registrations. The Agent owns every mutable Goal
and descendant runtime, current layer, completion mark, fault record, and
teardown state; the stored Goal object does not own live progress.

After a root Solo Goal's Locks pass against the final immutable post-Tandem
snapshot, its optional project-authored evaluator returns exactly one
deterministic, finite, signed, inspectable intrinsic-attraction contribution
from that same snapshot. A Goal without an evaluator contributes zero. The
Goal's pre-Observer score is:

```text
BaseAttractiveness
+ Validation Lock attractiveness
+ Execution Lock attractiveness
+ intrinsic Goal attraction
```

Intrinsic evaluation is read-only. It cannot mutate Keys, Locks, stores,
eligibility, lifecycle, Goal progress, or another subsystem, and it cannot run
owned children. Owned-child scores never proxy for or contribute to the root
Goal's score. Existing certainty, stable-ID tie-breaking, Running-Solo
equal-score retention, and strict-higher interruption rules then compare these
pre-Observer scores. The Agent may append projected satisfaction and then valid
one-use Observer advice as separate eligibility-gated components.

A root Tandem Goal has no score, intrinsic-attraction evaluation,
projected-satisfaction component, or Observer polish. When its Locks pass, the
Agent advances it as one automatic Tandem composite. Several root Tandem Goals
may therefore progress in the same Agent Tick, each at most once, before the
single Solo lane is considered.

- Goal construction validates the complete child set before claiming ownership;
  duplicate or already-owned children reject construction without changing any
  child's ownership.
- A root Goal considers at most one ordered layer per top-level Agent Tick.
- `AllMustFire` advances only after every child actually succeeds.
- `AnyCanFire` advances only after at least one child actually succeeds.
- `NoneNeedToFire` advances without invoking its children.
- A blocked or never-fired child cannot satisfy a layer.
- The activation Key is emitted once on Goal Enter and, along with forwarded
  child and completion output, follows the root Goal's Solo or Tandem boundary.
- Goal success remains observable for the rest of the Tick and may rearm on the
  following Tick.
- When a Goal exits, every Running child is offered cancellation even if an
  earlier child's Exit or Cleanup faults. The first failing child and stage are
  retained in the fault trace; one fault is rethrown unchanged and several are
  reported together.
- There is no child proxy/stand-in score.
- A Goal-owned child's authored mode does not create an independent root Solo
  lane or Tandem wave. The owning Goal's layer policy controls when the Agent
  advances that child.
- A Goal that returns `Succeeded` must satisfy its own cumulative
  `DeclaredOutputs` guarantee after its activation and forwarded child output
  are included.

A child `Failed` result does not directly fail its Goal in V1:

- `AllMustFire` advances every eligible unsatisfied child in authored order,
  retains successful-child progress, and leaves blocked or failed children
  unsatisfied. Failed children may rearm and retry on a later top-level Tick;
  the layer completes only after every child actually succeeds.
- `AnyCanFire` is serial in authored order. A blocked or Failed child permits
  the next sibling to be considered in the same Tick. The first Running child
  retains the layer and stops later siblings for that Tick. The first Succeeded
  child completes the layer without advancing later siblings.
- If no required success occurs, the Goal remains Running. Running children
  are cancelled when their own Locks close or the Goal exits; V1 performs no
  proactive sibling cancellation merely because another child failed or ran.

## Still deferred

- learned score bias, confidence-derived switching margins, and other learned
  selection influence;
- alternative graph-search/resource policies, including optional Nora/Aron
  breadth-versus-depth competition behind the Observer boundary;
- admission and execution of model-invented Keys, Locks, Executables, or
  capabilities;
- Key-attractiveness scoring for composite expressions;
- world-wide same-boundary Global influence across several Neurons;
- Unity authoring/runtime adapters for Executables and Goals;
- automatic consumption, borrowing, reservation, and returned Keys.
