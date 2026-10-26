# Detailed Roadmap

This is the planning source of truth for phases and exit criteria. GitHub Projects tracks tasks; issues track implementation and discussions. Dates are intentionally omitted for this side project.

## M0 — Foundation

- Inspect Xcode schemes, targets, build settings, supported macOS versions and dependencies.
- Identify where effective appearance is determined and how manual overrides interact with scheduling.
- Produce a repeatable baseline build and record regression checks on the M4.
- Capture baseline behaviour: launch, menu-bar controls, manual toggle, scheduled switching, sunrise/sunset accuracy, restart behaviour.
- Establish project identity, attribution and licensing practices.
- Add contribution, security and code-of-conduct files plus issue and PR templates.
- Configure labels, milestones and a GitHub Projects Kanban board.
- Agree branch naming, commit conventions and merge strategy.

**Exit criteria:** unchanged baseline builds/runs locally; behaviour and known limitations are documented; the project workflow is ready.

## M1 — Engineering baseline

- Confirm a headless Xcode build/test command and shared scheme.
- Add GitHub Actions for macOS build and tests on pull requests and default-branch pushes.
- Add XCTest coverage for isolated new logic.
- Add Windows companion solution when its architecture is agreed.
- Add Windows CI for restore, build and unit tests.
- Add formatting/lint checks where reliable and low maintenance.
- Enable dependency review, Dependabot and secret scanning where supported.
- Configure branch rules/required checks if available on the account/plan.

**Exit criteria:** relevant pull requests receive build/test feedback on both platforms. Do not add a CI badge until the workflow exists and has run.

## M2 — Sync prototype

- Write a protocol proposal before production implementation.
- Define protocol version, appearance value, state snapshot, validation and error handling.
- Keep the semantics documented and independently implementable in the future.
- Choose connection direction after testing the VM networking mode.
- Threat-model local access, authentication, port binding and firewall behaviour.
- Implement a tiny isolated sender/receiver proof of concept.
- Add protocol tests for malformed messages, duplicate states and reconnects.

**Exit criteria:** macOS can send a state to Windows in a controlled test; invalid input fails safely; security/connectivity decisions are documented.

## M3 — Windows integration

- Implement a per-user C#/.NET background companion.
- Apply the theme to the current user only and notify Windows of relevant changes.
- Separate protocol handling from Windows-specific theme application.
- Add diagnostics without sensitive information.
- Add startup/shutdown, reconnect/backoff and refresh-state-on-connect behaviour.
- Test theme changes with apps that respond to system settings.

**Exit criteria:** unit tests pass and the VM confirms reliable theme application and recovery.

## M4 — End-to-end sync

- Integrate state observation into the Dynamic Dark Mode lifecycle.
- Send current effective appearance at connection time.
- Add an enable/disable preference and clear status/error behaviour.
- Ensure scheduled switching and manual overrides both propagate.
- Test launch orders, sign-in, VM suspend/resume, host sleep/wake, network changes and restarts.
- Verify disabled sync leaves existing functionality untouched.

**Exit criteria:** end-to-end manual acceptance checklist passes on the actual Mac + Windows VM setup.

## M5 — First public release

- Complete security and failure-mode review.
- Finalise installation, configuration, uninstall and troubleshooting docs.
- Choose artifact formats and versioning/release-note approach.
- Assess macOS signing/notarisation and Windows signing costs/trade-offs.
- Build the release workflow and attach artifacts/checksums where appropriate.
- Add truthful CI, release and licence badges; coverage only if measured reliably.
- Publish known limitations and tested environment details.

**Exit criteria:** another user can install and use the app from the documentation without bespoke instructions.

## M6 — Maintenance and polish

- Enable Greptile AI reviews and tune for useful, low-noise feedback.
- Keep AI review advisory; it never substitutes for CI or human judgement.
- Add dependency automation and scheduled health checks where useful.
- Review community feedback and backlog.
- Consider independent clients only when there is a concrete need.

**Exit criteria:** maintainable, transparent and proportionate ongoing maintenance.

## Cross-cutting definition of done

- Issue has testable acceptance criteria.
- Tests are added, or the reason for no automated test is documented.
- Relevant CI passes.
- Documentation/release notes are updated as needed.
- Network-facing changes consider security and privacy.
- AI review feedback is triaged and verified.
- Manual-only behaviours are recorded in acceptance results.
