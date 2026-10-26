# Ideas and Future Possibilities

This file captures ideas that are not current commitments. Promote an idea to a GitHub issue only when it is concrete enough to investigate or implement.

## Open protocol and alternative clients

**Commitment:** document protocol semantics in a way that independent implementations could be built in the future.

**Not a first-release requirement:** supporting multiple clients, arbitrary Windows PCs, third-party implementations, discovery or compatibility guarantees.

Design for openness early by avoiding unnecessarily app-specific message semantics and by documenting versioning, validation and error behaviour. Do not build a plugin framework or multi-client server until a real use case justifies it.

## Potential future ideas
- A documented protocol specification and example messages.
- A small diagnostic command or status screen.
- A configuration export/import option.
- Optional client identity or pairing flow if needed by the threat model.
- Protocol compatibility tests for an independent client.
- Additional VM platforms only if there is personal interest.

## Decision rule
The project is a focused personal side project. Prefer the smallest implementation that is safe, reliable, maintainable and fun to work on. Do not add infrastructure just to support hypothetical scale.
