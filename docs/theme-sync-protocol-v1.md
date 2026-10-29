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

Use WebSocket over TLS (`wss://`), consistent with accepted [ADR 0002](decisions/0002-local-host-guest-theme-sync.md). Validate compatibility with supported OS versions and confirm host-to-guest reachability in Parallels Shared Network mode.

- macOS initiates the connection; Windows listens on a configurable port.
- Bind the listener to the intended Parallels virtual-network interface/address. If the intended binding cannot be confirmed at startup, the receiver must not start unless an equivalent narrowly scoped Windows Firewall rule has first been confirmed active.
- Do not use a wildcard listener protected only by a firewall.
- Do not rely on a fixed guest IP address.
- Do not treat a private IP, port number or Shared Network as authentication.
- No plaintext fallback, automatic LAN discovery, cloud relay or arbitrary LAN clients in v1.

### 2.1 WebSocket messages and framing

Each protocol message is one complete WebSocket text message containing a UTF-8 JSON object. Use the WebSocket library's message API; do not add a custom length prefix.

- The maximum application payload is 4096 bytes.
- Reject messages exceeding the limit, malformed UTF-8, invalid JSON, and JSON values that are not objects.
- Reject duplicate JSON member names before field validation; do not rely on a parser's default duplicate-key behaviour.
- Each message type defines its exact permitted fields. Reject unknown fields and missing required fields.
- Do not interpret multiple JSON objects inside one WebSocket message as separate protocol messages.

## 3. Pairing and authentication

Pairing is an explicit local setup flow. No credentials are sent to cloud services.

1. The Windows receiver creates or obtains its TLS server certificate and displays its fingerprint locally.
2. The user verifies/transfers that fingerprint to the Mac through the pairing UI. The Mac pins the expected certificate identity. Never silently trust a certificate merely because it was presented over the network.
3. Windows generates a cryptographically random 256-bit pairing secret.
4. The user transfers the secret to the Mac through an explicit local setup step. Never transfer it over an unverified connection, put it in a URL or command-line argument, or log it.
5. Store the secret in macOS Keychain on the Mac and protect the Windows copy using DPAPI for the appropriate user.
6. The Mac establishes the WebSocket over TLS connection, validates the pinned certificate, then authenticates using the pairing secret inside the verified TLS channel. Windows must authenticate each new connection before accepting appearance messages.
7. Pairing succeeds only after the Mac receives a valid successful authentication result.

The pairing secret is represented in protocol messages as unpadded Base64URL. Decoding the value must produce exactly 32 bytes. The secret authorizes the sender; certificate pinning authenticates the receiver. Neither replaces the other. Use platform TLS/crypto libraries; do not invent cryptographic primitives.

### 3.1 Authentication exchange

Authentication messages are WebSocket text messages containing JSON objects. Each message must match the schema shown below exactly.

**Mac → Windows: authentication request**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "auth.request",
  "secret": "<base64url-secret>"
}
```

The `secret` field contains the unpadded Base64URL encoding of the 32-byte pairing secret. The Mac must not send this message until TLS certificate validation against the pinned identity has succeeded.

**Windows → Mac: authentication success**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "auth.result",
  "status": "authenticated"
}
```

**Windows → Mac: authentication failure**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "auth.result",
  "status": "error",
  "code": "authentication_failed"
}
```

The receiver must validate the secret before accepting any appearance message. The sender must not send `appearance.set` until it receives a valid `auth.result` with `status: "authenticated"`.

On authentication failure, the receiver must not accept appearance commands, must return the generic failure result when appropriate, and must close the connection. Authentication attempts should be rate-limited. Malformed authentication messages may be rejected by closing the connection without a response. The receiver must validate exact fields and reject duplicate JSON member names. Never log secrets or raw credential material. Authentication is required on every new connection, and revoked credentials must no longer authorize updates.

### 3.2 Lifecycle and security failure

- Revoke pairing explicitly on either side; delete stored credentials and stop sync until paired again.
- Re-pairing generates a new secret; never reuse revoked credentials.
- A missing, invalid or changed pinned certificate fails closed and requires explicit re-pairing/renewal. Never silently accept a replacement.
- Missing/unavailable credential storage fails closed.
- Rate-limit repeated failed authentication attempts and close unauthenticated connections.
- Never log secrets, private keys or raw credential material.
- TLS plus pinning and pairing does not protect against a compromised endpoint or an attacker who controls the paired user's account and credentials. Firewall restrictions are defence in depth, not authentication.

## 4. Message format and validation

Messages are UTF-8 JSON objects. Each message type has an exact schema; unknown fields are rejected. Adding fields requires an explicitly reviewed protocol change. Duplicate JSON member names must be detected and rejected before field validation.

Every message must contain the protocol identifier `dynamic-dark-mode-sync` and integer version `1`. JSON booleans are not accepted as integers. Unsupported versions are rejected; there is no silent downgrade.

### 4.1 Appearance request

**Mac → Windows**

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

### 4.2 Acknowledgements

The receiver acknowledges a valid, authenticated appearance request only after successfully handling it. An `applied` acknowledgement means the receiver successfully applied or confirmed the requested setting; it does not guarantee that every application has repainted.

**Successfully applied**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "ack",
  "status": "applied",
  "appearance": "dark"
}
```

**Application failure**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "ack",
  "status": "error",
  "code": "apply_failed"
}
```

**Temporarily unavailable**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "ack",
  "status": "error",
  "code": "temporarily_unavailable"
}
```

Initial error codes are `apply_failed`, `temporarily_unavailable`, and `authentication_failed` (for `auth.result` only). Error codes must be machine-readable and contain no secrets. A malformed or unauthenticated message must not change Windows appearance; it may be rejected without a protocol acknowledgement, with the connection closed where appropriate.

Applying the same appearance repeatedly must be harmless. A duplicate state may be acknowledged as `applied` if it is already active. The sender must serialize updates and have at most one unacknowledged appearance request at a time.

## 5. State, retries and recovery

- Send current state immediately after authentication and whenever effective macOS appearance changes while sync is enabled.
- Send updates in order on one connection. Wait for an acknowledgement before sending the next appearance request.
- Applying the same state more than once must have the same result as applying it once. Avoid unnecessary Windows setting writes.
- The protocol carries current state, not a history of transitions. Do not queue or replay historical transitions.

### 5.1 Acknowledgement timeout

The sender must use a bounded timeout when waiting for an acknowledgement; the concrete value remains an implementation decision. If the acknowledgement times out, the sender must treat the outcome as uncertain, close/reconnect, reauthenticate, and send a fresh snapshot of the latest desired appearance. Duplicate application must be harmless.

### 5.2 Temporary unavailability

When Windows returns an `ack` with `status: "error"` and `code: "temporarily_unavailable"`, the sender must retain the latest desired appearance and retry it using bounded exponential backoff with jitter.

- Continue retrying the latest desired state until it is acknowledged as `applied`, the connection closes, or synchronization is disabled.
- Cap the retry delay to avoid rapid retry loops and unbounded delay growth.
- If the desired appearance changes while a retry is pending, replace the pending value with the new latest appearance. Do not replay the superseded state as a historical transition.
- If Windows recovers while the connection remains open and the desired appearance has not changed, the sender must still retry the latest state.
- If the connection closes, stop retries on that connection. After reconnecting and reauthenticating, send a fresh snapshot of the current desired appearance.

### 5.3 Application failure

`apply_failed` reports that Windows could not apply the requested appearance. It must not trigger the automatic retry loop reserved for `temporarily_unavailable`. A later appearance change or successful reconnection may trigger a fresh attempt for the current desired state.

### 5.4 Reconnection and sync preference

- Either component may start first.
- On disconnect, reconnect with bounded exponential backoff and jitter.
- After reconnect, repeat TLS validation and sender authentication, then send the latest current state.
- On VM suspend/resume, host network changes or receiver restart, the system must eventually reconnect and resynchronise without manual intervention.
- Disabling sync stops updates and closes the connection. Re-enabling sync reconnects and sends current state.
- Do not queue or replay historical transitions while disconnected.
- If Windows cannot apply a valid state, report failure rather than falsely acknowledging success. Do not require administrator privileges solely to change the current user's theme.

## 6. Validation and compatibility

Reject a message before changing Windows appearance if any of the following occurs:

- the WebSocket application message exceeds 4096 bytes;
- invalid UTF-8, malformed JSON, a non-object JSON value, or duplicate member names;
- a missing field, wrong field type, additional field, or incorrect protocol identifier;
- an unsupported version, unsupported message type, or invalid appearance value;
- an invalid authentication schema or failed sender authentication;
- TLS certificate verification or pinned-identity validation fails.

There is no plaintext fallback or silent version downgrade. Incompatible changes require a new protocol version. Invalid input must not crash the receiver or cause a default appearance to be applied. Avoid logging arbitrary untrusted payloads.

## 7. Conformance tests

### Transport and validation

- [ ] Connection uses WebSocket over TLS; no custom length prefix is used.
- [ ] Receiver binding is restricted to the intended virtual-network interface/address, or an equivalent narrowly scoped firewall rule is confirmed active before startup.
- [ ] Valid `light` and `dark` appearance messages are accepted.
- [ ] Missing/wrongly typed fields, extra fields, wrong protocol identifier, unsupported version/type and invalid appearance are rejected.
- [ ] Duplicate JSON member names are rejected, including repeated `appearance` fields.
- [ ] Boolean `version`, malformed JSON, invalid UTF-8, non-object JSON, and messages exceeding 4096 bytes are rejected.
- [ ] Rejected messages never change Windows appearance.

### Authentication and security

- [ ] Authentication request and success/failure results match the defined schemas.
- [ ] The secret is unpadded Base64URL and decodes to exactly 32 bytes.
- [ ] The Mac rejects an unpinned or changed receiver certificate and never sends the secret before certificate verification.
- [ ] Unauthenticated, incorrect-secret and revoked senders cannot update appearance.
- [ ] Appearance requests sent before successful authentication are rejected.
- [ ] Authentication is repeated after reconnect; failed authentication is rate-limited and secrets never appear in logs.
- [ ] No plaintext fallback is possible.

### State and recovery

- [ ] Initial state is sent after authentication; applying duplicate state is harmless.
- [ ] Current state is sent after a change and after reconnect; no stale queued transitions are replayed.
- [ ] If an acknowledgement is lost after Windows applies the setting, retry/reconnect does not cause an incorrect result.
- [ ] When Windows returns `temporarily_unavailable` and later recovers without disconnecting, the Mac retries the latest state using capped backoff and Windows eventually acknowledges `applied`.
- [ ] A newer desired appearance supersedes an older pending retry.
- [ ] `apply_failed` is reported and does not trigger the temporary-unavailability retry loop.
- [ ] Start-order, disconnect, VM suspend/resume and transient network recovery work.
- [ ] Disabling sync stops updates; enabling sync sends current state.

### Environment

- [ ] Verify Shared Network host-to-guest reachability, configurable endpoint, listener binding and firewall behaviour.
- [ ] Record tested macOS, Windows and Parallels versions.
- [ ] Verify current-user theme changes without unnecessary elevation.

## 8. Open implementation decisions

The proof of concept must validate these before v1 is considered production-ready:

1. Exact port and how to configure/find the Windows guest endpoint in Shared Network mode.
2. TLS library, certificate format and pinning API compatible with supported OS versions.
3. Safe pairing UI for comparing the certificate fingerprint and transferring the secret locally.
4. Concrete connect, acknowledgement, retry delay/cap, jitter and authentication rate-limit values.
5. Exact acknowledgement semantics when Windows accepts a preference but some apps update asynchronously.
6. Whether the chosen JSON libraries can reliably reject duplicate object member names; if not, choose a parser/validation strategy that can.

This document defines the proposed contract; it is not evidence that the transport, pairing UX or end-to-end behaviour has been implemented or tested.
