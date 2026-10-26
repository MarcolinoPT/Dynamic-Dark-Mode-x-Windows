# Security Policy

## Supported versions
Security reports are currently handled for the latest development branch. There are no formal release support guarantees yet.

## Reporting a vulnerability
Please do not publish exploitable details in a public issue. Use GitHub's private vulnerability reporting feature if it is enabled for this repository; otherwise contact the repository owner through a private channel available on their GitHub profile.

Include affected component, reproduction steps, impact and any suggested mitigation. Do not include secrets or personal data.

## Security design principles
- Keep synchronisation local and bind only as broadly as required.
- Validate all received messages and reject unknown or malformed versions safely.
- Avoid logging authentication material or sensitive network details.
- Treat AI security findings as leads to verify, not proof of vulnerability absence.
