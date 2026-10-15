# Security backlog

Open items left from the auth security review. One file per item.

| # | Item | Size | File |
|---|---|---|---|
| 1 | Wire HTTPS into the running app | Small | [https-wiring.md](https-wiring.md) |
| 2 | Vulnerable `System.Security.Cryptography.Pkcs` dependency | Small | [pkcs-dependency.md](pkcs-dependency.md) |
| 3 | Session redesign: logout, revocation, multiple devices | Large | [session-redesign.md](session-redesign.md) |
| 4 | Forgot / change password | Medium | [password-reset.md](password-reset.md) |
| 5 | Two-factor authentication | Large | [two-factor-auth.md](two-factor-auth.md) |
| 6 | Reject breached passwords | Small | [breached-passwords.md](breached-passwords.md) |
| 7 | Audit log for auth events | Medium | [audit-log.md](audit-log.md) |

Suggested order: 1, 2, 3, 4, then the rest.
