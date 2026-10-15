# Spec: Refresh tokens in their own table

**Type:** Feature   **Date:** 2026-10-15   **Requested by:** igormori

## Business Goals
- Staff can use several devices (tablet, laptop) at once without signing each other out.
- A stolen "stay signed in" token is detected and cut off, instead of working silently for 30 days.
- One lost or shared device can be signed out without touching the user's other devices.

## Objective
Give every sign-in its own stored "stay signed in" token (refresh token: the long-lived secret that gets a new short-lived access pass without typing the password), so tokens are replaced on every use, reuse is detected, and each device can be revoked alone.

## Current State
- Each user holds a single refresh token. Signing in on a second device replaces it and signs out the first.
- A token cannot be replaced on use, reuse of an old token is not detected, and one device cannot be revoked alone.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Every sign-in creates its own token record, tied to the user (and organization when known), with expiry, revocation and replacement history, device description (browser/app) and network address. Tokens form a "family": all tokens descending from one sign-in. The single token on the user account is removed. |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | All users of the system (Owner, Admin, Staff, custom roles). Every user must sign in once again after release. |
| **Where** is the impact? | Identity module: sign-in, token refresh, logout, new "sign out everywhere" action. |

### Rules
| # | Rule |
|---|---|
| 1 | Only a protected one-way fingerprint of the token is stored. The real token is never readable from the database. |
| 2 | The token is delivered to the client in a cookie that scripts cannot read, is sent only over secure connections and never on requests from other sites. |
| 3 | Each sign-in starts a new family. Several families (devices) can be active for one user at the same time. |
| 4 | Refreshing with an active token revokes it, issues a replacement in the same family and returns a new access pass. |
| 5 | Using a token that was already revoked is treated as theft: the whole family is revoked and the request is refused. |
| 6 | Expired, unknown or revoked tokens are refused with a generic "unauthorized" answer. |
| 7 | Logout revokes only the current device's token. "Sign out everywhere" revokes every token of that user. |
| 8 | Deleting a user removes their tokens. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Sign-in creates one active token, delivered in a cookie unreadable by scripts, secure-only and same-site-only | Check cookie attributes and one active record |
| 2 | Two devices signed in can both refresh independently | Both refreshes succeed |
| 3 | Refreshing an active token rotates it | Old token revoked; new token active in the same family |
| 4 | Reusing an already rotated token is refused and revokes its family | Unauthorized answer; every token in the family revoked |
| 5 | An expired token is refused | Unauthorized answer |
| 6 | Logout on one of two active devices | Only that device's token is revoked |
| 7 | Stored fingerprint differs from the real token | Stored value is not equal to the issued token |
| 8 | "Sign out everywhere" | All the user's tokens revoked |
| 9 | Release | The old single-token fields no longer exist; users must sign in again once |

## Out of Scope
| Item | Backlog file |
|---|---|
| Revoking short-lived access passes when password, role or status changes | `/backlog/improvements/session-redesign.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/improvements/session-redesign.md` | Updated (partially solved) |
