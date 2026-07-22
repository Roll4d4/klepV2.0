# KLEP V2.0

[![CI](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml/badge.svg)](https://github.com/Roll4d4/klepV2.0/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Status: preview](https://img.shields.io/badge/status-preview-orange.svg)](CHANGELOG.md)

KLEP is an engine-independent C# toolkit for **deterministic, inspectable
symbolic behavior arbitration**. It makes perceived facts, eligibility,
scoring, lifecycle, output, and the reason one behavior won available as
immutable evidence.

The Neuron is a passive Key and Executable-catalog substrate. Its exclusive
Agent owns the decision clock, Executable and Goal runtime state, deterministic
arbitration, and history. Every Agent also owns a revisioned recursive map of
the behavior it can currently execute; optional Observer policy may project
complete candidate outcomes or polish scores, but it cannot bypass Locks.

Preview.4 separates additional authorities. Desire evaluates which
project-authored conditions matter now. Learned Expectations retains empirical
effect estimates without deciding value. The Agent may apply that evidence only
to behavior whose Locks already passed, and its Intention ledger records the
Goal commitment that actually followed. None of these components may invent a
Key, open a Lock, or perform a physical effect.

Goal Structural Solution V1 lets one explicitly targeted, otherwise empty root
Solo Goal ask the structural Observer for a deterministic route through the
guaranteed outputs of already-registered root Actions. The Agent caches that
immutable answer, leases only those exact Actions, and invalidates it when the
catalog or mapped shape changes. This is bounded self-mapping over known
capabilities, not general planning.

The separate `Roll4d4.Klep.Imagination` project exposes an even narrower
proposal boundary. A strict Strong Manifest may select one project-admitted
capability version and closed, bounded arguments. Trusted descriptors still
own Locks, outputs, score, lifecycle, success, and the executable factory.
Weak Conjectures remain non-runnable evidence. Preview.4 includes no model
adapter and no mechanism that authors or executes generated code.

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
| Neuron | blackboard and behavior catalog | passive Key storage plus the registered root Executable set |
| Agent | utility selector and scheduler | exclusive decision clock, runtime ownership, deterministic Tandem settlement, then at most one eligible Solo behavior |
| Goal | composite Executable | owns authored child layers or one cached structural solution and may run in Solo or Tandem; it does not search during execution |
| Observer | structural and advisory policy seam | maps the Executable catalog, answers bounded structure queries, and may project or rank eligible roots, but may never bypass Locks |

KLEP's adoption case is not that those ingredients are new. It is their shared
execution contract: immutable evaluation snapshots, staged Key mutations,
barriered Tandem waves, eligibility before influence, stable tie-breaking,
transactional output validation, explicit lifecycle cleanup, and forensic
traces.

## About the cognition-inspired names

Optional modules named Desire, Learned Expectations, Intention, Imagination,
Ethics, Emotion, Memory, Agent learning, and Cognition are bounded computational
mechanisms and project vocabulary:

- Desire evaluates ordered project-authored preferred conditions as
  satisfaction, deficit, and pressure. It does not select behavior.
- Learned Expectations records empirical exact-Key and ActionOwned Desire
  effects with support, variance, prediction error, and confidence. It is a
  critic, not a terminal-value system or planner.
- Intention is the Agent's post-decision ledger for actual root Goal adoption,
  suspension, resumption, completion, and abandonment. It does not add
  stickiness or choose Goals.
- Imagination compiles only closed, project-admitted Strong Manifests into
  fresh Executables whose trusted descriptors own every executable semantic.
  It does not author code, discover capabilities, or make Weak Conjectures
  runnable.
- Ethics applies project-authored contextual rules to produce traced numeric
  influence. It does not solve ethics.
- Emotion integrates influence on two designer-named axes with velocity,
  bounds, friction, and snapshots. It does not claim felt experience.
- Memory associates and recalls perceived episodes using deterministic heat,
  repetition, fading, and scoring. It does not create world truth.
- Agent learning performs a tabular temporal-difference update over symbolic
  state and root behavior identities. It is not general learning or planning.
- Observer policy may map structure, supply a complete projected state, or
  adjust the rank of valid behavior. It cannot make invalid behavior possible,
  and the safe baseline abstains from claiming unknown outcomes.

The older phrase "higher cognition" describes this optional composition seam
inside the project. It is not an empirical result or cognitive-science
classification.

## Evidence, stated at its actual strength

The repository contains 25 project-authored, dependency-free console suites.
The current run executes **4,988 assertions**. That number means assertion
executions, including repetitions inside scenarios; it does not mean 4,988
independent tests.

The suites span approved CoreContract behavior, candidate behavior-library
decisions, portable cognition modules, and bounded zombie scenarios. They are
useful internal regression evidence for the contracts they actually assert.
They are not third-party validation, a performance benchmark, production
adoption evidence, or proof that KLEP is preferable to another architecture.

See [Claims and evidence](docs/CLAIMS_AND_EVIDENCE.md) for the per-suite count,
validation ladder, supported claims, and claims not yet demonstrated.

## Build and verify

The main runtime library targets `netstandard2.1`. The split Imagination
project targets .NET 8 because its strict manifest reader uses the framework's
`System.Text.Json`. Neither project has a `PackageReference`, a Unity
dependency, or a third-party runtime dependency. Install the .NET 8 and .NET
10 SDKs to reproduce the complete contributor build; projects that reference
only the main library may consume its `netstandard2.1` output.

```powershell
dotnet build KLEP.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Test-All.ps1
```

CI executes those project-owned checks on every push and pull request.

## Repository map

```text
src/Roll4d4.Klep/
  Core/          Keys, Locks, passive Neuron, Executables, Goals, Agent runtime
  Behaviors/     engine-free observation, input, and behavior recipes
  Observer/      structural mapping, safe projection seam, traceable polish
  Desire/        authored preferred conditions and attributed raw effects
  LearnedExpectations/  empirical effect estimates and critic evidence
  Emotion/       two-axis motion, influence, friction, and snapshots
  Ethics/        project-owned contextual evaluation boundary
  Memory/        experience, heat, consolidation, recall, continuation state
  Cognition/     explicit Ethics -> Emotion -> Memory composition

src/Roll4d4.Klep.Imagination/
  strict Strong/Weak JSON boundaries and trusted capability materialization

examples/        runnable consumer code
tests/           25 executable internal contract and scenario suites
docs/            constitution, contracts, decisions, evidence, provenance
scripts/         local build-and-test entry point
```

## Distribution and maturity

This repository is deliberately the portable code-and-contract surface, not a
Unity project and not a NuGet distribution (`IsPackable=false`). Unity hosts,
the Editor Observatory, and the versioned UPM tarball live on
[itch.io](https://roll4d4.itch.io/klep).

`2.0.0-preview.4` does not promise API stability, production fitness,
benchmark results, persistence codecs, networking, a general planner, an
imagination model, autonomous capability invention, or automatic project
integration. Evaluate one real
behavior, inspect its trace, measure your workload, and pin the exact reviewed
commit before depending on the preview.

Accepted semantics live in [docs/DECISIONS.md](docs/DECISIONS.md). The
[constitution](docs/KLEP_CONSTITUTION.md) and behavioral contracts define the
authority chain; Candidate decisions remain visible and are not silently
promoted to approved Core behavior.

## Provenance and license

The first public commit is a migration snapshot, not invented incremental
history. The V2.0 rewrite is human-directed and AI-assisted; current independent
review limits are disclosed in [Project provenance](docs/PROJECT_PROVENANCE.md).

KLEP V2.0 is open source under the [MIT License](LICENSE).
