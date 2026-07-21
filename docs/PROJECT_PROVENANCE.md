# Project provenance and review status

KLEP did not begin with this repository. The first public commit is a migration
snapshot: it selects the portable V2.0 source, contracts, and regression suites
from a longer experimental project. It should not be read as an incremental
record of how every line evolved.

The surviving origin transcript is distributed separately from the executable
code on the [KLEP itch.io page](https://roll4d4.itch.io/klep). It is historical
evidence, not a specification. Current behavior is governed by accepted
decisions, contracts, and executable checks in this repository.

## Development method

The V2.0 rewrite is human-directed and AI-assisted. Generative tools have been
used for implementation, review, testing, and documentation under the project
owner's direction. That process does not make a claim true. A public claim must
still point to inspectable code, an accepted contract, a reproducible check, or
clearly identify itself as an open hypothesis.

## Present review boundary

As of `2.0.0-preview.3`:

- the repository is maintained by one project owner;
- its contract suites and CI are project-authored internal evidence;
- there has been no independent security or scientific audit;
- no production adoption, comparative benchmark, or peer-reviewed result is
  claimed; and
- the public API is preview software and may change between preview releases.

This boundary is intentional disclosure, not a substitute for review. See
[`CLAIMS_AND_EVIDENCE.md`](CLAIMS_AND_EVIDENCE.md) for the exact evidence KLEP
does and does not currently provide.

## Reproducing or challenging a result

A useful outside report includes:

1. the KLEP commit or release tag;
2. the host, runtime, and operating system;
3. a minimal behavior arrangement or replay input;
4. the exact command or Tick sequence; and
5. the expected and observed trace.

Contradictory evidence is valuable. Open a GitHub issue with that material; do
not assume a contract is correct merely because the current implementation and
its own tests agree.
