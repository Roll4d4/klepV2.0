# KLEP Emotion contract

This contract defines the first deterministic, inspectable Emotion slice. It
models emotional dynamics after an action or event has already been evaluated.
It does not define a correct ethical system, perform long-term recall, or alter
KLEP behavior selection.

## Separation of responsibilities

The current conceptual direction is:

```text
action or event in context
    -> designer-owned Ethics evaluation
    -> Emotion influence
    -> Emotion motion and snapshot
    -> KLEPMemory retention and causal association
    -> future eligible-behavior ranking
```

The physical action does not contain its emotional meaning. Watching one actor
shoot another may be evaluated differently when the target is a victim, a
captor, or an immediate threat. Ethics supplies that contextual evaluation;
Emotion only integrates the resulting influence.

KLEP does not solve Ethics. Each higher-cognition designer owns the standards,
weights, rules, models, or other process that produces an evaluation.

## The emotional graph

Emotion occupies a normalized two-axis graph. Each coordinate is finite and in
`[-1, 1]`. Axis labels are configuration and have no built-in KLEP meaning. A
designer may use labels such as Valence and Activation or define a different
pair.

The emotional body exposes:

- **Position**: its current location on the graph.
- **Velocity**: the motion carried into the next Tick.
- **Influence**: an already-evaluated impulse with a stable source ID and
  Internal or External cause provenance.
- **Unchanged-position Tick count**: how long position has remained unchanged.

Internal and External provenance describes where the evaluated cause occurred,
not where the evaluator resides or what authority it possesses. It does not
alter the math or grant authority. Influence order is semantic and must be
supplied through an ordered list. One source ID may occur at most once per Tick.

## Deterministic Tick transition

The caller supplies consecutive integer Ticks. Emotion has no Unity `Update`,
`FixedUpdate`, wall clock, random source, or independent scheduler.

For one Tick, Emotion:

1. validates and copies the ordered supplied influences;
2. combines and bounds their net impulse;
3. adds that impulse to prior velocity and bounds speed;
4. moves position by that integrated velocity and bounds both graph axes;
5. subtracts configured linear friction from velocity magnitude;
6. snaps velocity to exact zero when friction consumes the remaining speed;
7. updates the unchanged-position Tick count; and
8. emits and retains an immutable snapshot.

Friction damps motion, not position. With no new influence, every emotional body
must reach exact rest in finite Tick time. If a positive configured reduction is
smaller than a representable `Single` change, velocity snaps to zero rather than
remaining in motion forever. The body may come to rest at `(-1, 0)`, `(0, 0)`,
or any other valid position; Emotion never invents a return to neutral.

Unchanged position is not synonymous with rest. At a graph boundary, outward
velocity may still be present and undergoing friction while clamping prevents
position from changing.

## Composition-owned no-experience Tick

An assembled cognition composition must continue emotional motion even when no
causal episode closes. `AdvanceEmotionWithoutExperience` supplies exactly one
consecutive Emotion Tick with an empty influence set. It therefore applies the
same integration and friction rules above, but it does not evaluate Ethics,
advance Memory or Desire, invent an event, or publish a cognition transition.

For any native Emotion Tick, the trusted host chooses either this friction-only
composition path or the normal cognition `Process` path carrying evaluated
influence, never both. The host still may not advance the retained Emotion
directly. The ZombieTest binds this choice to its explicit world loop; other
projects must declare their own Agent/world phase without claiming a universal
global clock.

## Snapshots

One snapshot records:

- Tick;
- position and velocity before integration;
- the ordered copied influences and their bounded net impulse;
- integrated velocity before friction;
- resulting position and carried velocity; and
- unchanged-position Tick count; and
- the immutable graph configuration, including axis labels and motion limits.

The runtime retains only a configurable bounded recent window for inspection.
This is not long-term Memory. `KLEPMemory` may copy snapshot position, motion,
and stability evidence into a completed causal experience and retain that a
particular Executable previously moved emotional state from one region toward
another.
The snapshot does not identify its owning Agent and is not a persistence or
restoration format; `KLEPMemory` and its host supply that ownership context.

## Current authority boundary

This Emotion slice is an isolated pure runtime assembly. It does not:

- evaluate Ethics;
- read or mutate Keys or Locks;
- change eligibility or authored score;
- select, interrupt, or advance an Executable or Goal;
- modify Agent Q-values or confidence;
- persist long-term Memory; or
- invoke the Observer's structural mapper, projection, or optional deliberation.

Any behavioral influence derived from Emotion must cross an accepted read-only
evidence boundary only after ordinary eligibility has been established. Emotion
itself never supplies eligibility or a selection command.

## Still open

- The project-owned context schemas and policy implementations that use the
  portable Ethics evaluation interface.
- The project-specific phase that advances Emotion relative to an owning
  Agent's Tick and guarded Neuron Local boundary outside the accepted ZombieTest
  ordering.
- Generic adapters that close a Memory experience after the relevant causal
  Executable and produced Emotion consequence become observable; the ZombieTest
  now defines only its factual bite adapter.
- The desired-region or stabilization policy used to recognize that an Agent
  has remained somewhere it wants to leave.
- Whether correction ranks eligible Executables, eligible Goals, or another
  higher-cognition proposal type.
- How competing Emotion, Memory, Observer-map/projection, optional Observer
  deliberation, and learned influences are attributed, combined, and explained
  without changing eligibility.
