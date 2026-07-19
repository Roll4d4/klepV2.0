# KLEP V2.0

[![CI](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml/badge.svg)](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Status: preview](https://img.shields.io/badge/status-preview-orange.svg)](CHANGELOG.md)

KLEP is an engine-independent C# toolkit for **deterministic, inspectable
symbolic behavior arbitration**. It makes perceived facts, eligibility,
scoring, lifecycle, output, and the reason one behavior won available as
immutable evidence.

This is preview source. It is not a claim of human cognition, a scientific
validation of a cognitive architecture, or a proven replacement for behavior
trees, GOAP, utility AI, state machines, or rule engines. Start with
[Claims and evidence](docs/CLAIMS_AND_EVIDENCE.md) for the exact boundary and
[When to use KLEP](docs/WHEN_TO_USE_KLEP.md) for an honest comparison.

For the tested Unity 6 package and Editor Observatory, use
**[KLEP V2.0 for Unity on itch.io](https://roll4d4.itch.io/klep)**.

## Run one real decision

Install the .NET 10 SDK, clone the repository, and run:

```powershell
dotnet run --project examples/KlepMinimalConsole/KlepMinimalConsole.csproj
```

The [minimal consumer](examples/KlepMinimalConsole/README.md) is executable
project code, not pseudocode. A host supplies `humanInRange=false`, `true`, and
`false`. KLEP then runs a Tandem Sensor, produces a one-cycle Key, evaluates an
`Eat human` Goal and a lower-scored `Wander` fallback, executes the winner, and
leaves an intent in the completed trace for the host to apply. The scenario is
replayed and fails if identical input produces a different trace signature.

## The execution contract

```text
host observation
    -> Sensor and Router Executables settle in deterministic Tandem waves
    -> immutable Key snapshot
    -> pure Lock evaluation
    -> eligible root behaviors only
    -> deterministic scoring and arbitration
    -> Action or Goal-owned child lifecycle
    -> immutable trace and declared Key output
    -> host applies effects after the completed Tick
```

The familiar concepts have stricter local meanings:

| KLEP term | Nearest familiar idea | Contracted KLEP boundary |
|---|---|---|
| Key | typed blackboard fact | immutable occurrence with scope, payload, lifetime, provenance, and store authority |
| Lock | predicate or precondition | pure evaluation over one frozen Key snapshot |
| Executable | action, sensor, rule, or behavior node | declared output plus explicit lifecycle, teardown, and trace evidence |
| Neuron | utility selector and scheduler | deterministic Tandem settlement followed by at most one eligible Solo behavior |
| Goal | scored composite | owns ordered child layers; it is not a search planner |
| Observer | advisor or score influence | may rank eligible roots and may never bypass Locks |

KLEP's adoption case is not that those ingredients are new. It is their shared
execution contract: immutable evaluation snapshots, staged Key mutations,
barriered Tandem waves, eligibility before influence, stable tie-breaking,
transactional output validation, explicit lifecycle cleanup, and forensic
traces.

## About the cognition-inspired names

Optional modules named Ethics, Emotion, Memory, Agent learning, and Cognition
are bounded computational mechanisms and project vocabulary:

- Ethics applies project-authored contextual rules to produce traced numeric
  influence. It does not solve ethics.
- Emotion integrates influence on two designer-named axes with velocity,
  bounds, friction, and snapshots. It does not claim felt experience.
- Memory associates and recalls perceived episodes using deterministic heat,
  repetition, fading, and scoring. It does not create world truth.
- Agent learning performs a tabular temporal-difference update over symbolic
  state and root behavior identities. It is not general learning or planning.
- Observer evidence may adjust the rank of valid behavior. It cannot make an
  invalid behavior possible.

The older phrase "higher cognition" describes this optional composition seam
inside the project. It is not an empirical result or cognitive-science
classification.

## Evidence, stated at its actual strength

The repository contains 13 project-authored, dependency-free console suites.
The current run executes **4,163 assertions**. That number means assertion
executions, including repetitions inside scenarios; it does not mean 4,163
independent tests.

Nine suites are mapped to approved CoreContract behavior. Three exercise
candidate behavior-library decisions, and one is the Zombie Goal scenario.
They are useful internal regression evidence for the contracts they actually
assert. They are not third-party validation, a performance benchmark,
production adoption evidence, or proof that KLEP is preferable to another
architecture.

See [Claims and evidence](docs/CLAIMS_AND_EVIDENCE.md) for the per-suite count,
validation ladder, supported claims, and claims not yet demonstrated.

## Build and verify

The runtime library targets `netstandard2.1` and has no Unity, NuGet, or
third-party runtime dependency. The repository's contributor harness and
examples use the .NET 10 SDK; a consuming application does not need to target
.NET 10 if it can reference a `netstandard2.1` library.

```powershell
dotnet build KLEP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Test-All.ps1
```

CI executes those project-owned checks on every push and pull request.

## Repository map

```text
src/Roll4d4.Klep/
  Core/          Keys, Locks, Neuron, Executables, Goals, Agent
  Behaviors/     engine-free observation, input, and behavior recipes
  Observer/      eligibility-gated guidance and traceable polish
  Emotion/       two-axis motion, influence, friction, and snapshots
  Ethics/        project-owned contextual evaluation boundary
  Memory/        experience, heat, consolidation, recall, continuation state
  Cognition/     explicit Ethics -> Emotion -> Memory composition

examples/        runnable consumer code
tests/           13 executable internal contract and scenario suites
docs/            constitution, contracts, decisions, evidence, provenance
scripts/         local build-and-test entry point
```

## Distribution and maturity

This repository is deliberately the portable code-and-contract surface, not a
Unity project and not a NuGet distribution (`IsPackable=false`). Unity hosts,
the Editor Observatory, and the versioned UPM tarball live on
[itch.io](https://roll4d4.itch.io/klep).

`2.0.0-preview.1` does not promise API stability, production fitness, benchmark
results, persistence, networking, a general planner, an imagination model, or
automatic project integration. Evaluate one real behavior, inspect its trace,
measure your workload, and pin the exact reviewed commit before depending on
the preview.

Accepted semantics live in [docs/DECISIONS.md](docs/DECISIONS.md). The
[constitution](docs/KLEP_CONSTITUTION.md) and behavioral contracts define the
authority chain; Candidate decisions remain visible and are not silently
promoted to approved Core behavior.

## Provenance and license

The first public commit is a migration snapshot, not invented incremental
history. The V2.0 rewrite is human-directed and AI-assisted; current independent
review limits are disclosed in [Project provenance](docs/PROJECT_PROVENANCE.md).

KLEP V2.0 is open source under the [MIT License](LICENSE).
