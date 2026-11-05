# Contributing to Melanopsin

Melanopsin is a modular application infrastructure project. Public contracts
should have explicit semantics, predictable resource ownership, and consistent
behavior across supported platforms. Contributions should preserve those
properties. Prefer focused changes over speculative abstractions.

## Before you start

All participation is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

- If the scope, compatibility impact, or design is unclear, open an issue or
  draft pull request before implementing it.
- Use of AI assistance and autonomous systems is subject to [Artificial
  Intelligence and Autonomous Systems](#artificial-intelligence-and-autonomous-systems).
- By submitting a change, you confirm that you have the right to contribute it
  under the repository's license. Contributions must be compatible with that
  license.

## Bug fixes

A bug fix should include regression coverage when suitable test support exists.
Where an automated test is impractical, describe the manual verification and its
result. The final pull request should make both the failure and the fix clear.

## Feature proposals

Use an issue or draft pull request to establish scope for new public APIs,
cross-module behavior, platform capabilities, and dependencies. Small,
self-contained features can go directly to a normal pull request.

Describe the problem before proposing an API. Include the intended API,
observable behavior, unsupported cases and fallback behavior, compatibility,
and the tests that will demonstrate the result. APIs explicitly marked as
experimental may evolve while their design is being validated, but the build and
tests must remain usable and each change should state what is still unstable.
An explicitly WIP module may iterate quickly; its public APIs remain
experimental until the module documentation marks them as stable.

## Breaking changes

Breaking a stable public API requires an issue. Include the previous behavior,
the new behavior, why the change is necessary, and a concrete migration path.
Behavioral compatibility matters even when source and binary compatibility are
preserved. Experimental APIs may change while their design matures; promotion to
stable behavior brings the full compatibility contract.

## General expectations

- Keep the change focused. Submit formatting and unrelated cleanup separately so
  the behavioral change remains easy to review, bisect, and revert.
- Add regression coverage when possible. If the change requires manual
  verification, state the environment, configuration, and exact steps used.
- Treat public APIs as contracts. Name behavior directly and document ownership,
  lifetime, exceptions, and threading constraints where they are not obvious.
- Keep platform-specific behavior and native handles within platform projects;
  shared APIs expose portable abstractions.
- Provide an explicit fallback when a platform lacks an optional capability.
- Document externally observable behavior together with any platform,
  accessibility, localization, or input assumptions.
- For hot-path changes, include before/after measurements covering allocations,
  latency, throughput, and ownership where relevant.
- Use comments to explain constraints that are not apparent from the code.
  Validate data at public, input, native interop, and data boundaries, and keep
  internal invariants enforced by the layer that owns them.
- New dependencies require a clear need and review of maintenance, license,
  vulnerability history, package size, AOT compatibility, and replacement cost.
- Commit source inputs and intentionally tracked generated files. Keep `bin/`,
  `obj/`, build products, and local configuration out of version control.

## Pull requests

A pull request should contain one coherent change. Explain the problem, the
chosen behavior, and how it was verified. Link resolved issues with
`Fixes #<issue-number>` when the change resolves them. External pull requests are
squash merged, and the pull request title must follow the commit message format
below.

Use the description sections that apply and add others when the change requires
them. Keep the description concise; reviewers can read the diff.

```markdown
## Summary
What changed and why?

## Behavior and API
What behavior or public API changed? Include a short usage example when useful.

## Verification
What checks were run? Which platforms or scenarios remain unverified?

## Compatibility
What is the fallback for unsupported platforms? For breaking changes, include
the previous behavior, new behavior, and migration path.

## AI assistance
If material: identify the tool and affected parts. The submitter must understand
and verify the result.
```

Before requesting review, confirm that:

- [ ] The change has one clear purpose and does not include unrelated edits.
- [ ] Public API names and ownership semantics are explicit.
- [ ] Platform-specific code stays behind an appropriate capability boundary.
- [ ] Error handling, disposal, and threading behavior are defined.
- [ ] Relevant checks pass locally, or the unverified platform is stated.
- [ ] Performance claims include measurements.
- [ ] New dependencies and licenses are disclosed and reviewed.
- [ ] Material AI assistance is disclosed, and every generated part has been
      verified.
- [ ] Public behavior has suitable tests, a sample, or a documentation update.
- [ ] Generated files and local build artifacts are not included.

## Artificial Intelligence and Autonomous Systems

This section applies to all contributors and project spaces. Compliance with
this section is a condition of participation.

### Definitions

For purposes of this section:

- **Author of Record** means the natural person identified as the author of a
  contribution and responsible for it under this policy.
- **AI System** means a machine-based system, including a generative model or
  agent, used to produce, transform, recommend, or act on contribution
  content or actions in Project Spaces. Deterministic automation, including
  continuous integration, linters, and dependency scanners, is governed by
  repository configuration and is outside this definition.
- **Autonomous Operation** means an operation in Project Spaces in which an AI
  System initiates, publishes, submits, updates, or approves an action and no
  natural person reviews and approves that specific action before publication.
- **Project Spaces** means issues, pull requests, review threads, discussions,
  and other project-controlled communication channels.
- **Material AI Assistance** means AI use that generates, rewrites, or
  substantially informs code, tests, documentation, analysis, or design included
  in a contribution. Routine spelling, grammar, and formatting assistance falls
  outside Material AI Assistance.

### Human accountability

Every contribution must have an Author of Record. The Author of Record remains
responsible for the contribution's design, correctness, licensing, security,
performance, and maintenance. AI assistance does not transfer or diminish that
responsibility.

### Prohibited autonomous conduct

Autonomous Operations are prohibited. Accordingly, an AI System must not open,
submit, update, or close a pull request; post or update a review; create, close,
or triage an issue; approve a change; make a merge decision; or otherwise act as
a participant in Project Spaces. No person may configure or direct an AI System
to perform a prohibited action.

AI Systems must not be identified as authors, co-authors, reviewers, assignees,
or participants in a contribution.

### Disclosure and verification

Material AI Assistance must be disclosed in the pull request description before
review. The disclosure must identify the AI System and the parts of the
contribution substantially generated, rewritten, or informed by it.

The Author of Record must understand the contribution and be able to explain it
during review. Generated APIs, tests, benchmarks, citations, and dependencies
must be verified against the implementation. The Author of Record must also have
the right to provide all content submitted to an AI System; secrets, private
source code, personal data, and restricted material remain prohibited.

Failure to comply with this section is grounds for rejection or removal of a
contribution and may result in further moderation action.

## Commit messages

Use English commit titles in Conventional Commits form:

```text
<type>(<scope>): <summary>
```

Use one of these types:

- `feat`: new capability
- `fix`: corrected behavior
- `perf`: performance change without a behavior change
- `refactor`: structural change without a behavior change
- `docs`: documentation
- `test`: tests
- `build`: build, SDK, or dependency changes
- `ci`: continuous integration
- `chore`: other maintenance

Use the primary affected package or subsystem as the scope, without the
`Melanopsin.` prefix. Omit the scope when a change spans the repository and no
single subsystem is primary.

Write the summary in the imperative mood, start it with a lowercase letter, omit
the trailing period, and keep it concise. The body is optional. Use it to explain
the reason, constraints, migration, and validation when the title cannot.

Mark breaking changes with `!` and add a `BREAKING CHANGE:` footer:

```text
feat(api)!: replace resource ownership model

BREAKING CHANGE: Consumers must use the new ownership model and release
resources through the returned handle.
```
