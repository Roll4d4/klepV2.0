# When to use KLEP

KLEP shares important building blocks with established behavior architectures.
Its adoption case is not that facts, predicates, actions, scoring, or composites
are new. Its case is a particular execution contract around those familiar
ideas: immutable evidence, staged mutation, deterministic settlement, explicit
lifecycle, and forensic traces.

## Translate the vocabulary first

These are nearest conventional analogues, not exact equivalences:

| KLEP concept | Familiar analogue | KLEP-specific boundary |
|---|---|---|
| Key | Typed blackboard fact | An immutable occurrence with scope, payload, lifetime, provenance, activation timing, and store authority |
| Lock | Predicate or action precondition | A pure condition over one frozen Key snapshot; Locks may contribute authored attractiveness but cannot mutate state |
| Executable | Action, sensor, derivation rule, or behavior node | A registered behavior with Solo/Tandem mode, declared outputs, explicit lifecycle, teardown, and trace evidence |
| Neuron | Blackboard plus behavior catalog | Passively owns Key stores and the registered root Executable set; it does not schedule or advance behavior |
| Goal | Composite behavior or scored utility action | Exclusively owns ordered child layers and may run in Solo or Tandem; it is not a search planner |
| Agent | Deterministic scheduler plus optional evidence consumers | Exclusively owns the Tick path, all Executable runtime state, arbitration, Intention state, recursive map, and decision history |
| Observer | Structural and advisory policy layer | Maps the catalog, queries read-only learned evidence, and may provide complete candidate projections or explained influence only for roots whose Locks already passed |
| Desire | Project-authored terminal-value evaluator | Evaluates satisfaction, deficit, pressure, and raw transition effects; it neither learns nor selects |
| Learned Expectations | Empirical critic | Retains observed effects and confidence independently of terminal value and execution |
| Intention | Commitment ledger | Records the root Goal the Agent actually adopted and its later runtime disposition; it is not a planner |

If a team cannot map this vocabulary onto concepts it already understands, it
should not adopt KLEP merely because the names sound cognitive.

## What is actually different

KLEP concentrates several rules that are optional or implementation-specific in
many other systems:

- **Immutable evaluation snapshots.** Peers evaluate a stable view instead of
  reading mutations caused by callback order.
- **Staged Key operations.** Visibility changes at contracted top-level or
  Tandem-wave boundaries rather than immediately and implicitly.
- **Deterministic Tandem settlement.** Eligible sensors and routers in one wave
  observe the same snapshot, publish together, and may expose a new snapshot to
  previously blocked Tandems before Solo selection.
- **Eligibility before influence.** Authored Locks decide possibility before
  utility, Agent learning, learned Desire effects, Memory, Emotion, or Observer
  evidence can rank it.
- **Separated value, expectation, and choice.** Desire states what matters,
  Learned Expectations records what tended to follow, and only the Agent may
  combine frozen evidence to choose among already-eligible behavior.
- **Inspectable commitment.** Intention records actual Goal adoption,
  suspension, resumption, completion, and abandonment without silently adding
  stickiness or planning authority.
- **Explicit lifecycle and teardown.** Enter, Running, terminal state, Exit,
  Cleanup, cancellation, and faults are named and traced.
- **Transactional output validation.** A complete output batch is preflighted;
  invalid operations do not leave a partially staged batch under the contracted
  store boundary.
- **Stable arbitration.** Stable IDs and a contracted current-Solo rule remove
  collection-order tie behavior.
- **Forensic evidence.** Snapshots, eligibility, score components, lifecycle
  steps, Key operations, guidance, and faults are retained as immutable traces.
- **Engine boundary.** The portable runtime owns symbolic decisions; a host owns
  clocks, world sampling, entity resolution, and physical effects.

Those properties are most relevant when exact reproducibility and explanation
matter more than minimal authoring ceremony.

## Choose KLEP when

KLEP is a plausible preview candidate when several of these are requirements:

- identical explicit inputs must produce the same symbolic decision trace;
- replay, lockstep simulation, experiment replication, or deterministic bug
  reports matter;
- the team must answer “which facts were visible, which candidates were
  blocked, and why did this action win?” after the fact;
- sensors or derivation rules must settle without callback-order races before
  one action is selected;
- lifecycle interruption, failure, cancellation, and cleanup need contracted
  semantics rather than per-node convention;
- learned or project-specific advice must never make an invalid behavior
  eligible;
- the behavior kernel must remain independent of one engine's frame lifecycle;
  or
- the team is willing to audit, vendor, and help evaluate a young source-first
  framework.

Candidate domains include deterministic games, replayable simulations,
research prototypes, agent-debugging tools, and systems where an inspectable
decision record is itself a deliverable. These are suitability hypotheses, not
published performance or production claims.

## Prefer an established alternative when

| Need | Usually start with | Why KLEP is not the default answer today |
|---|---|---|
| Mature visual authoring, engine marketplace support, and a large user community | Established behavior tree package | The standalone preview has no visual graph authoring and limited public adoption evidence |
| Dynamic multi-step plan search over desired world states | GOAP or another planner | KLEP currently has no general planner or learned world model; Goals are owned composites, not plan search |
| Straightforward ranking of independent actions | Established utility-AI package | KLEP adds snapshot, wave, lifecycle, and trace ceremony that may be unnecessary |
| Production inference over a large fact/rule base | Established rule engine | KLEP's primary abstraction is behavior arbitration and lifecycle, not a general inference agenda |
| A small, explicit set of modes and transitions | Finite or hierarchical state machine | A state machine is often easier to author and review for a genuinely small state space |
| Proven throughput, memory bounds, support policy, and long-lived API compatibility | A measured production dependency | KLEP does not yet publish representative benchmarks, a stable-API policy, or production case studies |

An established alternative is not a failure to understand KLEP. For ordinary
game AI, it may be the lower-risk engineering choice.

## Hybrid use

KLEP does not need to own every level of a project. A host-side planner,
animation controller, navigation system, or behavior tree can remain outside
the kernel while a KLEP Executable represents the contracted decision and
lifecycle boundary. Conversely, another system can own high-level intent while
KLEP arbitrates a narrow set of eligible actions.

There is no built-in general interoperability layer in the standalone preview.
The project must define how external state becomes immutable observation, how
one completed Tick becomes host intent, and which system owns cancellation and
side effects. Do not let two schedulers silently advance the same behavior.

## Cost of adoption

Before adopting, budget for:

- learning project-specific vocabulary and reading the accepted contracts;
- authoring stable identities, Key definitions, Locks, Executables, Goals, and
  host effect application explicitly;
- deciding which Candidate semantics, if any, the project is willing to rely
  on;
- validating determinism at the host boundary, not only inside Core;
- measuring representative candidate counts, payloads, trace retention, and
  Tick frequency yourself;
- vendoring a reviewed commit or using the separately distributed Unity package
  rather than expecting a NuGet package; and
- absorbing preview API change until a stability policy exists.

The regression suites currently require the pinned .NET 10 SDK even though the
runtime library targets `netstandard2.1`. That affects contributors reproducing
the repository checks; it does not require a consuming application to target
.NET 10.

## A low-risk evaluation

1. Read [CLAIMS_AND_EVIDENCE.md](CLAIMS_AND_EVIDENCE.md), the
   [constitution](KLEP_CONSTITUTION.md), and the contracts governing the slice
   you intend to use.
2. Run `scripts/Test-All.ps1` and preserve the exact commit and output.
3. Build one small consumer around a real project decision, not a synthetic
   assertion count.
4. Inspect its immutable trace and verify that it answers the debugging question
   that motivated the evaluation.
5. Compare authoring effort, decision quality, trace usefulness, CPU time, and
   allocation behavior with the simplest established alternative.
6. Exercise cancellation, faults, save/load boundaries, and engine integration
   that matter to the project.
7. Adopt only the reviewed slice, pin its version, and record project-specific
   policies and measurements.

The [Unity developer package on itch.io](https://roll4d4.itch.io/klep) is the
appropriate route for evaluating the Unity 6 host and Editor Observatory. This
repository remains the engine-independent source and contract surface.
