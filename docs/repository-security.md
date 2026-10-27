# Repository security posture

This document records the repository-level security controls and CI safeguards currently configured for Dynamic Dark Mode x Windows. Settings can change independently of the source code, so review this document when repository settings or workflows change.

## Repository settings

Verified on 2026-10-27:

- Dependabot alerts: enabled.
- Dependabot security updates: enabled.
- Dependency graph: enabled.
- Secret protection and push protection: enabled.
- Private vulnerability reporting: enabled.
- GitHub Actions full-length commit SHA pinning enforcement: enabled.
- GitHub Actions policy: all actions and reusable workflows are allowed, subject to the full-SHA pinning requirement.
- GitHub Actions workflow log/check retention: 90 days.
- GitHub Actions cache retention: 7 days; cache size eviction limit: 10 GB.
- CodeQL/code scanning: not configured as a required check.
- Automatic dependency submission: disabled.
- Malware alerts: disabled.
- Routine Dependabot version updates: disabled; security updates remain enabled.

## Branch protection

The active `Protect master` ruleset targets `refs/heads/master` and has no bypass actors. It requires:

- Pull requests before merging.
- Zero required approving reviews, appropriate for a single-maintainer project.
- Resolution of review conversations.
- The `Build macOS app` status check to pass.
- Force pushes are blocked and branch deletion is restricted.
- Copilot code review is requested automatically for new pushes; draft pull requests are not automatically reviewed.

The ruleset does not require branches to be up to date before merging. It does not require signed commits, code-scanning results, code-quality results, coverage thresholds, or deployment checks.

## Workflow permissions and action pins

Workflows should declare the smallest practical `GITHUB_TOKEN` permissions. Current workflows:

- `.github/workflows/macos-ci.yml` declares `contents: read`.
- `.github/workflows/dependency-review.yml` declares `contents: read` and `pull-requests: read`.

Third-party actions are pinned to full commit SHAs. The readable version comments are informational; the SHA is the actual reference. Repository enforcement rejects action references that are not full-length commit SHAs.

The dependency-review workflow checks pull-request dependency changes and fails for newly introduced high- or critical-severity vulnerabilities.

## Dependency management

- `Package.resolved` and `Podfile.lock` are committed.
- Dependabot security updates are enabled.
- Routine dependency version-update PRs are currently disabled; review dependency updates deliberately when needed.

## Vulnerability reporting

Private vulnerability reporting is enabled for this public repository. Please use GitHub's private vulnerability reporting channel for security issues rather than publishing exploitable details in a public issue. See [SECURITY.md](../SECURITY.md) for the reporting guidance.

## Automated review tools

Copilot code review is configured through the `Protect master` ruleset. CodeRabbit and Greptile may provide additional review findings where configured. Automated review comments are leads to investigate, not proof that a vulnerability exists or that code is secure; verify findings against the current code and validate any fix.

The repository-level permission scopes and exact configuration of third-party GitHub Apps should be reviewed in GitHub's installed-app settings by a repository administrator; this document does not claim to verify their external permission grants.
