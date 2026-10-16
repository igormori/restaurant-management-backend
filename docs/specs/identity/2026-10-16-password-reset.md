# Spec: Forgot and change password

**Type:** Feature   **Date:** 2026-10-16   **Requested by:** igormori

## Business Goals
- Users who forget their password can regain access without contacting support.
- Users can replace a password they fear is exposed.
- After a password changes, anyone who held the old password or an old login loses access.

## Objective
Let a user reset a forgotten password through an emailed code, and change a known password by proving the current one. Both end every existing login session of that user.

## Current State
- There is no way to reset a forgotten password or change a password. A user who forgets it is locked out for good.
- Users can log out and logins can be revoked per user family (see the refresh-token spec), but nothing ends all of a user's logins at once.
- Emailed 6-digit codes already exist for email verification, with expiry, an attempt limit and a resend cooldown.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Two new abilities: "forgot password" (request a code by email, then set a new password with it) and "change password" (logged-in user supplies the current and the new password). |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Any user with an account (Owner, Admin, Staff or custom role). Anonymous visitors can start "forgot password". |
| **Where** is the impact? | Identity module: account access flows, emailed codes, login sessions. |

### Rules
| # | Rule |
|---|---|
| 1 | Asking for a reset code gives the same response whether or not the email belongs to an account, so nobody can discover who has an account. |
| 2 | A reset code is single use, expires, and is rejected after too many wrong attempts, following the same limits as email verification codes. |
| 3 | Asking for a new code cancels earlier unused reset codes. Only the newest works. |
| 4 | Reset codes are stored only in protected form, like verification codes. |
| 5 | The new password must follow the same password rules as registration. |
| 6 | Change password requires the current password. A wrong current password is rejected and the password stays unchanged. |
| 7 | A successful reset or change ends all of the user's login sessions on every device. The user must log in again with the new password. |
| 8 | A new password equal to the current one is rejected on change. |
| 9 | Reset and change endpoints are rate limited like the other login-related endpoints. |
| 10 | Passwords and codes never appear in logs or responses. |
| 11 | Each successful reset and change is written to the audit log (who, when, which user), never recording any password value. |
| 12 | Only active users with a verified email can reset. Others get the same response as an unknown email, with no code sent. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Unknown email is not revealed | Responses for a known and an unknown email are identical |
| 2 | Valid code resets the password | The new password logs in; the old one no longer does |
| 3 | Code is single use | Using the same code twice fails the second time |
| 4 | Expired, wrong or over-attempted code is rejected | Each case is rejected and the password is unchanged |
| 5 | Only newest code works | After a second request, the first code is rejected |
| 6 | Stored code is not readable | The stored value differs from the emailed code |
| 7 | Weak new password is rejected | A password failing registration rules is refused on reset and change |
| 8 | Change needs current password | Wrong current password is rejected; correct one succeeds |
| 9 | All sessions end | After reset or change, every refresh token of the user is refused on every device |
| 10 | Rate limit applies | Excess requests receive a "too many requests" response |
| 11 | Actions are audited | Each success creates an audit entry without password data |
| 12 | Inactive or unverified user gets no code | No email is sent; the response matches an unknown email |

## Out of Scope
| Item | Backlog file |
|---|---|
| Ending already-issued short-lived access passes immediately (they last until they expire) | `/backlog/improvements/session-redesign.md` |
| Rejecting passwords found in public breaches | `/backlog/improvements/breached-passwords.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/features/password-reset.md` | Removed (fully solved) |
