# Backlog

One file per item, filed by type.

| Folder | What goes there |
|---|---|
| [features/](features/) | New capability that doesn't exist yet |
| [bugs/](bugs/) | Something that exists but behaves incorrectly |
| [improvements/](improvements/) | Existing capability that works but should work better |
| [tech-debt/](tech-debt/) | Internal quality issues (outdated code, missing tests, fragile parts) |
| [pre-production/](pre-production/) | Must be done before launch, outside the code (deployment, certificates, secrets, infrastructure) |

## Features
| Item | Size | File |
|---|---|---|
| Forgot / change password | Medium | [password-reset.md](features/password-reset.md) |
| Two-factor authentication | Large | [two-factor-auth.md](features/two-factor-auth.md) |
| Audit log for auth events | Medium | [audit-log.md](features/audit-log.md) |
| Remove a member and end their sessions | Medium | [2026-10-16-remove-member-and-end-sessions.md](features/2026-10-16-remove-member-and-end-sessions.md) |

## Bugs
| Item | File |
|---|---|

## Pre-production
| Item | File |
|---|---|
| Deploy the HTTPS certificate | [2026-10-16-https-certificate-deployment.md](pre-production/2026-10-16-https-certificate-deployment.md) |

## Improvements
| Item | Size | File |
|---|---|---|
| Session redesign: access pass revocation | Medium | [session-redesign.md](improvements/session-redesign.md) |
| Reject breached passwords | Small | [breached-passwords.md](improvements/breached-passwords.md) |
| Emailed codes expire after 10 minutes | Small | [2026-10-16-email-codes-expire-in-10-minutes.md](improvements/2026-10-16-email-codes-expire-in-10-minutes.md) |
| Sessions end after 7 days unused or 30 days after login | Medium | [2026-10-16-session-inactivity-and-absolute-limit.md](improvements/2026-10-16-session-inactivity-and-absolute-limit.md) |
| Login lockout per IP address | Medium | [2026-10-16-login-lockout-per-ip-address.md](improvements/2026-10-16-login-lockout-per-ip-address.md) |
| Menu: deactivate instead of delete | Medium | [2026-10-15-menu-deactivate-instead-of-delete.md](improvements/2026-10-15-menu-deactivate-instead-of-delete.md) |

## Tech debt
| Item | Size | File |
|---|---|---|
| Vulnerable `System.Security.Cryptography.Pkcs` dependency | Small | [pkcs-dependency.md](tech-debt/pkcs-dependency.md) |
| ASVS small fixes and missing tests (Identity) | Small | [2026-10-16-asvs-small-fixes-and-missing-tests.md](tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md) |

Suggested order for the auth items: pkcs-dependency, session-redesign, password-reset, then the rest.
