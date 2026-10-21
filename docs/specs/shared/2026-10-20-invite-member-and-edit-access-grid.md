# Spec: Invite a member and edit their access grid

**Type:** Feature   **Date:** 2026-10-20   **Requested by:** Igor Mori

Slice 2 of 3 of the per-location access grid (`docs/specs/shared/2026-10-19-per-location-access-grid.md`). It uses the grid, roles and delegation rules decided there.

## Business Goals
- Let an organization bring staff in and decide what each person may do at each location.
- Let owners delegate this safely, without anyone gaining more power than they hold.

## Objective
An authorized member invites a person by email with a chosen grid, and can later edit any member's grid, under the delegation rules, with every change audited.

## Current State
- A member's grid can be stored, but no screen or endpoint lets anyone invite a person or set or edit a grid.
- The delegation rules are decided but not enforced.
- A member's role cannot be changed (for example Waiter to Cashier).

## Future State
| Question | Answer |
|---|---|
| **What** will change? | A member can be invited by email with presets and a grid, and an existing member's presets and grid can be edited. |
| **How** should it behave? | See the Rules tables below. |
| **Who** is impacted? | Owner and Admins with Members = Manage (they invite and edit); invited people and edited members. |
| **Where** is the impact? | Identity (invitations, members, access), Shared (audit log, email sending). |

**Invitation**
| Rule | Detail |
|---|---|
| Content | Email address, one or more role presets and the final grid (location x module -> level). Presets fill the grid by the higher level per cell; any cell can then be changed |
| Acceptance | The invitee receives an email link and joins only after accepting. Until then they have no access. The link expires after 7 days and works once |
| Account | A person who already has an account joins with it; otherwise they create one while accepting. Their other organizations are untouched |
| Duplicates | Inviting someone already in the organization is refused. A new invitation to the same pending email replaces the old one |
| Cancel | An authorized member can cancel a pending invitation |
| Owner | An invitation cannot make anyone an Owner |

**Invitation email**
| Part | Content |
|---|---|
| Subject | "You are invited to join <organization name> on <product name>" |
| Body | Who invited them (name), the organization name, the roles offered per location (e.g. "Waiter at Centro"), a button with the accept link, and the link's expiry date |
| Safety text | A line saying to ignore the email if it was not expected. It never shows the full grid or any password |
| Language | The invitee's language when known, otherwise the inviter's |
| Owned by | Identity: the email belongs to the module that sends it |

**Editing a grid**
| Rule | Detail |
|---|---|
| Edit | Presets and individual cells of a member can be changed. Role labels are stored with the grid as decided in slice 1 |
| Effect | Applies on the member's next request, with no new login |
| Audit | Each invitation, cancellation and grid change records who, when, before and after |

**Delegation (who may do what)**
| Rule | Detail |
|---|---|
| Who | The Owner, or an Admin with Members = Manage, at the locations involved |
| Cap | Nobody can grant a level above their own at that location and module |
| Self | Nobody can change their own grid |
| Owner | The Owner's access can never be edited |
| Tenancy | Only members of the caller's own organization can be invited or edited; another organization's member is reported as not found |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Invite works | An authorized member invites an email; the person receives a link, accepts, and has exactly the chosen grid |
| 2 | No access before acceptance | A pending invitee is refused everywhere |
| 3 | Link is limited | An expired or already used link is refused |
| 4 | Duplicates handled | Inviting a current member is refused; re-inviting a pending email leaves one valid invitation |
| 5 | Grid edit applies at once | After a level is lowered, the member's next request is refused without a new login |
| 6 | Role change possible | Changing a member from Waiter to Cashier updates the grid and label and writes one audit entry with Before = Waiter and After = Cashier |
| 7 | Cap enforced | An Admin cannot grant a level above their own at a location; the request is refused and nothing is saved |
| 8 | No self-edit | A member editing their own grid is refused |
| 9 | Owner protected | Editing the Owner, or inviting someone as Owner, is refused |
| 10 | Members level required | A member without Members = Manage at the location is refused |
| 11 | Tenant isolation holds | Editing a member of another organization returns not found |
| 11a | Email is complete | The invitation email contains the inviter, organization, roles per location, accept button and expiry date, and nothing else about access |
| 12 | Changes are audited | Invitation, cancellation and every grid change appear in the audit log with who, when, before and after |

## Out of Scope
| Item | Backlog file |
|---|---|
| Making someone an Owner or removing that role; protecting the last Owner | `/backlog/features/2026-10-16-member-role-change.md` |
| Removing a member or ending their sessions | `/backlog/features/2026-10-16-remove-member-and-end-sessions.md` |
| Creating, editing and renaming roles | `/backlog/features/2026-10-15-custom-roles.md` |
| Filtering lists by location access | `/backlog/features/2026-10-19-filter-lists-by-location-access.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/features/2026-10-19-invite-member-and-edit-access-grid.md` | Removed (fully solved) |
| `/backlog/features/2026-10-16-member-role-change.md` | Updated (preset and grid change solved here; Owner role changes and last-Owner protection stay) |
