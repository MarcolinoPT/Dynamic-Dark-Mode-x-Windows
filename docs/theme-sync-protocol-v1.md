# Theme Sync Protocol v1 (Draft)

- **Status:** Draft for review
- **Related issue:** [#8 — Specify an open, versioned theme-sync protocol](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/8)
- **Scope:** Transport-neutral application message contract for one-way macOS → Windows appearance synchronisation.

This draft defines the smallest useful v1 contract. It does **not** select a transport or define connection authentication, pairing, port binding, or the network threat model; those decisions belong in the networking/security ADR tracked by issue #9.

## Goals and non-goals

V1 carries the current effective macOS appearance to one Windows VM. macOS is authoritative. The Windows receiver applies the received state; it does not send appearance changes back.

V1 does not define multiple clients, discovery, arbitrary LAN access, cloud relays, remote access, scheduling, or sunrise/sunset calculations.

## Message format

Messages are UTF-8 JSON objects. A v1 state message must contain exactly the four fields listed below. **Unknown fields are not allowed**: receivers must reject a message containing any additional field, including optional metadata. This keeps v1 validation unambiguous; adding fields requires an explicitly versioned protocol change.

| Field | Type | Required value / meaning |
| --- | --- | --- |
| `protocol` | string | `dynamic-dark-mode-sync` |
| `version` | integer | `1` |
| `type` | string | `appearance.set` |
| `appearance` | string | `light` or `dark`, representing the current effective macOS appearance |

Example — dark appearance:

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "appearance.set",
  "appearance": "dark"
}
```

Example — light appearance:

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "appearance.set",
  "appearance": "light"
}
```

No timestamp or sequence number is required in v1. The message represents **current state**, not an event that must be replayed in order.

## State and delivery semantics

1. **Initial snapshot:** when a connection becomes usable, the sender sends the current effective appearance, even if it has not changed recently.
2. **Change notification:** when the effective macOS appearance changes, the sender sends the new state.
3. **Idempotency:** applying the same appearance more than once must have the same result as applying it once. Receivers must tolerate duplicate state messages.
4. **Reconnect/resynchronisation:** after a disconnect or reconnect (including VM resume or network recovery), the sender sends a fresh current-state snapshot. It does not need to replay missed transitions.
5. **Ordering:** the sender must serialize appearance updates and send them in state-change order. V1 may use only a transport that preserves message order on a single connection; transports that can reorder messages are not supported. After reconnect, the sender sends a fresh current-state snapshot and does not replay queued pre-disconnect updates.
6. **One-way authority:** the Windows receiver does not originate appearance changes for the Mac.

## Validation and failure behaviour

A receiver must validate a message before changing the Windows appearance.

- Reject messages that are not valid UTF-8 JSON objects.
- Reject a missing or incorrectly typed required field.
- Reject a message containing any field not listed in the v1 message format; v1 does not allow optional or unknown fields.
- Reject an unexpected `protocol` identifier.
- Reject an unsupported `version`; do not guess at compatibility.
- Reject an unsupported `type` or an `appearance` value other than `light` or `dark`.
- A rejected message must not change the current Windows appearance.
- Parsing or validation failures must not crash the receiver. Diagnostic logging should avoid dumping arbitrary untrusted payloads.
- Transport errors, connection closure, and reconnect scheduling are handled by the transport/implementation layer; reconnect must trigger a fresh snapshot.

This draft intentionally does not define a wire-level error response or acknowledgement. TCP delivery alone does not prove that the receiver parsed or applied a message. If application-level acknowledgement becomes necessary, specify it explicitly before adding it to v1.

## Compatibility and evolution

- The `version` field identifies the protocol contract, not the application release.
- A receiver that does not support version 1 must reject it without changing appearance.
- Incompatible changes require a new protocol version.
- New message types or values are not implicitly supported by v1.
- Implementations must not infer an appearance from absent or invalid fields.

## Transport and security boundary

The message contract is transport-neutral. A local WebSocket remains a candidate, not a decision made by this document.

Before production networking is implemented, the project must record in an ADR:
- connection direction and host/guest network assumptions;
- bind address and exposure limits;
- how an untrusted local process is prevented from changing the guest appearance;
- whether pairing or authentication is required;
- firewall implications and behaviour when the VM is stopped, suspended, or its address changes.

The first release remains one Mac host → one Windows VM, local and cloud-free. Do not interpret “local” or “Shared Networking” as authentication.

## Conformance checklist

A v1 sender/receiver pair should demonstrate:

- [ ] Both example messages parse and represent the expected appearance.
- [ ] Missing fields, wrong JSON types, unknown/additional fields, wrong protocol identifier, unsupported version/type, and invalid appearance values are rejected.
- [ ] Rejected messages do not change appearance.
- [ ] Applying a duplicate message is harmless.
- [ ] The sender transmits current state on initial connection and after reconnect, without replaying stale queued updates.
- [ ] The transport preserves message order on a single connection, updates are sent in state-change order, and reconnect sends a fresh current-state snapshot without replaying queued pre-disconnect updates.
- [ ] A compatible implementation can be written using this document without relying on private application details.
