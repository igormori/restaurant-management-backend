# Plan: Invite a member and edit their access grid

**Spec:** docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md   **Date:** 2026-10-20   **Status:** In progress
**Risk:** High — new public endpoint that creates accounts and memberships, new authorization (delegation) rules, a Shared contract change and new Identity tables (no data, pre-launch)

## Summary
8 tasks in 5 batches. Shared foundations come first (location names in the lookup, new audit actions and messages). Identity then gains one set of delegation rules (who, cap, self, Owner) and one grid builder, used by the member-access edit endpoint and by invitations; invitations are stored with their final grid, emailed, accepted with a one-time link, or cancelled.

About 35 files, over the 10-file split guide in the root `CLAUDE.md`. If the architect prefers two pull requests, split after B3 (edit a member's grid) and B4–B5 (invitations); B4 depends on B3's rules, not the reverse.

Terms: DoD (Definition of Done: the checks a task must pass to count as finished); DTO (Data Transfer Object: a plain class passed between modules); grid (per member: location x module -> None, Read or Manage); preset (a stored role whose default levels fill a grid); cap (nobody grants a level above their own at that location and module); SQL (Structured Query Language).

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Location lookup returns the location name | B1 | P1 | — | #11a |
| 2 | T2 | Audit actions and messages for invitations and delegation | B2 | P1 | — | #12 |
| 3 | T3 | Delegation rules and shared grid builder | B3 | P1 | T2 | #7, #8, #9, #10 |
| 4 | T4 | Edit a member's access at a location | B3 | P1 | T2, T3 | #5, #6, #7, #8, #9, #10, #11, #12 |
| 5 | T5 | Create an invitation | B4 | P1 | T2, T3 | #4, #7, #9, #10, #11, #12 |
| 6 | T6 | Invitation email | B4 | P1 | T1, T5 | #1, #11a |
| 7 | T7 | Accept an invitation | B5 | P1 | T5, T6 | #1, #2, #3 |
| 8 | T8 | Cancel a pending invitation | B5 | P2 | T2, T5 | #11, #12 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Shared contract change (`LocationSummaryDto` gains the name) |
| T2 | — | — | Shared: new audited actions |
| T3 | — | — | Authorization: delegation rules (Members level, cap, self, Owner) |
| T4 | — | — | New signed-in endpoint; tenant data writes |
| T5 | — | — | New signed-in endpoint; migration adds invitation tables; raw SQL filter on a partial unique index; one-time token stored only as a fingerprint |
| T6 | — | — | Email carries a one-time link; new required setting (accept page address) |
| T7 | — | — | Public endpoint that creates a verified account; memberships written into another organization than the token's (tenant scope) |
| T8 | — | — | New signed-in endpoint; tenant data writes |

Email uses the existing email service; no new library, no backfill (pre-launch, root `CLAUDE.md`).

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] `python3 .claude/scripts/check-security-matrices.py` passes
- [ ] Spec Backlog Impact kept: the deletion of `backlog/features/2026-10-19-invite-member-and-edit-access-grid.md` stays uncommitted for the user

## Checklist
- [x] T1 — Location lookup returns the location name
- [x] T2 — Audit actions and messages for invitations and delegation
- [x] T3 — Delegation rules and shared grid builder
- [x] T4 — Edit a member's access at a location
- [x] T5 — Create an invitation
- [x] T6 — Invitation email
- [ ] T7 — Accept an invitation
- [ ] T8 — Cancel a pending invitation
