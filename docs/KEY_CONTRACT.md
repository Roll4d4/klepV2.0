# KLEP Key contract

Status: approved pure-runtime contract. Exact accepted rules are identified in
[DECISIONS.md](DECISIONS.md). Candidate Unity and project-integration behavior
remains governed separately.

## Definition and occurrence

- `KLEPKeyDefinition` is immutable Core definition data.
- `KLEPKeyAsset` compiles authored Unity data into a definition and never holds
  live Neuron state.
- `KLEPKeyId` is ordinal and case-sensitive. Display-name changes do not alter
  matching.
- `KLEPKeyFact` is one immutable emitted occurrence with definition, payload,
  lifetime, `SourceId`, issued tick, activated tick, and occurrence ID.
- A fact carries opaque store authority internally. A fact from another store
  cannot remove or replace a local occurrence even if textual trace IDs collide.

## Scope and ownership

- Local is the default scope. Each Neuron privately owns one Local store.
- Global definitions require a caller-supplied Global store.
- The world/coordinator commits that shared store once per world boundary before
  Neurons observe the boundary. Neurons never advance Global state.
- A late or skipped Neuron aligns with the current Global boundary. It does not
  replay expired one-cycle facts from boundaries it missed.
- Initial Global facts must be staged before the world's first commit. Later
  Global emissions appear at the next world commit.

## Snapshot boundary

- Adds, removes, and replacements are staged.
- External and Solo Local changes become visible when the owning Agent commits
  the Neuron's next guarded Local boundary at `KLEPAgent.Tick`.
- Actual Local changes emitted by Tandem Executables become visible at the next
  deterministic wave barrier inside the current Agent Tick. Peers in one wave
  always read the same pre-barrier snapshot.
- Global changes become visible only at the next world-owned Global boundary;
  neither the Neuron store nor its Agent publishes them during a Local Tandem
  wave.
- A snapshot copies a deterministic ordered list of immutable facts. Later store
  changes cannot alter an existing snapshot.
- One Agent Tick may therefore contain wave 0, wave 1, and later immutable
  snapshots. The snapshot changes only by publishing a new revision at a
  barrier.
- Facts are ordered by Key ID, then occurrence ID, using ordinal comparison.
- One Key ID cannot be visible as both Local and Global in one snapshot.

## Cross-neuron interaction V1

- `CopyKey`, `GiveKey`, `TakeKey`, and `TradeKeys` accept only an exact,
  activated, Persistent Local fact. They reject Global and OneCycle facts
  without staging changes.
- Every delivery creates a new recipient occurrence with new opaque store
  authority and new issued and activated ticks in the recipient's Local
  timeline. It preserves the definition, payload, Persistent lifetime, and
  original `SourceId`.
- Delivered occurrences coexist with same-Key-ID occurrences already in the
  recipient store. No interaction implicitly removes or replaces one.
- `CopyKey` stages a recipient delivery and retains the exact source fact.
- `GiveKey` is an owner-authorized move: it stages removal of the exact source
  fact and delivery of the new recipient occurrence.
- `TakeKey` has the same move effect as Give, but it succeeds only with the
  exact donor-issued grant for that recipient and source fact.
- `TradeKeys` performs two owner-authorized Gives. Both removals and both
  deliveries are staged, or none are staged.
- V1's public mutation API is a trusted host/coordinator boundary, not a
  security sandbox between arbitrary callers. Calling `CopyKey`, `GiveKey`,
  `CreateTakeGrant`, or `TradeKeys` is the host's explicit authorization on
  behalf of the named owner or owners. A `TakeKey` caller cannot construct a
  grant directly, but untrusted code must not be handed unrestricted mutation
  API access or owner Neuron references.
- `RequestKey` is an immutable addressed intent only. It does not grant fact
  authority or copy, remove, replace, move, or deliver any fact. A response is
  a separate explicit operation.
- Interaction transport treats the payload as opaque. It preserves but does
  not interpret or rewrite payload fields, and requires no domain-specific
  field such as ammo data or `observedWorldTick`.
- Interaction staging never changes a current snapshot. Source removal and
  recipient delivery become visible at each affected store's next top-level
  Agent-committed Local boundary, never at a within-Agent-Tick Tandem wave.
  Trade is staging-atomic; it does not promise globally simultaneous Local
  visibility.
- Lock evaluation remains pure. A Lock may test snapshot state, but it cannot
  stage Copy, Give, Take, Trade, Request, or any other mutation.

## Lifetime

- `OneCycle` remains visible for the rest of the top-level Agent cycle where it
  activates, including later tandem-wave snapshots, and expires before the next
  top-level committed boundary.
- `Persistent` remains until its exact fact is removed or replaced.
- Replacement stages removal of one exact fact and a new immutable occurrence.
- Evaluation never consumes, removes, replaces, or mutates a fact.

## Payload

- Supported fields are Boolean, Int64, finite Double, and Text.
- Construction copies input fields, rejects duplicate/blank names, and exposes
  fields in ordinal order.
- Payloads, definitions, facts, and snapshots do not retain mutable Unity asset
  field-spec data.
- Unity objects, Transforms, vectors, lists, DateTime, and Neuron references do
  not enter Core in this slice.

## Health example

1. Spawn a Persistent Local Health fact with `hp = 10`.
2. Replacing it with `hp = 0` becomes visible at the next Agent Tick boundary.
3. A Tandem Consume Health At Zero behavior observes that snapshot and emits an
   exact removal.
4. Every peer in that wave still sees Health.
5. The wave barrier publishes a new immutable snapshot without Health.
6. Final Solo selection in that same Agent Tick sees
   `Not(KeyPresent(Health))` as true,
   so the death-animation behavior can be selected. If consumption is performed
   by the Solo action instead, its removal becomes visible on the following
   Agent Tick.
