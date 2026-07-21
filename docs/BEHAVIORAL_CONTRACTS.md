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
| `EXECUTABLE_CONTRACT.md` | eligibility, guaranteed successful outputs, lifecycle, Solo/Tandem modes, and Goal recipe semantics | `KlepExecutableSmoke` |
| `AGENT_CONTRACT.md` | exclusive Agent Tick, runtime/Goal ownership, Tandem settlement, Solo arbitration, projected satisfaction, optional learned-Desire contribution, navigation learning, confidence, and guidance request | `KlepAgentSmoke`, `KlepExecutableSmoke`, `KlepLearnedExpectationsSmoke` |
| `INTENTION_CONTRACT.md` | Agent-owned post-decision adoption, suspension, resumption, completion, abandonment, and frozen intention evidence | `KlepIntentionSmoke`, `KlepAgentSmoke` |
| `OBSERVER_CONTRACT.md` | recursive catalog validation/mapping, evidence-bound self-models, deterministic read-only queries and structural dependency proposals, read-only learned-expectation consumption, read-only projections, optional low-confidence deliberation, one-use eligible influence, and staleness | `KlepObserverSmoke` |
| `LEARNED_EXPECTATIONS_CONTRACT.md` | independent exact later-Key evidence plus ActionOwned raw Desire-effect critic estimates, support, variance, prediction error, confidence, read-only snapshots, and the eligibility-gated Agent consumption boundary | `KlepLearnedExpectationsSmoke`, `KlepAgentSmoke`, `KlepObserverSmoke` |
| `EMOTION_CONTRACT.md` | explicit two-axis emotional body, motion, friction, and snapshots | `KlepEmotionSmoke` |
| `DESIRE_CONTRACT.md` | project-authored preferred conditions, satisfaction, deficit, pressure, raw experienced effects, and attribution | `KlepDesireSmoke` |
| `ETHICS_CONTRACT.md` | project-owned contextual evaluation and traced Emotion influence | `KlepEthicsSmoke` |
| `MEMORY_CONTRACT.md` | causal experience, association, heat, trauma, fading, recall, and continuation state | `KlepMemorySmoke` |
| `COGNITION_CONTRACT.md` | Ethics to Emotion to Memory causal composition and read-only Observer evidence | `KlepCognitionSmoke` |

The listed console suites are designated CoreContract regression suites. A
passing suite proves only the rules it asserts; an untested edge case still
requires an accepted decision before its behavior is protected.

## Accepted architecture migration

The authority documents now describe the accepted Agent-owned scheduler
architecture. The runtime and existing suites may temporarily retain historical
`KLEPNeuron.Tick`, Neuron-owned runtime, Solo-only Goal, possible-output, or
low-confidence-only Observer assumptions while that migration is in progress.
Those historical behaviors do not override the Accepted decisions.

Migration is not CoreContract-complete until deterministic regressions prove:

- one Agent exclusively owns each passive Neuron decision boundary;
- the Agent owns every root and Goal-descendant runtime and trace;
- existing Tandem waves, Solo interruption, output timing, and teardown remain
  behaviorally identical under Agent ownership;
- Solo and Tandem Goals follow their distinct scoring and output boundaries;
- every `Succeeded` run emits every `DeclaredOutput`;
- invalid catalog revisions are rejected before activation and unchanged valid
  maps are reused by exact revision;
- Observer self-models and explicit query results bind the accepted map and
  immutable completed evidence without creating selection, execution, or Goal
  recipe authority; and
- projected satisfaction, optional learned-Desire contribution, and optional
  low-confidence deliberation remain separate and inspectable; the learned
  contribution uses one frozen Desire snapshot and one frozen critic snapshot,
  explicit root-to-effect-action bindings, and never queries an ineligible Solo
  or Tandem.

## Candidate integration contracts

| Contract | Current status | Validation tier |
|---|---|---|
| `BEHAVIOR_LIBRARY_CONTRACT.md` | Candidate behavior-library and host-observation rules | `KlepBehaviorSmoke`, `KlepPlayerInputSmoke`, `KlepZombieSmoke` |

The separately distributed Unity package also carries candidate
`ZOMBIE_TEST_CONTRACT.md` and `OBSERVATORY_CONTRACT.md` files, plus
`OBSERVATORY_USAGE.md`. They and their Unity-specific validation projects are
deliberately outside this engine-independent source mirror. `KlepUnityCompile`
is a compile gate, not a behavioral contract suite.

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
