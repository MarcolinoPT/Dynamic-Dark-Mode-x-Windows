# Contributing

Thanks for your interest in Dynamic Dark Mode × Windows.

## Before starting
- Read the [README](README.md), [roadmap](docs/roadmap.md), and relevant architecture/ADR documents.
- For substantial changes, open or find an issue first so scope and acceptance criteria are clear.
- Keep changes focused and avoid unrelated refactoring.

## Development workflow
1. Create a focused branch from the default branch (for example, `feat/protocol-prototype`, `fix/reconnect-after-resume`, or `docs/contributor-guidelines`).
2. Make a small, focused change that satisfies the issue's acceptance criteria.
3. Add or update tests for changed behaviour and regression cases.
4. Run the relevant build and test commands locally.
5. Open a pull request using the [PR template](.github/pull_request_template.md).
6. Triage review feedback, including AI-generated comments; verify suggestions rather than applying them blindly.
7. Merge only when acceptance criteria are met and required checks pass.

## Commit messages
Use Conventional Commit-style messages, with a short imperative description and an issue reference when one exists:

- `feat(protocol): add strict message validation (#10)`
- `fix(parser): reject malformed UTF-8 (#10)`
- `test(parser): cover maximum payload size (#10)`
- `docs(contributing): document commit conventions (#10)`

Use a meaningful type such as `feat`, `fix`, `test`, `docs`, `refactor`, `build`, or `ci`. Add a scope when useful. Do not invent issue numbers. Use `Fixes #N` in a PR description only when the PR fully resolves that issue; use `Refs #N` or `(#N)` for related work that does not close it.

## Pull-request expectations
- Describe the problem, intended solution, and scope.
- Link related issues and explain whether the PR closes them.
- Explain how it was tested; include manual test results where automated tests are not possible.
- Call out networking, permissions, security, privacy, and compatibility implications.
- Update documentation when behaviour, architecture, or setup changes.
- Report CI accurately and do not merge while required checks are failing or pending.

AI-generated review comments are advisory. The author remains responsible for validating suggestions, and the maintainer makes the final decision.

## Security-sensitive changes
Preserve approved architecture decisions and security boundaries. Do not casually weaken local-only communication, TLS certificate pinning, explicit/manual pairing, secret protection, or fail-closed behaviour. Any change to these decisions should be scoped, justified, and reviewed explicitly.

## Licensing
This project derives from GPL-licensed Dynamic Dark Mode. Preserve required copyright and licence notices and review GPL obligations when redistributing modified source or binaries.
