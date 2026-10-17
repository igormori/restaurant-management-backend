# Identity: OWASP ASVS 5.0 Level 2 gap report

**Date:** 2026-10-16 · **Standard:** OWASP ASVS 5.0.0 (latest release), Level 1 and 2 requirements ·
**Compared with:** `docs/security/identity-matrix.md`, plus Web (`W`) and Shared (`SH`) rows where
Identity relies on them · **Nothing changed:** no code, test or matrix edits.

**Statuses**
- **Covered:** matrix rows with passing tests prove it.
- **Partial:** part is proven, or the code already does it but no row or test proves it. The
  missing part is named.
- **Missing:** the code does not do it, so there is no rule or test.
- **Not applicable:** the feature does not exist in this backend. The reason is given.

**Risk** (summary order): **High** means an attacker can get into or stay in an account.
**Medium** means attacks last longer or go unseen. **Low** means hardening, documentation, or
proof of something the code already does.

## Summary: Partial and Missing, by risk

| # | Risk | ASVS | Status | Gap | Related rows |
|---|---|---|---|---|---|
| 1 | High | V6.3.3 | Missing | No multi-factor authentication, and no documented rationale for going without it. ASVS L2 requires one or the other. Owners and Admins control money and staff | — |
| 2 | High | V7.3.2 | Missing | No absolute session lifetime. Each refresh sets a new 30-day expiry, so a stolen refresh cookie works for as long as it keeps being used, unless reuse is detected | F1, T17 |
| 3 | High | V6.2.12 | Missing | New passwords are not checked against breached-password lists (register, reset, change) | R6–R9, P11, C4 |
| 4 | High | V6.2.4 | Missing | New passwords are not checked against a list of common passwords (top 3000) | R6–R9 |
| 5 | Medium | V16.3.1 | Missing | Logins are not logged, successful or failed. Only the lockout is reported (Sentry), so credential stuffing spread across accounts goes unseen | L18, L16 |
| 6 | Medium | V7.4.1, V7.4.2 | Partial | Logout, logout-all, password change and reset revoke refresh tokens only. The access token keeps working for up to 15 min, as do the roles it carries after a role is removed. No row states this window | O2, O5, C5, P6, F4 |
| 7 | Medium | V6.1.1 | Partial | Anyone who knows an email can lock that login with 5 wrong passwords, and repeat it every 15 min. Nothing documents how malicious lockout is prevented | L8, L16 |
| 8 | Medium | V6.5.5 | Partial | Verification and reset codes live 15 min. ASVS allows at most 10 min for out-of-band codes | T16 |
| 9 | Medium | V16.3.2 | Missing | Refused access (403s, select-organization by a non-member) is not logged | S3, W3 |
| 10 | Medium | V7.4.5 | Missing | An Owner or Admin cannot end another user's sessions (for example, a fired waiter's tablet) | O5 |
| 11 | Medium | V7.5.2 | Partial | Users can end every session (logout-all) but cannot see their sessions or end one other device. Ending sessions does not ask for the password again | O5, L15 |
| 12 | Medium | V6.2.5 | Missing (conflicts) | ASVS forbids composition rules. R7 and R8 require an uppercase letter, a digit and a symbol. Product decision | R7, R8 |
| 13 | Medium | V16.3.3 | Partial | Rate-limit rejections are logged (L16). Verify and reset lockouts are not, and neither are other attempts to get past a control | L16, V4, P8 |
| 14 | Low | V7.3.1, V7.1.1, V7.1.2 | Partial | Inactivity timeout (30 days of not refreshing) and session count (unlimited devices) are in the matrix but with no risk decision or NIST justification. Shared restaurant tablets make this matter | L15, T17 |
| 15 | Low | V7.2.4 | Covered | Proven by matrix row L20 | L20 |
| 16 | Low | V6.2.9 | Partial | 64-character passwords are accepted only within 72 bytes (bcrypt limit). 64 accented or non-Latin characters are refused | R6, R9, T7 |
| 17 | Low | V3.3.1, V3.3.3 | Partial | Refresh cookie is `Secure` but named `refresh_token`, with no `__Secure-` prefix. `__Host-` cannot be used with `Path=/api/auth`; that deviation is not recorded | L2, T10 |
| 18 | Low | V9.1.2 | Covered | Proven by matrix row T26 | T26 |
| 19 | Low | V16.2.5 | Partial | Email addresses are logged when a verification email fails to send (`RegistrationService.cs:100`, `VerificationService.cs:138`). Codes and lockouts are kept out of logs (P5, L18) | P5, L18 |
| 20 | Low | V6.1.2, V6.2.11 | Missing | No list of context-specific words (product name, role names, "restaurant"…) blocked in passwords | — |
| 21 | Low | V8.1.2, V8.2.3 | Partial | Response fields are proven (R2, L1, L4). No row proves a client cannot set protected fields such as verified, active or roles through register | R2, L4 |
| 22 | Low | V6.5.3, V7.2.3, V11.5.1 | Partial | Code already uses a secure random generator: 6-digit codes and 64-byte refresh tokens. No test proves it | R1, P2, T8 |
| 23 | Low | V6.2.8 | Partial | Code already checks the password exactly as typed (only email is trimmed and lowercased). No test proves spaces or casing are kept | L3, T7 |
| 24 | Low | V9.2.1 | Covered | Proven by matrix row T21 | T21 |
| 25 | Low | V9.1.3 | Covered | Proven by matrix row T22 | T22 |
| 26 | Low | V14.2.1 | Covered | Proven by matrix row T23 | T23 |
| 27 | Low | V6.3.2 | Covered | Proven by matrix row T24 | T24 |
| 28 | Low | V11.4.1 | Covered | Proven by matrix row T25 | T25 |

**Not counted above:** matrix row V11 (🐞, locked account gets 423 on verify and resend) is an
open bug already in the backlog. It is an account-enumeration leak that no ASVS 5.0 L2
requirement names directly.

## V6 Authentication

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V6.1.1 | 1 | Document anti-brute-force controls and how malicious lockout is prevented | Partial | Limits are documented in the matrix header and proven (L7–L11, L16, T16, T18). Nothing prevents or documents an attacker locking a victim out (summary #7) |
| V6.1.2 | 2 | Document context-specific words banned from passwords | Missing | Summary #20 |
| V6.1.3 | 2 | Document every authentication pathway | Covered | Matrix sections 4–9 list every way to get or renew a session (login, refresh, select organization, reset, change); T3 makes undeclared endpoints private |
| V6.2.1 | 1 | Passwords at least 8 characters | Covered | R6, R9 |
| V6.2.2 | 1 | Users can change their password | Covered | C5 |
| V6.2.3 | 1 | Change requires current and new password | Covered | C2, C4 |
| V6.2.4 | 1 | Check against top 3000 passwords | Missing | Summary #4 |
| V6.2.5 | 1 | No composition rules | Missing (conflicts) | R7 and R8 enforce composition rules (summary #12) |
| V6.2.6 | 1 | Password fields masked | Not applicable | Front-end concern; this is an API |
| V6.2.7 | 1 | Paste and password managers allowed | Not applicable | Front-end concern; the API accepts any pasted value |
| V6.2.8 | 1 | Password checked exactly as received | Partial | Code does not trim or change case; over 72 bytes is refused, never truncated (T7, L13). No test for spaces or casing (summary #23) |
| V6.2.9 | 2 | Passwords of 64+ characters allowed | Partial | Summary #16 |
| V6.2.10 | 2 | No forced periodic rotation | Not applicable | Passwords never expire; there is no rotation rule to test |
| V6.2.11 | 2 | Context-specific words list enforced | Missing | Summary #20 |
| V6.2.12 | 2 | Check against breached passwords | Missing | Summary #3 |
| V6.3.1 | 1 | Credential-stuffing and brute-force controls implemented | Covered | L7–L11, L16, L17, T18 |
| V6.3.2 | 1 | No default accounts | Covered | T24 |
| V6.3.3 | 2 | MFA, or documented rationale and mitigations | Missing | Summary #1 |
| V6.3.4 | 2 | No undocumented pathways; consistent strength | Covered | T3 (private by default); L5, F4, S4, C8 refuse the same inactive, unverified or locked users on every pathway |
| V6.4.1 | 1 | Activation codes random, short-lived, single use | Covered | R1, V2, V8, T16; randomness is code only (summary #22) |
| V6.4.2 | 1 | No password hints or secret questions | Not applicable | None exist |
| V6.4.3 | 2 | Secure forgotten-password process, no MFA bypass | Covered | P1–P14; no MFA exists to bypass |
| V6.4.4 | 2 | Lost MFA factor re-proofed | Not applicable | No MFA |
| V6.5.1 | 2 | Codes usable once | Covered | V8, V7, P7 |
| V6.5.2 | 2 | Low-entropy lookup secrets hashed with salt | Not applicable | No lookup (backup) codes. Email codes are stored as a keyed per-user HMAC (T6) |
| V6.5.3 | 2 | Codes from a secure random generator | Partial | Summary #22 |
| V6.5.4 | 2 | Codes have 20+ bits of entropy | Covered | 6 digits (R1, P2) |
| V6.5.5 | 2 | Out-of-band codes live at most 10 min | Partial | Summary #8 |
| V6.6.1 | 2 | SMS codes only with safeguards | Not applicable | No SMS or phone codes |
| V6.6.2 | 2 | Codes bound to their own request | Covered | V2 (another account's code), V7, P7 (replaced code) |
| V6.6.3 | 2 | Code entry protected by rate limits | Covered | V4, V10, P8, P13 |
| V6.8.1–V6.8.4 | 2 | External identity provider rules | Not applicable | No external identity provider, SSO or SAML. Own tokens are under V9 |

## V7 Session management

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V7.1.1 | 2 | Inactivity and absolute lifetime documented and justified | Partial | Summary #14 |
| V7.1.2 | 2 | Concurrent-session limit documented | Partial | L15 allows unlimited devices; no stated decision (summary #14) |
| V7.1.3 | 2 | Federated session coordination documented | Not applicable | No federation |
| V7.2.1 | 1 | Tokens verified by the backend | Covered | T1, T2, T15, W2 |
| V7.2.2 | 1 | Dynamic tokens, not static keys | Covered | L1, F1 |
| V7.2.3 | 1 | Reference tokens unique, secure random, 128+ bits | Partial | Unique: T8. 512-bit random value is code only (summary #22) |
| V7.2.4 | 1 | New token on login, old one ended | Covered | L20 |
| V7.3.1 | 2 | Inactivity timeout | Partial | 30 days without refreshing (T17), no risk decision (summary #14) |
| V7.3.2 | 2 | Absolute maximum session lifetime | Missing | Summary #2 |
| V7.4.1 | 1 | Ended session cannot be used | Partial | Refresh side proven (O2, O3, O5). Access token still works up to 15 min (summary #6) |
| V7.4.2 | 1 | All sessions end when an account is disabled or deleted | Partial | Refresh refused for deactivated users (F4); tokens deleted with the user (T12). Access token survives up to 15 min (summary #6) |
| V7.4.3 | 2 | Option to end other sessions after a password change | Covered | C5, P6 end every session automatically |
| V7.4.4 | 2 | Logout reachable | Covered | O2, O5 (backend side; the button is front-end) |
| V7.4.5 | 2 | Admins can end a user's or all users' sessions | Missing | Summary #10 |
| V7.5.1 | 2 | Re-authenticate before changing email, phone or MFA | Not applicable | No endpoint changes email, phone or MFA today. It applies as soon as one is added |
| V7.5.2 | 2 | View and end active sessions | Partial | Summary #11 |
| V7.6.1–V7.6.2 | 2 | Federated re-authentication; no silent session creation | Not applicable (7.6.1) / Covered (7.6.2) | No federation. Sessions start only from an explicit login (L1, L12) |

## V8 Authorization

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V8.1.1 | 1 | Function and data access rules documented | Covered | W5–W10, S3, T13, T14 |
| V8.1.2 | 2 | Field-level rules documented | Partial | Summary #21 |
| V8.2.1 | 1 | Function access needs explicit permission | Covered | T3, S1, C1, O4, W5–W9 |
| V8.2.2 | 1 | No direct object access to others' data (IDOR) | Covered | C6, F9, S3, S6, O2 |
| V8.2.3 | 2 | Field-level access enforced | Partial | Summary #21 |
| V8.3.1 | 1 | Enforced at a trusted layer | Covered | W11 (user and organization only from the token), T14 |
| V8.4.1 | 2 | No cross-tenant effects | Covered | O5, S3, S5, F5, T13, T14, P6 (audit per organization) |

## V9 Self-contained tokens

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V9.1.1 | 1 | Signature checked before use | Covered | T2 |
| V9.1.2 | 1 | Algorithm allowlist, never `none` | Covered | T26 |
| V9.1.3 | 1 | Keys only from trusted configuration | Covered | T22 |
| V9.2.1 | 1 | `exp` and `nbf` enforced | Covered | T21 |
| V9.2.2 | 2 | Right token type for the purpose | Not applicable | The only JWT issued is the access token. Refresh tokens are random values checked against the database, never accepted as access tokens |
| V9.2.3 | 2 | Audience checked | Covered | T2 |
| V9.2.4 | 2 | Distinct audiences when one key serves several | Not applicable | One audience |

## V10 OAuth and OIDC

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V10.1–V10.7 | 1–2 | OAuth client, resource server, authorization server, OIDC, consent | Not applicable | No OAuth or OpenID Connect. Two ideas still apply to our own refresh tokens: replay protection (V10.4.5) is covered by F3, and absolute expiry (V10.4.8) is summary #2 |

## Other chapters with Identity behaviour

| ASVS | L | Requirement (short) | Status | Evidence or gap |
|---|---|---|---|---|
| V2.3.1 | 1 | Steps happen in order | Covered | L5, L12 (no login before verification), P1 (no reset for unverified) |
| V2.4.1 | 2 | Anti-automation | Covered | R14, V10, RS5, L16, F8, O6, P13, C10, T18 |
| V3.3.1 | 1 | Cookie `Secure` and `__Secure-` or `__Host-` prefix | Partial | Summary #17 |
| V3.3.2 | 2 | Cookie `SameSite` fits its purpose | Covered | L2 (Strict) |
| V3.3.3 | 2 | `__Host-` prefix unless shared | Partial | Summary #17 |
| V3.3.4 | 2 | Session cookie `HttpOnly`, set only by `Set-Cookie` | Covered | L1, L2 |
| V4.1.3 | 2 | Proxy headers cannot be forged by users | Covered | L17 |
| V11.4.1 | 1 | Only approved hash functions | Covered | T25 |
| V11.4.2 | 2 | Passwords stored with a slow password hash | Covered | R13, L6, T7 (bcrypt, work factor 12) |
| V11.4.3 | 2 | Hash output 256+ bits where collisions matter | Covered | T6 (64 hex characters) |
| V11.4.4 | 2 | Key derivation from passwords | Not applicable | No keys are derived from passwords |
| V11.5.1 | 2 | Secure random values with 128+ bits | Partial | Summary #22 (6-digit codes fall under V6.5.4 instead) |
| V13.3.1 | 2 | Secrets in a vault, not in source | Covered | W17 (Key Vault), T4, T5 (placeholder refused in production) |
| V13.3.2 | 2 | Least privilege on secrets | Not applicable | Deployment and vault access policy, outside what the app's tests can prove |
| V14.2.1 | 1 | No sensitive data in URLs | Covered | T23 |
| V16.2.5 | 2 | Sensitive data kept out of logs | Partial | Summary #19 |
| V16.3.1 | 2 | All authentication attempts logged | Missing | Summary #5 |
| V16.3.2 | 2 | Failed authorization logged | Missing | Summary #9 |
| V16.3.3 | 2 | Security events and bypass attempts logged | Partial | Summary #13 |
| V16.3.4 | 2 | Unexpected errors logged | Covered | RS6, P12, C9, SH18 |
| V16.5.1 | 2 | Generic error messages | Covered | SH18, W2, L5, V3, P7 |
| V16.5.3 | 2 | Fails securely, never open | Covered | L12, P7, P9, C2 (nothing changes on refusal) |

## Decisions (2026-10-16)

| Summary # | Decision | Where |
|---|---|---|
| 1 | Build MFA after the MVP; required for Owner and Admin | `backlog/features/two-factor-auth.md` |
| 2, 14 | Sessions end after 7 days unused or 30 days after login | `backlog/improvements/2026-10-16-session-inactivity-and-absolute-limit.md` |
| 3, 4, 20 | Check breached passwords (register, reset, change) and product words | `backlog/improvements/breached-passwords.md` |
| 5, 9, 13 | Log logins, refused access and lockouts | `backlog/features/audit-log.md` |
| 6 | Access tokens revocable before expiry | `backlog/improvements/session-redesign.md` |
| 7 | Lock per IP address; higher account-wide limit; email on lock; no CAPTCHA | `backlog/improvements/2026-10-16-login-lockout-per-ip-address.md` |
| 8 | Emailed codes expire after 10 minutes | `backlog/improvements/2026-10-16-email-codes-expire-in-10-minutes.md` |
| 10 | Owner or Admin removes a member or signs them out, per organization | `backlog/features/2026-10-16-remove-member-and-end-sessions.md` |
| 11, 12, 16 | Accepted deviations: no session list, composition rules kept, 72-byte limit | Identity matrix header |
| 15, 17–19, 21–28 | Small fixes and test-only rows | `backlog/tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md` |
