# Agent and contributor instructions

These instructions apply to the whole repository unless a more specific `AGENTS.md` overrides them.

## Project context and scope

- Read `README.md`, `CONTRIBUTING.md`, the relevant architecture/ADR documents under `docs/`, and the linked issue before making a substantial change.
- Treat the issue's acceptance criteria and existing approved design decisions as the source of truth. Do not silently expand scope or mix unrelated refactoring into a change.
- Distinguish implemented behaviour from proposals, prototypes, and planned work in documentation and PR descriptions.
- If requirements conflict, security assumptions are unclear, or a change would alter an approved architecture decision, stop and ask for clarification rather than guessing.

## Work planning: vertical slices

- Work is organized as **vertical slices**: one issue describes one independently deliverable, observable capability, and one PR implements that issue.
- A slice includes the production change (where applicable), relevant automated tests, manual validation scenarios, and necessary documentation updates. Do not defer testing or manual acceptance criteria to an unrelated follow-up ticket.
- Keep issues small enough that the intended implementation, tests, and review can be understood together. If scope, dependencies, or uncertainty make that difficult, split the work before implementation.
- Do not split work artificially by class, method, or test file. A ticket should deliver a meaningful behaviour or capability, not merely a technical fragment with no independently verifiable outcome.
- State the goal, scope and exclusions, dependencies, acceptance criteria, automated test expectations, manual validation scenarios, and relevant security constraints in each issue.
- Express observable acceptance behaviour in Gherkin (`Feature`, `Scenario`, `Given`, `When`, `Then`). Include both automated and manual scenarios in the same issue when both are relevant. Clearly identify which scenarios are automated and which require manual execution.
- Manual scenarios are first-class acceptance criteria. Record the tested environment, actual result, pass/fail outcome, and relevant evidence or logs (with secrets and sensitive data redacted).
- When uncertainty prevents a well-defined implementation slice, create a narrowly scoped investigation/spike with a concrete question and expected decision/output. Use its findings to define implementation issues; do not hide exploratory work inside a broad implementation ticket.
- An issue is not complete merely because its code is merged. Complete its applicable automated checks and required manual validation, and record the results.
- Avoid permanent umbrella implementation issues that accumulate unrelated deliverables. Keep architectural rationale in ADRs, protocol contracts in their documentation, and implementation work in individual vertical-slice issues.

### Iterative ticket selection and readiness

- Always select and review an existing issue before starting implementation. Do not begin work from an informal request or an assumed next step without identifying the issue that defines its scope and acceptance criteria.
- At the completion of each issue, review the remaining backlog before choosing the next item. Re-check priorities, dependencies, current implementation state, and approved architecture decisions; update or split stale or oversized tickets rather than carrying forward outdated assumptions.
- Before considering the current slice complete, identify at least one concrete next issue and bring it to **Ready** state in the project workflow. Ready means its scope, dependencies, acceptance criteria, automated tests, and relevant manual Gherkin scenarios are sufficiently clear to begin without inventing requirements.
- If no existing issue is genuinely ready, create or refine the smallest appropriate issue (or a focused investigation issue) and resolve its blockers before marking it Ready. Do not mark speculative or dependency-blocked work Ready merely to satisfy the process.
- Keep only work that is genuinely prepared in Ready state. This is a rolling, iterative planning process, not a commitment to pre-plan the entire project.

## Git and commit conventions

- Create a focused branch from the default branch for each independent change. Follow existing branch naming patterns such as `feat/short-description`, `fix/short-description`, and `docs/short-description`.
- Use Conventional Commit-style messages with a concise imperative description and an issue reference when one exists:
  - `feat(protocol): add strict message validation (#10)`
  - `fix(parser): reject malformed UTF-8 (#10)`
  - `test(parser): cover maximum payload size (#10)`
  - `docs(contributing): document commit conventions (#10)`
- Use a meaningful type such as `feat`, `fix`, `test`, `docs`, `refactor`, `build`, or `ci`. Add a scope when it improves clarity.
- Do not invent issue numbers. If no relevant issue exists, omit the issue reference and recommend creating/linking an issue when the work warrants one.
- Use `Fixes #N` in a PR description only when the PR fully resolves that issue. Use `Refs #N` or `(#N)` for related work that does not close it.
- Never amend, force-push, or rewrite shared history unless explicitly requested.
- Never push commits, create a PR, or merge a PR unless the user explicitly authorizes that action. Never merge a PR merely because CI is green.

## Implementation and testing

- Keep changes minimal, readable, and consistent with the surrounding code and target frameworks.
- Add or update automated tests for changed behaviour and regression fixes. Include malformed-input and boundary cases when relevant.
- Structure tests clearly with Arrange, Act, and Assert sections.
- Keep assertions in the test itself, where the test's behaviour and expectations are visible. Test helpers should arrange fixtures or capture and return values; they must not perform assertions or hide test expectations.
- Capture all values needed for verification during the Act phase, then assert them together in the Assert phase.
- Use at most one `ShouldSatisfyAllConditions` call per test. Put the test's independent assertions in that single holistic assertion block so one failure does not prevent the remaining checks from running. If a coherent holistic assertion block is awkward or the checks represent distinct behaviours, split the test rather than adding multiple holistic assertion blocks.
- For expected failures, capture the exception and other resulting state during Act, then assert the exception type and state together in the test's holistic assertion block.
- Run the narrowest relevant tests first, then applicable build/test commands. Report the commands run and their actual results; never claim tests passed if they were not run or CI has not completed.
- Review the final diff for accidental changes, generated files, secrets, formatting problems, and unrelated edits.
- Treat CI failures as blockers until understood. Fix the cause rather than weakening or skipping a check merely to get a green build.

## C# coding conventions

- Prefer named arguments at method and constructor call sites so the intent of each argument is explicit, including for framework APIs where practical. Follow the target C# language version and avoid adding names when they reduce clarity or are unsupported.
- Prefer semantic, self-documenting expressions over opaque operator usage when a clear alternative exists. This is not a blanket ban on language operators; use the clearest reasonable expression and follow established project conventions.
- Add automated tests for new behaviour and regression fixes. For security-sensitive code, cover successful operation, failure paths, and relevant boundary cases.
- Keep UI initialization responsive. Move potentially blocking I/O, certificate-store operations, and expensive cryptographic work off the UI thread, and marshal UI updates back to it safely.

## Security and privacy

- Preserve the project's approved security boundaries and architecture decisions. In particular, do not casually weaken local-only communication, TLS certificate pinning, explicit/manual pairing, secret protection, or fail-closed behaviour.
- Do not introduce automatic LAN discovery, trust-on-first-use, insecure fallback modes, or broader network exposure unless an approved issue/ADR explicitly changes the design.
- Never log or expose pairing/authentication secrets, tokens, private keys, or other credentials. Redact sensitive values in diagnostic output.
- Validate untrusted input at boundaries; reject malformed or unsupported input safely and predictably.
- Do not claim security properties that are not implemented and tested. Update relevant documentation and tests when security-sensitive behaviour changes.

## Pull requests and AI review

- Use the repository PR template. Explain the problem, approach, issue links, scope, tests, and security/compatibility implications.
- Triage all substantive review feedback, including AI-generated comments. Verify suggestions against code, tests, requirements, and project design; do not apply them blindly.
- Address valid findings or explain clearly why they are not applicable. Include nitpicks when they affect correctness, maintainability, or the requested review scope.
- Keep the PR description accurate as the branch evolves, including final CI results before requesting merge.
- The human maintainer owns final decisions and merge approval.
