# KLEP V2.0

[![CI](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml/badge.svg)](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml)

KLEP V2.0 is the engine-independent C# codebase for a deterministic,
inspectable symbolic behavior system. It gives developers a small vocabulary
for expressing what an agent perceives, what is currently possible, why one
behavior wins, what actually executes, and what evidence remains afterward.

This repository is deliberately **not a Unity project and not a Unity
package**. It is the portable code-and-contract surface: the kernel, behavior
primitives, higher-cognition springboards, architectural contracts, and
executable regression suites. Engine hosts belong at the boundary and
translate their worlds into KLEP observations and effects.

For the tested Unity 6 developer download, use
**[KLEP V2.0 for Unity on itch.io](https://roll4d4.itch.io/klep)**.

KLEP does not claim to reproduce a human mind. Its purpose is the useful kind
of unnatural precision: make a cognition-like process deterministic enough to
test, inspect, reproduce, and explain.

## The working language

- **Keys** are immutable typed fact occurrences perceived inside a Neuron.
- **Locks** are pure conditions over a frozen Key snapshot.
- **Executables** are lifecycle-controlled Sensors, Routers, or Actions.
- **Goals** are scored Executables that own ordered layers of child behavior.
- **Neurons** perform deterministic eligibility, scoring, arbitration, and
  execution.
- **Agents** observe Neuron traces, retain bounded learning evidence, and may
  ask an Observer for guidance.
- **Observers** may rank already-eligible behavior; they cannot make invalid
  behavior eligible.

The ordinary decision path is:

```text
host observation
    -> Sensor / Router Executables
    -> immutable Key snapshot
    -> pure Lock evaluation
    -> eligible root behaviors only
    -> deterministic scoring and arbitration
    -> Action or Goal-owned child lifecycle
    -> immutable trace and declared Key output
```

The optional higher-cognition path is separate and causal:

```text
observed event -> project Ethics -> Emotion -> Memory -> Observer evidence
```

Ethical meaning belongs to the project. Emotion integrates evaluated
influence. Memory records perceived experience and factual lifecycle outcome.
None of those systems may silently manufacture Keys, open Locks, or rewrite
whether an Executable actually succeeded.

## Repository map

```text
src/Roll4d4.Klep/
  Core/          Keys, Locks, Neuron, Executables, Goals, Agent
  Behaviors/     engine-free observation, input, and zombie behavior recipes
  Observer/      eligibility-gated guidance and traceable polish
  Emotion/       two-axis motion, influence, friction, and snapshots
  Ethics/        project-owned contextual evaluation boundary
  Memory/        experience, heat, consolidation, recall, and continuation state
  Cognition/     Ethics -> Emotion -> Memory composition and evidence adapters

tests/           13 executable, dependency-free contract suites
docs/            constitution, contracts, and decision ledger
scripts/         local build-and-test entry point
```

The portable runtime contains 43 implementation files and has no Unity, NuGet,
or third-party runtime dependency. It targets `netstandard2.1`; the contract
suites target .NET 10.

## Build and verify

Install the .NET 10 SDK, then run:

```powershell
dotnet build KLEP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Test-All.ps1
```

The suites are ordinary console programs with hand-written deterministic
assertions. They cover Key identity and exchanges, Lock truth, Executable and
Goal lifecycle, ownership atomicity, arbitration, Agent/Observer evidence,
Emotion, Ethics, Memory, cognition rollback, input behaviors, and the
Wander/Eat Human/Avoid Edge behavior set. The current 13 suites execute 4,163
assertions.

## Host integration

A host owns time, world sampling, entity resolution, and physical effects. A
typical integration does the following:

1. Define stable Key and Executable identities.
2. Translate world observations into immutable Sensor inputs.
3. Express eligibility with Locks and register root Executables or Goals.
4. Construct one `KLEPNeuron`, optionally wrap it in `KLEPAgent`, and advance
   exactly one explicit Tick per decision boundary.
5. Apply only the intent recorded by that completed Tick.
6. Preserve the immutable trace for diagnostics, learning, or replay.

Conceptually:

```csharp
var neuron = new KLEPNeuron("npc.001");
neuron.RegisterExecutable(projectSensor);
neuron.RegisterExecutable(projectGoal);

var agent = new KLEPAgent(neuron, configuration, observer);
KLEPAgentTickTrace trace = agent.Tick();

// The host applies effects from the completed trace after the Tick.
```

Project code supplies the concrete Sensors, Actions, policies, and effect
application. KLEP supplies the deterministic authority and evidence chain.

## Status and boundaries

This is a preview of the V2.0 standalone line. The accepted semantics live in
[`docs/DECISIONS.md`](docs/DECISIONS.md); the constitution and behavioral
contracts define the authority chain. Candidate decisions remain visible and
must not be mistaken for approved Core behavior.

This repository does not claim a general planner, imagination model,
persistence codec, networking layer, universal morality, or automatic engine
integration. The versioned Unity host and Editor Observatory are distributed
as the [KLEP Unity package on itch.io](https://roll4d4.itch.io/klep). That page
links back here as the engine-independent source of the portable system.

## License

KLEP V2.0 is open source under the [MIT License](LICENSE).
