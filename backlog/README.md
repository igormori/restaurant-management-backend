# Backlog

One file per item, filed by type.

| Folder | What goes there |
|---|---|
| [features/](features/) | New capability that doesn't exist yet |
| [bugs/](bugs/) | Something that exists but behaves incorrectly |
| [improvements/](improvements/) | Existing capability that works but should work better |
| [tech-debt/](tech-debt/) | Internal quality issues (outdated code, missing tests, fragile parts) |

## Features
| Item | Size | File |
|---|---|---|
| Forgot / change password | Medium | [password-reset.md](features/password-reset.md) |
| Two-factor authentication | Large | [two-factor-auth.md](features/two-factor-auth.md) |
| Audit log for auth events | Medium | [audit-log.md](features/audit-log.md) |

## Bugs
| Item | File |
|---|---|

## Improvements
| Item | Size | File |
|---|---|---|
| Session redesign: access pass revocation | Medium | [session-redesign.md](improvements/session-redesign.md) |
| Reject breached passwords | Small | [breached-passwords.md](improvements/breached-passwords.md) |
| Menu: deactivate instead of delete | Medium | [2026-10-15-menu-deactivate-instead-of-delete.md](improvements/2026-10-15-menu-deactivate-instead-of-delete.md) |

## Tech debt
| Item | Size | File |
|---|---|---|
| Wire HTTPS into the running app | Small | [https-wiring.md](tech-debt/https-wiring.md) |
| Vulnerable `System.Security.Cryptography.Pkcs` dependency | Small | [pkcs-dependency.md](tech-debt/pkcs-dependency.md) |

Suggested order for the auth items: https-wiring, pkcs-dependency, session-redesign, password-reset, then the rest.
