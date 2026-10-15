# Plan: Hash verification codes

**Spec:** docs/specs/identity/2026-10-15-hash-verification-codes.md   **Date:** 2026-10-15   **Status:** Draft
**Risk:** High — renames a column on a table with data, runs a backfill, and changes how a security check works

## Summary
4 tasks in 3 batches. A new required server secret is validated when the app starts, including in the Docker `migrate` service. Codes are stored as an HMAC-SHA256 fingerprint (HMAC: Hash-based Message Authentication Code, a one-way fingerprint made with a secret key) and checked with a constant-time comparison. A migration then retires every code that was stored as readable text.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Verification hash key configuration and startup check | B1 | P1 | — | #8 |
| 2 | T2 | Verification code hasher | B2 | P1 | T1 | #1, #2, #3 |
| 3 | T3 | Store and check codes as fingerprints | B3 | P1 | T2 | #1, #2, #3, #4, #5, #6 |
| 4 | T4 | Retire existing readable codes (migration backfill) | B3 | P1 | T3 | #7 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New server secret `VerificationCode:HashKey`. The app and `migrate` refuse to start without it. It is never committed. It comes from `infra/docker/.env` (Docker), user-secrets (`dotnet run`) or Key Vault (deployed). The test factory supplies its own value. `docker-compose.yml` is already edited (uncommitted) |
| T2 | — | — | Cryptography: HMAC-SHA256 and constant-time comparison from the .NET (dotnet) base library |
| T3 | — | — | Changes the email verification check (auth flow). Column `code` is renamed to `code_hash` |
| T4 | — | — | Hand-written `migrationBuilder.Sql` backfill on a table with data. It marks all existing codes used and blanks their readable values |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [ ] T1 — Verification hash key configuration and startup check
- [ ] T2 — Verification code hasher
- [ ] T3 — Store and check codes as fingerprints
- [ ] T4 — Retire existing readable codes (migration backfill)
