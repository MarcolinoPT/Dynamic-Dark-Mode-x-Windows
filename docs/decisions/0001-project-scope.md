# ADR 0001: Focused one-way synchronisation

- **Status:** Accepted
- **Date:** 2026-10-26

## Context
The primary use case is synchronising the macOS host appearance to one Windows 11 virtual machine. This is a personal side project, not a general-purpose network theme service.

## Decision
- macOS is the source of truth.
- Synchronisation is one-way: macOS to the Windows VM.
- Preserve the existing Dynamic Dark Mode functionality.
- Keep the initial implementation local and cloud-free.
- Start with a per-user Windows companion unless discovery shows a concrete reason otherwise.
- Document protocol semantics openly so independent clients are possible later.
- Do not make multiple clients, arbitrary LAN machines or remote hosting part of the first release.

## Consequences
The implementation can remain small and testable. Protocol design should not prevent future independent clients, but no multi-client infrastructure is required now.
