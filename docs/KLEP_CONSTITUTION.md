# KLEP constitution

This constitution defines the durable boundaries of KLEP. It does not replace
the exact rules in `DECISIONS.md` or the subsystem contracts. When an algorithm
or edge case matters, the accepted decision and its approved contract tests are
the authority.

## Purpose

KLEP is a small, deterministic, inspectable symbolic behavior toolkit for game
developers and scientists. Its job is to make perceived facts, conditions,
action selection, execution, and causal explanation explicit enough to author,
test, reproduce, and study.

KLEP is not required to simulate a human mind. Higher-cognition systems may be
built with it, but they remain bounded symbolic mechanisms whose evidence and
influence must be inspectable.

## Semantic sovereignty

The protected KLEP vocabulary is:

- Neurons own one deterministic decision cycle;
- Keys are symbolic perceived or internally produced facts;
- Locks are pure conditions over an immutable Key snapshot;
- Executables are eligible, scored, selected, and advanced through an explicit
  lifecycle;
- Goals are Executables with explicitly contracted child behavior; and
- Observer or higher-cognition systems may rank only behavior that ordinary
  Locks already made eligible.

These concepts may not be renamed, merged, split, or assigned new meaning by an
implementation convenience. A semantic change requires an accepted decision,
an updated contract, and regression evidence.

## Truth, evidence, and proposal

KLEP keeps three categories separate:

1. observation or actual execution can establish runtime evidence;
2. deterministic derivation may transform declared evidence under an explicit
   rule; and
3. learning, recall, planning, or imagination may propose direction.

A proposal is not world truth. Memory cannot manufacture a current Key, and
Observer advice cannot open a Lock. Admission or execution of model-invented
definitions and capabilities remains outside the current approved contracts.

## Determinism and time

Given identical definitions, initial state, ordered inputs, explicit clocks,
and random seed, Core behavior must be reproducible. KLEP therefore requires:

- immutable evaluation snapshots;
- stable ordinal ordering where authored order does not govern;
- one declared Core Tick path per Neuron;
- injectable or caller-owned time and randomness; and
- an explicit owner for cross-Neuron or Global boundaries.

Unity frame order, wall time, mutable static state, and ScriptableObject asset
mutation are not hidden decision inputs.

## Eligibility before influence

Lock eligibility is absolute. Scoring, learning, Emotion, Memory, Ethics,
Observer, Planner, or any future learned model may compare or rank only the
currently eligible set. No influence may make an invalid behavior eligible or
silently rewrite the facts on which eligibility was evaluated.

## Causality and inspectability

Every material decision must retain enough immutable evidence to explain:

- which snapshot was observed;
- which candidates were eligible or blocked and why;
- each authored or external score component;
- which Executable advanced and through which lifecycle stages;
- which Key operations were actually emitted and when they became visible;
- which higher-cognition evidence was consulted; and
- which fault, cancellation, success, or failure actually occurred.

Diagnostics are outward-only views. Looking at history must not change the
history being inspected.

## Portability and ownership

Core and pure cognition assemblies must remain independent of Unity lifecycle
and editor APIs. Unity components are adapters and hosts. Authored assets are
definitions; mutable runtime state belongs to per-Agent or explicitly shared
runtime owners.

Subsystem ownership is explicit. Memory, Emotion, Agent learning, Key stores,
Executable lifecycle, and future Planner state are not ambient globals and do
not become one another's hidden mutation surface.

## Trust boundary

The current runtime assumes trusted authored host and behavior code. Purity is
enforced through narrow APIs and validated traces where practical, but it is not
a security sandbox. Untrusted generated code, plugins, or imagination-model
output require a separate capability and validation boundary before execution.

## Change discipline

Protected behavior changes follow this order:

1. identify the governing accepted decision and contract;
2. record an unresolved conflict or add an explicit proposed decision;
3. obtain owner acceptance before changing the protected meaning;
4. update the contract and its deterministic regression tests; and
5. report compile, headless contract, Unity import, and Play Mode evidence as
   separate validation levels.

Compilation alone never proves behavioral compatibility. A Candidate rule may
be implemented for inspection, but it is not permanent KLEP behavior until its
decision is Accepted.
