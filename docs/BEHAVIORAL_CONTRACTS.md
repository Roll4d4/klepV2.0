# KLEP behavioral contracts

This file is the registry for behavioral authority and validation tiers. It
does not duplicate subsystem algorithms. Exact rules live in accepted rows of
`DECISIONS.md` and in the referenced contract.

## Reading order

For a behavioral question, use this order:

1. an Accepted entry in `DECISIONS.md`;
2. `KLEP_CONSTITUTION.md` and the approved subsystem contract below;
3. the mapped CoreContract regression suite;
4. an explicitly approved LegacyObserved test, when one exists;
5. public documentation and archaeological source as intent evidence only.

Candidate decisions and candidate integration contracts describe implemented
preview behavior for inspection. They do not override an Accepted decision or
silently become permanent because a build passes.

## Approved pure-runtime contracts

| Contract | Authority boundary | CoreContract suite |
|---|---|---|
| `KEY_CONTRACT.md` | Key identity, values, lifetime, visibility, exact authority, and exchange | `KlepKeySmoke`, `KlepLockSmoke` |
| `EXECUTABLE_CONTRACT.md` | eligibility, score, lifecycle, output boundaries, Neuron arbitration, Tandem settlement, and Goals | `KlepExecutableSmoke` |
| `AGENT_CONTRACT.md` | Agent Tick ownership, environment identity, learning, confidence, and guidance request | `KlepAgentSmoke` |
| `OBSERVER_CONTRACT.md` | read-only holistic evidence, one-use eligible influence, and staleness | `KlepObserverSmoke` |
| `EMOTION_CONTRACT.md` | explicit two-axis emotional body, motion, friction, and snapshots | `KlepEmotionSmoke` |
| `ETHICS_CONTRACT.md` | project-owned contextual evaluation and traced Emotion influence | `KlepEthicsSmoke` |
| `MEMORY_CONTRACT.md` | causal experience, association, heat, trauma, fading, recall, and continuation state | `KlepMemorySmoke` |
| `COGNITION_CONTRACT.md` | Ethics to Emotion to Memory causal composition and read-only Observer evidence | `KlepCognitionSmoke` |

The listed console suites are designated CoreContract regression suites. A
passing suite proves only the rules it asserts; an untested edge case still
requires an accepted decision before its behavior is protected.

## Candidate integration contracts

| Contract | Current status | Validation tier |
|---|---|---|
| `BEHAVIOR_LIBRARY_CONTRACT.md` | Candidate behavior-library and host-observation rules | `KlepBehaviorSmoke`, `KlepPlayerInputSmoke`, `KlepZombieSmoke` |
| `ZOMBIE_TEST_CONTRACT.md` | Candidate Unity demonstration contract around accepted zombie intent | `KlepUnityCompositionSmoke`, headless behavior suites, plus a required Play Mode walkthrough |
| `OBSERVATORY_CONTRACT.md` | Candidate diagnostics and Editor presentation contract | `KlepObservatorySmoke` plus Unity import and Editor inspection |

`OBSERVATORY_USAGE.md` is usage documentation, not semantic authority.
`KlepUnityCompile` is a compile gate, not a behavioral contract suite.

## LegacyObserved status

No test is currently designated LegacyObserved. Legacy code and earlier project
variants may supply archaeological evidence, but they have no authority until a
specific observation is isolated, named, approved, and mapped here. This makes
the absence explicit rather than allowing an old implementation to govern by
accident.

## Acceptance and conflict procedure

A protected rule becomes permanent only when all of the following are true:

- the owner accepts a decision row;
- the governing contract states the same rule;
- deterministic regression coverage exists at the appropriate tier; and
- no higher-authority document contradicts it.

When sources disagree, implementation stops at the conflict. The conflict is
recorded in `DECISIONS.md`; no source is silently selected. A later accepted
row may mark an older row `Superseded`, retaining the history without leaving
two active rules.

## Validation levels

Validation reports keep these claims separate:

- **Compile:** relevant .NET or Unity assemblies compiled.
- **CoreContract:** mapped deterministic headless assertions passed.
- **Unity import:** Unity imported and reloaded the assemblies successfully.
- **Play Mode:** a named scene and interaction were exercised in Unity.
- **Standalone build:** a player build completed and, when claimed, was run.

Passing one level does not imply the levels below it in this list were
performed. In particular, compile success is not behavioral or Play Mode proof.
