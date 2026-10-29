# Agent and contributor instructions

These instructions apply to the whole repository unless a more specific `AGENTS.md` overrides them.

## Project context and scope

- Read `README.md`, `CONTRIBUTING.md`, the relevant architecture/ADR documents under `docs/`, and the linked issue before making a substantial change.
- Treat the issue's acceptance criteria and existing approved design decisions as the source of truth. Do not silently expand scope or mix unrelated refactoring into a change.
- Distinguish implemented behaviour from proposals, prototypes, and planned work in documentation and PR descriptions.
- If requirements conflict, security assumptions are unclear, or a change would alter an approved architecture decision, stop and ask for clarification rather than guessing.

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