# Dynamic Dark Mode × Windows

A focused side project to synchronise the appearance of a macOS host with its Windows 11 VM.

> **Project goal:** macOS is the source of truth. When macOS switches between Light and Dark appearance, the Windows VM follows automatically.

This project builds on the Apple Silicon-compatible [Dynamic Dark Mode](https://github.com/threadMapperX/Dynamic-Dark-Mode) fork and retains attribution to the original authors. See [LICENSE](LICENSE) for the applicable GPL terms.

## Scope

### First release
- One macOS host synchronising with one Windows VM.
- One-way theme synchronisation: macOS → Windows.
- Preserve Dynamic Dark Mode's existing scheduling and manual appearance features.
- Reconnect after VM suspend/resume or temporary network loss.
- No cloud service or external account required.
- A small Windows companion that runs in the user's session.

### Deliberately out of scope for the first release
- Multiple Windows clients or arbitrary LAN computers.
- Reverse synchronisation from Windows to macOS.
- Cloud relay, remote access or hosted infrastructure.
- Reimplementing sunrise/sunset calculations in Windows.

### Long-term design commitment: open protocol
The synchronisation protocol should be documented and independently implementable so that alternative clients could be created later. The first implementation only needs to support this project's macOS sender and Windows VM receiver. Protocol openness and multi-client support are future design considerations, not first-release requirements.

## Architecture
- **macOS:** Swift / existing Dynamic Dark Mode app.
- **Windows:** C# / .NET companion.
- **Transport candidate:** local WebSocket connection, subject to discovery and security review.
- **Authority:** the current effective macOS appearance.
- **Reliability:** send current state on connection; tolerate duplicate state messages; reconnect with bounded backoff.

The exact host/guest networking arrangement, connection direction, authentication and macOS appearance-observation mechanism must be validated during discovery before implementation.

## Engineering principles
- Keep scope focused and avoid speculative infrastructure.
- Prefer small changes and isolated pull requests.
- Treat CI and tests as part of implementation, not post-release cleanup.
- AI reviews are advisory; deterministic checks and human judgement remain important.
- Never report coverage, security or build status unless a real check measures it.
- No secrets in source control, logs, issues or artifacts.
- Preserve upstream licence and attribution.
- Document manual acceptance tests that cannot run on hosted CI.

## Roadmap
Track work in [GitHub Issues](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues) and the GitHub Projects board (to be configured).

1. **M0 — Foundation:** source/target discovery, reproducible baseline build, repository governance and initial regression notes.
2. **M1 — Engineering baseline:** macOS and Windows CI, test scaffolding, linting and PR conventions.
3. **M2 — Sync prototype:** documented protocol, safe local transport and isolated proof of concept.
4. **M3 — Windows integration:** theme application, startup, reconnect and failure handling.
5. **M4 — End-to-end sync:** integrate sender, preferences and full VM tests.
6. **M5 — First public release:** documentation, packaging, signing decision, release workflow and acceptance evidence.
7. **M6 — Maintenance and polish:** badges, AI review, dependency maintenance and post-release fixes.

Phases are sequential gates rather than dates. A phase is complete only when its acceptance criteria are met.

## Development status
**Current phase: M0 — Foundation.** No synchronisation implementation has been started in this repository yet.

## Project documentation
- [Detailed roadmap](docs/roadmap.md)
- [Ideas and future possibilities](docs/ideas.md)
- [Architecture decision: project scope](docs/decisions/0001-project-scope.md)
- [Contribution guidelines](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Code of conduct](CODE_OF_CONDUCT.md)
