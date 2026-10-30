# Contributing

Thanks for your interest in Dynamic Dark Mode × Windows.

## Before starting

- Read the [README](README.md), [roadmap](docs/roadmap.md), and relevant architecture/ADR documents.
- Work should start from a clearly scoped issue. If no suitable issue exists, create one before implementation.
- Confirm that the issue describes one independently deliverable outcome and has testable acceptance criteria. Clarify or split uncertain/oversized work before coding.
- Keep changes focused and avoid unrelated refactoring.

## Vertical-slice workflow

The project follows a **one issue → one PR** workflow. Each issue and its PR should deliver one meaningful, independently reviewable vertical slice of functionality.

A vertical slice includes all work needed to deliver and verify its behaviour:
- Production implementation, where applicable.
- Relevant automated tests.
- Manual validation scenarios and recorded results where the environment requires them.
- Necessary documentation and contract updates.

Do not defer tests or manual acceptance criteria to a separate ticket merely because they are a different type of work. Keep them with the behaviour they validate.

### Issue expectations

Each implementation issue should state:

1. **Goal:** one observable outcome or capability.
2. **Scope and exclusions:** what is included and what is deliberately not included.
3. **Dependencies:** prerequisites and related decisions.
4. **Acceptance criteria:** observable behaviour, written in Gherkin.
5. **Automated tests:** expected unit, component, integration, or other automated coverage.
6. **Manual validation:** Gherkin scenarios that require a real OS, device, network, or user interaction.
7. **Security and compatibility:** relevant constraints, failure behaviour, and supported environments.

Use `Feature` and `Scenario` with `Given`, `When`, and `Then` to describe behaviour. Identify which scenarios are automated and which are manual; Gherkin is the shared behavioural specification, not a requirement to adopt a particular BDD framework.

Manual validation is first-class acceptance evidence. Record:
- OS, application, runtime, and relevant environment versions.
- Preconditions and steps needed to reproduce the scenario.
- Actual result and pass/fail outcome.
- Relevant evidence or logs, with secrets and sensitive information redacted.

### Sizing and uncertainty

- Prefer a small issue that can be implemented, tested, and reviewed as one PR.
- If the implementation, test plan, dependencies, or review surface is too broad or uncertain, split the work before implementation.
- Do not split artificially by class, method, file, or test type. Each resulting issue must still deliver a meaningful behaviour or capability.
- When an important design or feasibility question is unresolved, create a focused investigation/spike. State the question to answer and the expected output (for example, a decision, measured result, or recommendation). Use the findings to define implementation slices.
- A merge alone does not prove an issue is done. Complete the applicable automated checks and required manual scenarios, and record their results.

Avoid permanent umbrella implementation issues that accumulate unrelated work. Keep architectural rationale in ADRs, protocol contracts in their documentation, and implementation work in individual vertical-slice issues.

### Iterative planning and ticket readiness

- Always select and review a concrete issue before starting work. The issue is the agreed scope and acceptance contract for its PR.
- When an issue is completed, review the remaining backlog before selecting the next slice. Reassess priorities, dependencies, implementation state, and approved design decisions; revise stale or oversized issues before they are picked up.
- Before finishing the current slice, identify at least one concrete next issue and move it to **Ready** in the project workflow. A Ready issue must have a clear goal, bounded scope, known dependencies, testable acceptance criteria, automated test expectations, and relevant manual Gherkin scenarios. It should be actionable without guessing.
- If no issue is ready, refine or create the smallest suitable issue, or resolve the necessary blocker first. Do not label speculative or dependency-blocked work Ready just to keep the queue populated.
- Plan iteratively: prepare at least the next slice, not the entire roadmap.

## Development workflow

1. Confirm the issue is ready and understand its acceptance scenarios and approved design constraints.
2. Create a focused branch from the default branch (for example, `feat/protocol-prototype`, `fix/reconnect-after-resume`, or `docs/contributor-guidelines`).
3. Implement the complete vertical slice, including its relevant automated tests, manual scenarios, and documentation.
4. Run the relevant build and automated tests locally. Record manual validation results when the required environment is available.
5. Open one PR for the issue using the [PR template](.github/pull_request_template.md). Link the issue and explain the scope, tests, manual validation, and security/compatibility implications.
6. Triage review feedback, including AI-generated comments; verify suggestions rather than applying them blindly.
7. Merge only when acceptance criteria are met and required pre-merge checks pass.
8. Complete any explicitly post-merge validation (such as the supported Mac ↔ Windows Parallels E2E run) and record its result against the issue. A failed post-merge validation remains visible and actionable; do not describe the slice as fully validated until resolved.

## Commit messages

Use Conventional Commit-style messages, with a short imperative description and an issue reference when one exists:

- `feat(protocol): add strict message validation (#10)`
- `fix(parser): reject malformed UTF-8 (#10)`
- `test(parser): cover maximum payload size (#10)`
- `docs(contributing): document commit conventions (#10)`

Use a meaningful type such as `feat`, `fix`, `test`, `docs`, `refactor`, `build`, or `ci`. Add a scope when useful. Do not invent issue numbers. Use `Fixes #N` in a PR description only when the PR fully resolves that issue; use `Refs #N` or `(#N)` for related work that does not close it.

## Pull-request expectations

- One PR should implement one issue. Avoid combining unrelated issues or silently expanding the linked issue's scope.
- Describe the problem, intended solution, and scope.
- Link the issue and explain whether the PR closes it.
- Explain how it was tested; include manual test results where the required environment is available, or state clearly what remains outstanding.
- Call out networking, permissions, security, privacy, and compatibility implications.
- Update documentation when behaviour, architecture, or setup changes.
- Report CI accurately and do not merge while required checks are failing or pending.

AI-generated review comments are advisory. The author remains responsible for validating suggestions, and the maintainer makes the final decision.

## Security-sensitive changes

Preserve approved architecture decisions and security boundaries. Do not casually weaken local-only communication, TLS certificate pinning, explicit/manual pairing, secret protection, or fail-closed behaviour. Any change to these decisions should be scoped, justified, and reviewed explicitly.

## Licensing

This project derives from GPL-licensed Dynamic Dark Mode. Preserve required copyright and licence notices and review GPL obligations when redistributing modified source or binaries.
