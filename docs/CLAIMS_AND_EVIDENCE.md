# Claims and evidence

This page defines what the public KLEP V2.0 preview currently supports saying.
It separates implementation evidence from inference, comparison, and external
validation.

## The narrow claim

KLEP is an engine-independent C# toolkit for deterministic, inspectable symbolic
behavior arbitration. Its vocabulary makes perceived facts, pure conditions,
behavior eligibility, scoring, lifecycle, output, and causal traces explicit.

That is an engineering claim. It is not a claim that KLEP reproduces human
cognition, is scientifically validated as a cognitive architecture, or is a
better general solution than behavior trees, GOAP, utility AI, state machines,
or rule engines.

## What the names mean

KLEP uses cognition-inspired names for optional, bounded mechanisms:

| KLEP name | Implemented mechanism | What the name does not establish |
|---|---|---|
| Ethics | Project-authored, ordered contextual rules producing traced influence on two designer-named axes | A universal moral system, moral truth, or ethical reasoning |
| Emotion | A bounded two-axis state with velocity, accumulated influence, friction, and immutable snapshots | Felt experience, affective science validity, or human emotion |
| Memory | Deterministic episode association, repetition, heat, retention, detail fading, and pure recall scoring | Autobiographical consciousness, conjecture, or current world truth |
| Observer | Read-only evidence sources contributing explained numeric influence to already-eligible root behaviors | A planner, world model, or authority to bypass Locks |
| Agent learning | A tabular semi-Markov temporal-difference update over a symbolic state signature and root action ID | General learning, transfer learning, model learning, or planning |

The repository sometimes groups these modules under “higher cognition.” Here
that phrase is project vocabulary for a constrained composition seam. It is not
an empirical result or a cognitive-science classification. The exact boundaries
are stated in the [constitution](KLEP_CONSTITUTION.md) and subsystem contracts.

## Current internal evidence

The runtime targets `netstandard2.1` and has no third-party runtime dependency.
The repository includes 13 dependency-free console suites. CI builds the
solution and executes every suite through [`scripts/Test-All.ps1`](../scripts/Test-All.ps1).

The current total is **4,163 assertion executions**, not 4,163 distinct tests,
independently specified requirements, or externally reviewed cases:

| Executable suite | Assertion executions |
|---|---:|
| `KlepAgentSmoke` | 74 |
| `KlepBehaviorSmoke` | 52 |
| `KlepCognitionSmoke` | 34 |
| `KlepEmotionSmoke` | 43 |
| `KlepEthicsSmoke` | 34 |
| `KlepExecutableSmoke` | 344 |
| `KlepKeySmoke` | 848 |
| `KlepLockSmoke` | 113 |
| `KlepMemorySmoke` | 75 |
| `KlepObserverSmoke` | 78 |
| `KlepPlayerInputSmoke` | 136 |
| `KlepZombieGoalSmoke` | 2,203 |
| `KlepZombieSmoke` | 129 |
| **Total** | **4,163** |

Some assertions execute repeatedly inside scenario loops. The suites were
written with the implementation and its project-owned contracts. They are
useful regression evidence that the checked implementation follows the rules
the suites actually assert. They are not independent confirmation that those
rules are complete, useful for another project, performant, or preferable to
another architecture. As [BEHAVIORAL_CONTRACTS.md](BEHAVIORAL_CONTRACTS.md)
states, an untested edge case remains unprotected.

## Evidence ladder

Different validation levels answer different questions:

| Level | What it can establish | Public status |
|---|---|---|
| Source and contract inspection | The declared algorithms, boundaries, and unresolved decisions are reviewable | Present in this repository |
| Compile | The selected projects compile under the documented SDK and configuration | Automated in CI |
| Approved CoreContract execution | The assertions mapped to accepted pure-runtime contracts pass | Automated in CI for 9 designated suites |
| Candidate and scenario regression execution | Implemented preview behavior continues to match its authored scenarios | CI also runs 3 mapped behavior-library suites and the Zombie Goal scenario suite |
| Unity import and Editor inspection | A particular Unity package imports and its tooling can be inspected | A separate tested developer package is distributed on [itch.io](https://roll4d4.itch.io/klep); it is not evidence supplied by this engine-free repository |
| Play Mode or standalone run | A named scene and interaction worked in a named Unity version | Must be reported separately for each build and scenario |
| Independent validation | An outside user, implementation, benchmark, replication, or deployment corroborates usefulness | Not yet documented here |

Passing a higher row in this table does not silently prove a lower or different
kind of claim. In particular, a successful compile does not prove behavior, and
internal contract execution does not prove production fitness.

## Claims supported by this repository

Subject to the inputs and ownership rules in the contracts, the source and
regression suites support these bounded claims:

- Core uses explicit caller-owned Tick boundaries rather than hidden engine
  timing.
- Lock evaluation and scoring operate on immutable Key snapshots and do not
  mutate runtime state.
- eligibility is established before Agent or Observer influence is considered;
- Tandem behaviors use deterministic snapshot-and-barrier waves before Solo
  arbitration;
- stable ordinal ordering resolves otherwise unordered work and score ties;
- Executables have explicit lifecycle, cancellation, fault, output, and cleanup
  paths;
- Key-output batches are preflighted, and failed batches restore pending store
  state as specified by the contract;
- decisions and material higher-cognition influence retain immutable,
  inspectable evidence; and
- Core and the portable cognition modules do not depend on Unity APIs.

“Deterministic” is conditional, not magical. The
[constitution](KLEP_CONSTITUTION.md) requires identical definitions, initial
state, ordered inputs, explicit clocks, and random seed. Host side effects,
uncontrolled concurrency, and mutable project policy remain the host's
responsibility.

## Claims not currently demonstrated

The public project does not currently establish:

- human-like or “higher” cognition as a scientific accomplishment;
- a learned world model, abstraction formation, counterfactual simulation,
  general planning, or transfer learning;
- superiority to behavior trees, GOAP, utility AI, state machines, or rule
  engines;
- performance, memory-use, or scaling characteristics under representative
  workloads;
- production reliability, long-term API stability, or backward compatibility;
- security against untrusted authored or generated code;
- external adoption, third-party tests, independent replication, or a published
  empirical evaluation; or
- universal ethical or emotional meaning.

Absence from this list is not evidence against the project; it is a boundary on
what has been shown. New public evidence should name its setup, version,
workload, expected result, observed result, and whether it was produced by the
project author or independently.

## Preview and adoption boundary

`2.0.0-preview.1` is auditable preview source, not a promise of a stable
production dependency. The standalone project deliberately sets
`IsPackable=false`; this repository is not currently a NuGet distribution. The
runtime library targets `netstandard2.1`, while reproducing the current contract
suites requires the .NET 10 SDK pinned in `global.json`.

Prospective adopters should first read [WHEN_TO_USE_KLEP.md](WHEN_TO_USE_KLEP.md),
prototype one real behavior, run the suites, inspect the applicable accepted
decisions, measure their own workload, and pin the exact reviewed commit. That
is a stronger trust basis than a subsystem name or a large assertion count.
