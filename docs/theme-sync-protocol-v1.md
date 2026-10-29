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

Pairing is an explicit local setup flow. No credentials are sent to cloud services. The receiver accepts only one paired Mac in v1.

1. On first setup, the Windows receiver creates a self-signed TLS server certificate and private key if no identity exists. Persist them in the Windows Current User → Personal certificate store, with private-key access restricted to the Windows user running the companion. The identity must survive normal VM restarts and application upgrades.
2. The Windows companion displays the full SHA-256 fingerprint of its own certificate. The Mac discovers the Windows endpoint and inspects the certificate presented by the live TLS endpoint without trusting it and without sending any pairing secret.
3. The Mac displays the full SHA-256 fingerprint calculated from that live peer certificate. The user compares it with the fingerprint displayed directly by the Windows companion and explicitly confirms the match on the Mac. If they do not match or the user rejects pairing, abort without storing a trusted identity or sending a secret.
4. Before provisioning, the user must explicitly enable **Pairing mode** in the local Windows companion UI. Pairing mode is a deliberate local setup action and cannot be enabled over the network. The receiver must reject `pair.request` when Pairing mode is not active.
5. Only after fingerprint confirmation, the Mac pins the verified certificate identity in Keychain and generates a cryptographically random 256-bit pairing secret.
6. The Mac stores the secret as pending in Keychain, establishes TLS with certificate validation against the newly pinned identity, and sends the secret using the bootstrap exchange in section 3.1. The secret must never be sent before fingerprint confirmation.
7. Windows accepts a pairing request only while Pairing mode is active and no Mac is already paired. It protects and persists the secret using DPAPI before returning success. The Mac activates its pending secret only after receiving a valid success result over the same pinned TLS identity.
8. On subsequent connections, the Mac validates the TLS certificate against the pinned identity before sending the existing secret in `auth.request`. Windows authenticates every new connection before accepting appearance messages.

The pairing secret is represented in protocol messages as unpadded Base64URL. Decoding it must produce exactly 32 bytes. The secret authorizes the sender; certificate pinning authenticates the receiver. Neither replaces the other. Use platform TLS/crypto libraries; do not invent cryptographic primitives. Full fingerprint comparison is a user-operated first-pairing trust step, not automatic trust-on-first-use.

### 3.1 First-pairing bootstrap exchange

This exchange is permitted only after the user has explicitly enabled Pairing mode in the local Windows companion UI and the Mac has verified and pinned the live TLS certificate after manual fingerprint comparison. The receiver accepts at most one Mac and rejects a pairing request if Pairing mode is inactive or a pairing already exists. These failure conditions use the same generic error result.

**Mac → Windows: pairing request**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "pair.request",
  "secret": "<base64url-secret>"
}
```

The `secret` is the unpadded Base64URL encoding of exactly 32 cryptographically random bytes.

**Windows → Mac: pairing success**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "pair.result",
  "status": "paired"
}
```

Windows may send this success result only after it has successfully persisted the secret using DPAPI. The Mac activates the pending Keychain secret only after receiving a valid success result over the same pinned TLS identity.

**Windows → Mac: pairing failure**

```json
{
  "protocol": "dynamic-dark-mode-sync",
  "version": 1,
  "type": "pair.result",
  "status": "error",
  "code": "pairing_failed"
}
```

Use the same generic `pairing_failed` result for inactive Pairing mode, an already-paired receiver, or a provisioning/storage failure; do not disclose unnecessary receiver state. Malformed requests may be rejected by closing the connection without a response. A network request must never replace, reset or revoke an existing pairing.

**Uncertain provisioning outcome:** if the connection drops or the Mac does not receive a valid success result, the Mac must not activate the pending secret or automatically resend it. It must discard the pending secret and pin state for that incomplete attempt and report that deliberate re-pairing is required. Windows must not automatically accept a retry or overwrite an existing stored secret. If Windows may have persisted the secret before the acknowledgement was lost, the user must explicitly reset/revoke pairing locally on Windows and remove the incomplete pairing on the Mac, then restart the full fingerprint-verification and pairing flow. There is no idempotent provisioning retry in v1.

The receiver must validate exact fields and reject duplicate JSON member names. Never log secrets or raw credential material.

### 3.2 Authentication exchange

The following exchange applies to an already paired Mac. Do not use `auth.request` to provision a secret to an unpaired receiver.

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

The receiver must validate the secret before accepting any appearance message. The sender must not send `appearance.set` until it receives a valid `auth.result` with authenticated status.

On authentication failure, the receiver must not accept appearance commands, must return the generic failure result when appropriate, and must close the connection. Authentication attempts should be rate-limited. Malformed authentication messages may be rejected by closing the connection without a response. The receiver must validate exact fields and reject duplicate JSON member names. Never log secrets or raw credential material. Authentication is required on every new connection, and revoked credentials must no longer authorize updates.

### 3.3 Lifecycle and security failure

- Revoke pairing explicitly on either side; delete stored credentials and stop sync until paired again.
- Re-pairing generates a new secret; never reuse revoked credentials.
- A missing, invalid or changed pinned certificate fails closed and requires explicit re-pairing. Never silently accept a replacement or automatically trust a regenerated identity.
- If the receiver certificate/private key or either side's pairing credentials are lost, unavailable or invalid, stop syncing and require deliberate re-pairing. Do not silently reset trust.
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

- Continue retrying the latest desired state until it is acknowledged as `applied`, an `apply_failed` acknowledgement is received, the connection closes, or synchronization is disabled.
- Cap the retry delay to avoid rapid retry loops and unbounded delay growth.
- If the desired appearance changes while a retry is pending, replace the pending value with the new latest appearance. Do not replay the superseded state as a historical transition.
- If Windows recovers while the connection remains open and the desired appearance has not changed, the sender must still retry the latest state.
- If the connection closes, stop retries on that connection. After reconnecting and reauthenticating, send a fresh snapshot of the current desired appearance.

### 5.3 Application failure

`apply_failed` reports that Windows could not apply the requested appearance and must stop any active retry loop for that state. It must not trigger or continue the automatic retry loop reserved for `temporarily_unavailable`. Wait for a later appearance change or successful reconnection before attempting the current desired appearance again.

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
- [ ] Receiver binding meets the restrictions in section 2. If the intended binding cannot be confirmed, an equivalent narrowly scoped firewall rule is confirmed active before startup; a wildcard listener protected only by a firewall is never used.
- [ ] Valid `light` and `dark` appearance messages are accepted.
- [ ] Missing/wrongly typed fields, extra fields, wrong protocol identifier, unsupported version/type and invalid appearance are rejected.
- [ ] Duplicate JSON member names are rejected, including repeated `appearance` fields.
- [ ] Boolean `version`, malformed JSON, invalid UTF-8, non-object JSON, and messages exceeding 4096 bytes are rejected.
- [ ] Rejected messages never change Windows appearance.

### Authentication and security

- [ ] Authentication request and success/failure results match the defined schemas.
- [ ] The secret is unpadded Base64URL and decodes to exactly 32 bytes.
- [ ] The Mac displays the fingerprint derived from the live TLS peer certificate and sends no secret before the user confirms it matches the fingerprint displayed directly by Windows.
- [ ] A rejected/mismatched fingerprint leaves no trusted pin and sends no pairing secret.
- [ ] Pairing is rejected unless the user has explicitly enabled local Pairing mode in the Windows companion; the network cannot enable Pairing mode.
- [ ] First pairing provisions a Mac-generated 256-bit secret only over TLS validated against the user-confirmed pinned identity.
- [ ] Pairing requests and success/failure results match the exact schemas in section 3.1; inactive Pairing mode, already-paired state and storage failure use the generic pairing_failed result.
- [ ] Windows sends a successful paired result only after the secret is persisted using DPAPI; the Mac activates its pending Keychain secret only after a valid success result.
- [ ] An uncertain provisioning disconnect never triggers an automatic retry or silent credential replacement; deliberate local reset/re-pairing is required on both sides.
- [ ] The Windows receiver certificate/private key persists in the Current User → Personal certificate store with private-key access restricted to the companion's Windows user.
- [ ] Missing/lost certificate or private key fails closed and requires explicit re-pairing; no replacement identity is silently trusted.
- [ ] The Mac rejects an unpinned or changed receiver certificate and never sends the existing secret before certificate verification.
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

The following initial implementation defaults are agreed; validate them in the proof of concept and tune only if tests justify a change:

- Pairing mode is explicitly enabled in the local Windows UI and expires after **5 minutes**. The receiver must not accept a new pairing request after expiry.
- Connection timeout: **5 seconds**.
- Appearance acknowledgement timeout: **3 seconds**.
- Reconnect and temporary-unavailability retry: **bounded exponential backoff with jitter**, starting at **1 second** and capped at **30 seconds**.
- Authentication rate limit: **5 failed attempts per minute**, followed by a cooldown. The exact cooldown duration remains to be selected during implementation.

Still open:

1. Exact port and how to configure/find the Windows guest endpoint in Shared Network mode.
2. TLS library, certificate format and pinning API compatible with supported OS versions.
3. Exact cooldown duration for authentication rate limiting.
4. Exact acknowledgement semantics when Windows accepts a preference but some apps update asynchronously.
5. Whether the chosen JSON libraries can reliably reject duplicate object member names; if not, choose a parser/validation strategy that can.

This document defines the proposed contract; it is not evidence that the transport, pairing UX or end-to-end behaviour has been implemented or tested.
