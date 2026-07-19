# KLEP Executable contract

This contract records the owner-approved lifecycle, scheduling, output, and
Goal rules implemented by Core. Agent learning is specified separately in
`AGENT_CONTRACT.md` and does not alter the selection rules below.

## Definition and pure selection

An Executable has immutable authored definition data:

- one non-empty stable ID;
- a display name and diagnostic kind (`Action`, `Goal`, `Router`, or `Sensor`);
- a `Solo` or `Tandem` execution mode;
- one finite base-attractiveness score;
- ordered Validation and Execution Lock groups;
- an ordered declaration of every stable Key ID it may add or replace.

Every Lock in both groups must pass against the same immutable snapshot before
the Executable is available. Evaluation and scoring cannot mutate Keys, Locks,
stores, lifecycle, or behavior state. The current score is
`BaseAttractiveness` plus each Validation and Execution Lock's attractiveness.
Key-attractiveness remains deferred. Agent confidence is diagnostic and is not
a score component. An optional one-use Observer component may be appended only
after eligibility under the separate `OBSERVER_CONTRACT.md` boundary.

## Lifecycle

Registration changes commit only at a top-level `KLEPNeuron.Tick` boundary.
Initialization runs once per committed registration tenure and before that
Executable's first eligibility evaluation.

One run follows this state sequence:

```text
Idle -> Enter -> Running -> Succeeded | Failed | Cancelled -> Exit -> Cleanup
```

- Enter and Tick occur in the same Neuron Tick when an Idle Executable starts.
- A Running Executable ticks without re-entering.
- Exit and Cleanup run exactly once when a run terminates or is interrupted.
- A terminal state remains observable for the rest of its Neuron Tick.
- It rearms to Idle at the following top-level Tick and may then run again.
- Removal of an active Executable cancels and tears it down before removal.
- A lifecycle exception is recorded, teardown is attempted once, and the
  exception is rethrown.

There are no Core `Update`, `FixedUpdate`, `ExecutableUpdates`,
`ExecutableFixedUpdate`, or `FixedExecute` paths. `KLEPNeuron.Tick` is the sole
source of Core timing.

## Output boundary

Lifecycle callbacks return ordered Key operations; they never receive a Neuron
or KeyStore reference. Add and Replace operations must target a Key ID declared
by that Executable. Remove targets one exact visible fact. An undeclared or
invalid operation is a programming fault.

This slice does not expose an Executable lifetime override: Add uses the Key
definition's default lifetime, and Replace preserves the exact fact's lifetime.
Whether a later authoring API permits explicit overrides remains open.

- Tandem Local outputs from one wave are validated and published together at
  the next same-Tick wave barrier.
- Declared-but-not-emitted outputs do not cause another wave.
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

The registered set is frozen and stable-ID ordered for the Tick. Every Tandem
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
races. Tandems are eligibility-gated but not scored.

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
  still have occurred; patient simply means the Neuron is waiting for stimuli.

The accepted Agent passes an explicitly configured action certainty threshold
(default `0`) into this arbitration, then calculates confidence after observing
the decision cycle. Learned confidence does not replace strict comparison,
change that configured threshold, or otherwise influence arbitration. Guidance
is requested when confidence is at or below its separate guidance threshold;
that diagnostic threshold also defaults to `0`.

## Goals

`KLEPGoal` inherits `KLEPExecutableBase`, uses its own Locks and score, and owns
its children exclusively. Goal children are not root Neuron registrations.

- Goal construction validates the complete child set before claiming ownership;
  duplicate or already-owned children reject construction without changing any
  child's ownership.
- A Goal considers at most one ordered layer per top-level Tick.
- `AllMustFire` advances only after every child actually succeeds.
- `AnyCanFire` advances only after at least one child actually succeeds.
- `NoneNeedToFire` advances without invoking its children.
- A blocked or never-fired child cannot satisfy a layer.
- The activation Key is emitted once on Goal Enter and follows the Solo output
  boundary.
- Goal success remains observable for the rest of the Tick and may rearm on the
  following Tick.
- When a Goal exits, every Running child is offered cancellation even if an
  earlier child's Exit or Cleanup faults. The first failing child and stage are
  retained in the fault trace; one fault is rethrown unchanged and several are
  reported together.
- There is no child proxy/stand-in score.

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
- multi-step Observer route search, a separate Planner API, and Nora/Aron
  model competition (the one-use eligibility-gated score overlay is defined
  separately in `OBSERVER_CONTRACT.md`);
- Key-attractiveness scoring for composite expressions;
- world-wide same-boundary Global influence across several Neurons;
- Unity authoring/runtime adapters for Executables and Goals;
- automatic consumption, borrowing, reservation, and returned Keys.
