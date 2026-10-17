# Plan: Sign-in token carries roles only for the organization in use

**Spec:** docs/specs/identity/2026-10-16-token-roles-only-for-selected-organization.md   **Date:** 2026-10-16   **Status:** Draft
**Risk:** High — changes which role claims the access token carries (auth change); no schema, data or contract change

## Summary
1 task in 1 batch. The access token builder (`AccessTokenFactory`) stops adding every role of every
organization when no organization is chosen: it adds roles only for the chosen organization, or for
the single organization a user belongs to. Sign-in and token refresh both build tokens through it,
so one change covers both.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Token roles only for the organization in the token | B1 | P1 | — | #1, #2, #3, #4, #5 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Auth change: role claims in the access token (JWT: JSON Web Token) narrowed; Identity matrix rows L14 and F6 changed, L21 and F10 added |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [ ] T1 — Token roles only for the organization in the token
