# Theme Sync Protocol v1

- **Status:** Proposed for review; not production-ready until open implementation decisions are resolved.
- **Related issue:** [#8 — Specify an open, versioned theme-sync protocol](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/8)
- **Protocol identifier:** `dynamic-dark-mode-sync`
- **Version:** `1`
- **Scope:** One macOS host sends its effective Light/Dark appearance to one paired Windows VM. No cloud, relay, discovery, multi-client hosting, Windows-to-Mac commands, schedules, wallpaper or event history.

## 1. Roles and authority

The macOS app is the sole authority for desired appearance. The Windows companion receives and applies the state for the current Windows user. Windows does not send appearance state back. While disconnected, Windows retains its last successfully applied state.

Keep protocol parsing, transport, macOS appearance observation and Windows theme application behind separately testable boundaries. Existing scheduling, wallpaper, brightness, shortcut, Touch Bar and launcher behaviour must remain independent.

## 2. Proposed transport

Use TCP protected by TLS 1.3, subject to validating compatibility with supported OS versions and confirming host-to-guest reachability in Parallels Shared Network mode.

- macOS initiates the connection; Windows listens on a configurable port.
- Bind the listener to the intended Parallels interface where feasible, not indiscriminately to every interface.
- Restrict inbound access with Windows Firewall as far as the actual network topology permits.
- Do not treat a private IP, port number or Shared Network as authentication.
- No plaintext fallback, automatic LAN discovery, cloud relay or arbitrary LAN clients in v1.

### 2.1 Framing

TCP is a byte stream. Each message is framed as a 4-byte unsigned payload length in network byte order (big-endian), followed by exactly that many UTF-8 JSON bytes. The length excludes the prefix.

Reject zero-length payloads and lengths above 4096 bytes before allocating or parsing. Reject truncated frames and malformed UTF-8. Use bounded read/write loops; a socket read or write is not guaranteed to transfer a complete frame.

## 3. Pairing and authentication

Pairing is an explicit local setup flow. No credentials are sent to cloud services.

1. The Windows receiver creates or obtains its TLS server certificate and displays its fingerprint locally.
2. The user verifies/transfers that fingerprint to the Mac through the pairing UI. The Mac pins the expected certificate identity. Never silently trust a certificate merely because it was presented over the network.
3. Windows generates a cryptographically random 256-bit pairing secret.
4. The user transfers the secret to the Mac through an explicit local setup step. Never transfer it over an unverified connection, put it in a URL or command-line argument, or log it.
5. Store the secret in macOS Keychain on the Mac and protect the Windows copy using DPAPI for the appropriate user.
6. The Mac establishes TLS, validates the pinned certificate, then authenticates using the pairing secret inside the encrypted channel. Windows must authenticate each new connection before accepting appearance messages. Compare secrets in constant time where supported.
7. Pairing succeeds only after authentication is confirmed.

The secret authorizes the sender; certificate pinning authenticates the receiver. Neither replaces the other. Use platform TLS/crypto libraries; do not invent cryptographic primitives.

### 3.1 Lifecycle and failure

- Revoke pairing explicitly on either side; delete stored credentials and stop sync until paired again.
- Re-pairing generates a new secret; never reuse revoked credentials.
- A missing, invalid or changed pinned certificate fails closed and requires explicit re-pairing/renewal. Never silently accept a replacement.
- Missing/unavailable credential storage fails closed.
- Rate-limit repeated failed authentication attempts and close unauthenticated connections.
- Never log secrets, private keys or raw credential material.
- TLS plus pinning and pairing does not protect against a compromised endpoint or an attacker who controls the paired user's account and credentials. Firewall restrictions are defence in depth, not authentication.

## 4. Message format

Messages are UTF-8 JSON objects. V1 state messages contain exactly the four fields below; unknown fields are rejected. Adding fields requires an explicitly reviewed protocol change. Duplicate JSON member names must be detected and rejected before field validation; do not rely on a parser's default behaviour.

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "appearance.set",
  "appearance": "dark"
}
```

| Field | Type | Rule |
| --- | --- | --- |
| `protocol` | string | Exactly `dynamic-dark-mode-sync` |
| `version` | integer | Exactly `1`; JSON booleans are not accepted as integers |
| `type` | string | Exactly `appearance.set` |
| `appearance` | string | Exactly `light` or `dark` |

The message describes current state, not a historical event. No timestamp or sequence number is required in v1.

## 5. State and acknowledgements

- Send current state immediately after authentication and whenever effective macOS appearance changes while sync is enabled.
- Applying the same state repeatedly must be harmless; avoid unnecessary Windows setting writes.
- Send updates in order on one connection. Send one appearance message at a time and wait for its acknowledgement before sending the next.
- The receiver acknowledges a valid, authenticated state message only after successfully handling it. Acknowledgement means the receiver handled the setting, not that every app has repainted.
- Proposed success acknowledgement:

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "ack",
  "status": "applied",
  "appearance": "dark"
}
```

- Proposed failure acknowledgement for a valid, authenticated message that could not be applied:

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "ack",
  "status": "error",
  "code": "apply_failed"
}
```

A duplicate state may be acknowledged as `applied` if it is already active. Error codes must be machine-readable and contain no secrets. Initial codes: `apply_failed`, `temporarily_unavailable`. Malformed or unauthenticated messages receive no protocol acknowledgement; close the connection.

## 6. Reconnection and sync preference

- Either component may start first.
- On disconnect, reconnect with bounded exponential backoff and jitter.
- After reconnect, repeat TLS validation and sender authentication, then send the latest current state.
- Do not queue or replay historical transitions while disconnected.
- On VM suspend/resume, host network changes or receiver restart, the system must eventually reconnect and resynchronise without manual intervention.
- Disabling sync stops updates and closes the connection. Re-enabling sync reconnects and sends current state.
- If an acknowledgement times out, close/reconnect and resend current state; duplicate application is safe.
- If Windows cannot apply a valid state, report failure rather than falsely acknowledging success. Do not require administrator privileges solely to change the current user's theme.

## 7. Validation and compatibility

Reject before changing Windows appearance if any of the following occurs:

- invalid UTF-8, malformed/non-object JSON, duplicate member names, truncated/zero-length/oversized frame;
- missing field, wrong type, additional field, wrong protocol identifier;
- unsupported version, unsupported message type or invalid appearance value;
- TLS certificate verification or sender authentication failure.

Unsupported versions are rejected; there is no silent downgrade. Incompatible changes require a new protocol version. Invalid input must not crash the receiver or cause a default appearance to be applied. Avoid logging arbitrary untrusted payloads.

## 8. Conformance tests

### Message and framing
- [ ] Valid `light` and `dark` messages.
- [ ] Missing/wrongly typed fields, extra fields, wrong protocol identifier, unsupported version/type and invalid appearance rejected.
- [ ] Duplicate JSON member names rejected, including repeated `appearance`.
- [ ] Boolean `version`, malformed JSON, invalid UTF-8, zero-length, oversized and truncated frames rejected.
- [ ] Rejected messages never change Windows appearance.

### Security
- [ ] Sender rejects unpinned or changed receiver certificate.
- [ ] Pairing secret is never sent before certificate verification.
- [ ] Unauthenticated, incorrect-secret and revoked senders cannot update appearance.
- [ ] Failed authentication is rate-limited and secrets never appear in logs.
- [ ] No plaintext fallback.

### State and lifecycle
- [ ] Initial state sent after authentication; duplicates are harmless.
- [ ] Current state sent after change and after reconnect; no stale queued transitions replayed.
- [ ] Start-order, timeout, disconnect, VM suspend/resume and transient network recovery work.
- [ ] Disabling sync stops updates; enabling sync sends current state.
- [ ] Apply failure is reported and not acknowledged as success.

### Environment
- [ ] Verify Shared Network host-to-guest reachability, stable/configurable endpoint, listener binding and firewall behaviour.
- [ ] Record tested macOS, Windows and Parallels versions.
- [ ] Verify current-user theme changes without unnecessary elevation.

## 9. Open implementation decisions

The proof of concept must validate these before v1 is considered production-ready:

1. Exact port and how to configure/find the Windows guest endpoint in Shared Network mode.
2. TLS library, certificate format and pinning API compatible with supported OS versions.
3. Safe pairing UI for comparing the certificate fingerprint and transferring the secret locally.
4. Concrete connect, acknowledgement and backoff timeout values.
5. Exact acknowledgement semantics when Windows accepts a preference but some apps update asynchronously.
6. Whether the chosen JSON libraries can reliably reject duplicate object member names; if not, choose a parser/validation strategy that can.

This document defines the proposed contract; it is not evidence that the transport, pairing UX or end-to-end behaviour has been implemented or tested.
