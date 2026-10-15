# Plan: Refresh tokens in their own table

**Spec:** docs/specs/identity/2026-10-15-refresh-tokens-own-table.md   **Date:** 2026-10-15   **Status:** Draft
**Risk:** High — new table plus dropping two columns from `users` (which has data), auth flow and API (Application Programming Interface) contract change, new public endpoint, CORS (Cross-Origin Resource Sharing) change

## Summary
5 tasks in 4 batches. Add a `refresh_tokens` table and a token service (issue, rotate, detect reuse, revoke), then move
sign-in, refresh and organization selection onto it with an HttpOnly cookie. After that, add logout and "sign out
everywhere", and finally drop the old single-token columns from `users`.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Refresh token table and entity | B1 | P1 | — | #1, #7 (Rule 8) |
| 2 | T2 | Refresh token service: issue, rotate, reuse detection, revoke | B2 | P1 | T1 | #2, #3, #4, #5, #7, #8 |
| 3 | T3 | Sign-in, refresh and organization selection use per-device tokens in a cookie | B3 | P1 | T2 | #1, #2, #3, #4, #5 |
| 4 | T4 | Logout (this device) and sign out everywhere endpoints | B3 | P2 | T3 | #6, #8 |
| 5 | T5 | Drop the old single-token columns from users | B4 | P1 | T3, T4 | #9 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New table with a cascade-delete foreign key to `users`; stores IP (Internet Protocol) address and device description (personal data) |
| T2 | — | — | Token fingerprint is unkeyed SHA-256 (Secure Hash Algorithm, 256-bit); reuse revokes the whole family |
| T3 | — | — | Auth change: refresh reads only the cookie (no email in body); token removed from response body; CORS adds `AllowCredentials` |
| T4 | — | — | New public endpoint `POST api/auth/logout` (cookie only, no access token); new authenticated `POST api/auth/logout-all` |
| T5 | — | — | Migration drops `users.refresh_token_hash` and `users.refresh_token_expiry` (data loss by design: every user signs in again) |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [ ] T1 — Refresh token table and entity
- [ ] T2 — Refresh token service: issue, rotate, reuse detection, revoke
- [ ] T3 — Sign-in, refresh and organization selection use per-device tokens in a cookie
- [ ] T4 — Logout (this device) and sign out everywhere endpoints
- [ ] T5 — Drop the old single-token columns from users
