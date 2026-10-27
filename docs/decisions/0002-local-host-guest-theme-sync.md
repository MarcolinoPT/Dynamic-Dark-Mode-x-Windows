# ADR 0002: Local host-to-guest theme-sync transport

- **Status:** Accepted
- **Date:** 2026-10-27
- **Related issue:** [#9 — Assess VM networking, connection direction and local security](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/9)
- **Related protocol:** [Theme Sync Protocol v1](../theme-sync-protocol-v1.md)

## Context

The first release synchronises the effective macOS appearance to one Windows 11 ARM virtual machine running in Parallels Desktop on the same Mac. The project is intended to remain local and cloud-free, not to provide a general-purpose network service.

The VM uses Parallels Shared Network. During investigation, host-to-guest TCP connectivity was successfully tested after restarting the guest and Parallels Desktop. An earlier failure recovered, but its root cause remains unknown. ICMP ping does not work; this does not block a TCP-based design.

The protocol is defined separately in Theme Sync Protocol v1. It requires ordered messages on a single connection, an initial current-state snapshot, and a fresh snapshot after reconnect.

## Decisions

1. **Transport:** Use a local WebSocket connection over TCP for the prototype and initial implementation.
2. **Connection direction:** The macOS host initiates the connection to the Windows guest. The host remains the source of truth for appearance.
3. **Scope and exposure:** Keep the service local to the host/guest setup. Do not expose it to the public internet, arbitrary LAN clients, or cloud relays.
4. **Binding and firewall:** Restrict the Windows listener to the intended virtual-network interface/address where practical, and use a narrowly scoped Windows Firewall rule. Do not rely on a fixed guest IP remaining unchanged.
5. **Authentication:** Require an explicit authentication mechanism. Shared Network or loopback/virtual-network placement alone is not authentication. The exact mechanism and how credentials are provisioned are implementation decisions to settle before the receiver is exposed for use.
6. **Recovery:** Treat VM unavailability, suspension, disconnection, and network/address changes as recoverable transport conditions. Reconnect with bounded retry/backoff and send a fresh current-state snapshot after reconnect; do not replay stale queued appearance updates.
7. **Cloud and clients:** Keep the first release cloud-free and limited to one Mac host and one Windows VM.

## Threat-model assumptions

- The host and the intended Windows guest are the two trusted application endpoints.
- Other local processes and other devices on the network are not automatically trusted to send appearance commands.
- A message must pass transport authentication and protocol validation before it can change the Windows appearance.
- Network locality is a reduction in exposure, not proof of peer identity.
- The design does not attempt to defend against a fully compromised host or guest operating system.

## Deferred implementation details

This ADR selects the architecture and security requirements, not every implementation parameter. The following can be decided during implementation without reopening the architectural decision unless new evidence requires it:

- Concrete port and guest-address discovery strategy.
- Authentication mechanism and credential storage/provisioning details.
- Exact firewall rule commands and deployment/configuration UX.
- Retry intervals, backoff limits, and diagnostics.
- Handling of specific Parallels address changes and host sleep/wake edge cases.

These details must preserve the decisions above. In particular, do not ship an unauthenticated listener or broaden network exposure as a shortcut.

## Consequences

Host-initiated WebSocket over TCP matches the demonstrated working connection direction and the protocol's ordered-delivery requirement. The implementation remains small and local. The prototype must verify authenticated connection establishment, safe protocol validation, reconnect/snapshot behaviour, and the practical binding/firewall configuration before production integration.
