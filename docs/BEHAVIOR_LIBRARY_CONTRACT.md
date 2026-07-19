# KLEP behavior library contract candidate

Status: implementation candidate for owner audit. Ground sensing and the first
reactive zombie chain are implemented. A generic cross-scene/spawned-entity
registry remains outside this slice.

## Host observation boundary

A world sensor is split deliberately:

1. A Unity component samples Physics, input, scene objects, or another external
   system before an Agent decision Tick.
2. A pure Core Tandem Executable receives the sampled value.
3. `KLEPAgent.Tick` exclusively commits one guarded Neuron Local boundary and
   owns all runtime scheduling and firing.
4. The Executable publishes Key operations at the normal Tandem wave barrier.

The runner is the default owner of a Unity `Update` and is an optional frame
host for `TickOnce`. Sensors, Executables, the Agent, and the Neuron have no
independent `Update`/`FixedUpdate` decision path. A server, test, turn
controller, or explicit world host can disable automatic ticking and call
`TickOnce` itself. A host coordinating several Agent/Neuron pairs must declare
their Agent-Tick and external-state synchronization order.

Observation sources are registered and sampled in ordinal stable-ID order.
Inactive or disabled sources remain registered but are sampled as false, which
prevents their last observation from leaking into later Agent Ticks.

Source membership is frozen when the runner initializes. Adding, reparenting,
or removing sensor components at runtime is not silently reflected in the Core
registry. Destroying a registered source stops the runner before another Agent
Tick so it cannot proceed with a stale observation. Dynamic behavior membership
needs a later explicit, boundary-staged contract.

## Ground Sensor

The Ground slice contains:

- `KLEPGroundSensor`, the Unity scene `PhysicsScene.OverlapSphere` adapter;
- `KLEPObservedKeySensorExecutable`, the pure presence-to-Key behavior;
- `KLEPNeuronRunner`, the Unity host that samples then calls its Agent Tick;
- `KLEPKeyDefinitionCache`, which compiles each assigned Key asset once for the
  runner and rejects different assets that claim the same stable Key ID.

The Ground Key must be Local and `OneCycle`:

- sampled true: add one Ground occurrence;
- sampled false: add nothing and do not report a successful run whose declared
  Ground output was omitted;
- repeated true: the old occurrence expires and one current occurrence is
  emitted, so Ground does not accumulate;
- true can unlock a Solo behavior later in the same Agent Tick;
- false on a later Agent Tick leaves Ground absent before Solo selection.

The sensor uses its assigned center Transform, or its own Transform when center
is empty. Radius must be finite and greater than zero. The layer mask and trigger
policy define what counts as ground. The mask defaults to no layers so an
unconfigured sensor cannot mistake its agent's own collider for terrain; an
empty mask is rejected during initialization rather than silently staying false.

### Unity setup

1. Put `KLEPNeuronRunner` on the agent root and author a stable Neuron ID.
2. Put `KLEPGroundSensor` on that root or one of its owned children.
3. Author a stable sensor ID, usually such as `sensor.ground`.
4. Assign the feet Transform, probe radius, dedicated ground layers that exclude
   the agent's collider layers, and a Local `OneCycle` Ground `KLEPKeyAsset`.
5. Leave automatic Tick enabled for one Agent decision Tick per Unity frame, or turn
   it off when another simulation host calls `TickOnce`.

The runner also discovers `IKLEPExecutableProvider` components for pure Action,
Router, and Goal Executables, plus `IKLEPAgentTickSink` components that apply
Unity effects after the completed Agent Tick. Providers and sinks are sorted by stable
ID and must remain active while their runner advances. Generic Inspector-first
authoring components remain later behavior-library slices; the zombie test uses
one deliberately narrow controller provider/sink.

## Key data is still data

Keys carry payload fields. The deterministic rewrite changes how a payload is
modified, not whether it exists:

- old model: retain a mutable object and change it in place;
- current model: find one exact `KLEPKeyFact`, construct a new immutable
  `KLEPKeyPayload`, and emit `Replace(exactFact, newPayload)`;
- the replacement becomes visible at its approved boundary;
- older snapshots retain the old payload and remain truthful.

For a continuously sampled fact, replacement is often unnecessary. A
`OneCycle` sensor can emit a fresh occurrence with the current payload every
Agent Tick. If the observation disappears, the prior occurrence expires naturally.
A persistent fact such as Health or ChosenTarget is updated with exact-fact
replacement.

This is why Boolean, Int64, finite Double, and Text payload values exist now.
It is also why Core does not currently store a raw `Transform`, `GameObject`, or
arbitrary `object`: those references have Unity lifetime, thread, destroyed-null,
and serialization behavior that an immutable deterministic snapshot cannot own.

## Enemy Sensor and zombie test

The enemy sensor publishes one Local `OneCycle` `EnemyDetected` occurrence
per observed enemy. Several occurrences of one Key ID already coexist, so each
enemy does not need a different Key definition.

A practical first payload is:

```text
entityId    Text      stable runtime/world entity handle
teamId      Text      stable team handle (or Int64 when the game owns numeric IDs)
distance    Number    sampled distance
positionX   Number    sampled world position
positionY   Number
positionZ   Number
```

The Unity adapter:

1. overlap the detection volume;
2. reject self and non-entity colliders;
3. collapse several colliders belonging to the same entity;
4. sort detections by `entityId` using ordinal comparison;
5. register `entityId -> Unity entity/Transform` in a Unity-side resolver;
6. supply the ordered immutable payloads before the Agent Tick.

The pure Core sensor emits one declared `EnemyDetected` Add per supplied
payload. With no supplied enemy it emits nothing and does not report a
successful run that omitted `EnemyDetected`. Re-emission updates current
position and distance without mutating an old fact. Missing enemies are not
re-emitted, so their observations expire.

Behaviors that only reason symbolically read the payload directly. A Unity
movement or aiming adapter that needs a live Transform resolves `entityId`
through the Unity registry at execution time and handles a missing/destroyed
entity as an ordinary failed observation. The raw Unity object never enters the
Core Key snapshot.

A future durable chosen target should be a separate Persistent Local
`ChosenTarget` fact. A
target-selection behavior can add it initially and later replace its exact fact
to change the entity handle or last-known data. This separates transient sensor
truth from durable intent.

The zombie test deliberately uses a smaller scene-local contract: every entity
has an authored text ID and team ID, and `KLEPEnemySensor` retains only its latest
sample's `entityId -> KLEPEntityIdentity` map. `KLEPZombieController` is given
that sensor explicitly and resolves a live target only after the Agent Tick.
There is no static/global registry. Spawned-ID policy, persistence, and generic
cross-scene ownership remain open.

The original pre-Goal vertical slice was the following historical conditional
Router shape; it is not the current guaranteed-output authoring contract:

```text
Ground Sensor ───────────────> Ground
Enemy Sensor ────────────────> EnemyDetected[]
Ground + EnemyDetected[] ────> nearest-target Router
                                 ├─ outside range ─> MoveTarget
                                 └─ inside range ──> AttackTarget
Ground + MoveTarget ─────────> Move Solo (Running)
Ground + AttackTarget ───────> Attack Solo (Succeeded)
```

The guaranteed-output replacement uses two independently truthful Tandem
Executables: a MoveTarget Router that declares only MoveTarget and succeeds only
outside range, and an AttackTarget Router that declares only AttackTarget and
succeeds only inside range. A nonmatching route emits nothing and does not claim
success. They run in a later Tandem wave than the Sensors, so exactly one target
is available to final Solo selection in the same Agent Tick. Unity movement is
measured in authored units per Agent Tick, and the attack effect is applied only
from that Tick's succeeded Attack trace.

## Zombie Goal showcase

The release showcase lifts those actions into an inspectable Goal hierarchy
without changing Core scheduling:

```text
Ground ring -- incomplete support --> EdgeDanger
Ground + no edge --------------------> deterministic WanderDirection Router
Ground + EnemyDetected -------------> nearest-target Router
                                        |-- far  --> MoveTarget
                                        `-- near --> AttackTarget

100  Goal Avoid Edge -- Any [Avoid Edge action (Running)]
 50  Goal Eat Human  -- Any [Attack (Succeeded), Move (Running)]
 10  Goal Wander     -- Any [Wander action (Running)]
```

In that compact diagram, "nearest-target Router" is shorthand for the two
separately declared range routes above; it is not one successful Executable with
conditional declared output.

Each Goal is one root Solo candidate and owns its children exclusively. The
Agent sees only the three Goals during Solo arbitration. Eat Human considers
Attack before Move in authored order: a blocked Attack permits Move to run;
an eligible Attack succeeds first and completes that Goal. Avoid Edge and
Wander deliberately remain Running while their factual Locks remain open.

Avoid Edge excludes neither challenger by score alone. `EdgeDanger` positively
opens Avoid Edge and negatively closes Eat Human and Wander. EnemyDetected does
not close Wander: it opens Eat Human while Wander remains eligible, making the
strict score-based interruption visible. Eligibility is still resolved before
either score participates.

The edge adapter issues eight separate downward raycasts around the body in
fixed clockwise order: front, front-right, right, back-right, back, back-left,
left, front-left. Physics remains in Unity. A pure reducer copies the support
mask and derives a finite local avoidance vector. Full support emits no
EdgeDanger and the sensor does not claim successful completion with that
declared output omitted; symmetric missing patterns use the lowest missing
ordinal as a stable fallback rather than engine randomness.

The deterministic wander Router uses an explicit unsigned seed, a positive
segment length, and a fixed table of eight normalized local headings. Its
OneCycle WanderDirection payload is recreated each Agent Tick. Wall time, Unity
randomness, `System.Random`, and process-dependent string hashing do not
participate.

The Unity controller retains its owned action objects only as a narrow effect
adapter. After the completed Agent Tick, it applies an intent only when the child's
recorded cycle equals that trace's cycle. Children do not become Neuron roots,
and the Observatory continues to show them recursively beneath their Goal.

## Player input and locomotion

The zombie-test target uses the same boundary. Demo-only Input System adapters
sample keyboard and mouse state only when its runner requests an observation:

```text
Keyboard sample --------> W / A / S / D presence Keys
Mouse screen position --> horizontal aim plane --> MouseAim XYZ Key
Ground + MouseAim ------> Player Locomotion Solo (Running)
                                   |
                                   +--> rotate, then normalized local movement
```

All five input Keys are Local `OneCycle` facts. Opposing directions cancel,
diagonals normalize, and no held direction means aim-only rotation. W therefore
means forward relative to the facing produced by the same Agent Tick's MouseAim
fact.
The pure behavior and production controller do not depend on Unity's Input
System; only the dedicated demo sensor assembly reads the devices.
