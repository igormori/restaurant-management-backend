# Spec: Hash verification codes

**Type:** Fix   **Date:** 2026-10-15   **Requested by:** igormori

## Business Goals
- A leaked copy of the database must not give an attacker working email verification codes.
- Protect user accounts from being verified by someone who does not own the email address.

## Objective
Store email verification codes only in protected form, so the real code cannot be read from the database, while keeping the verification flow unchanged for users.

## Current State
- Each emailed 6-digit verification code is saved in the database as readable text. Anyone with database access can read valid codes.
- A plain one-way fingerprint (hash) would not help: there are only one million possible 6-digit codes, so an attacker could try them all and match a leaked fingerprint in seconds.
- A newly sent code does not explicitly cancel earlier unused codes for the same user.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Codes are stored only as a keyed one-way fingerprint. The key is a server secret kept outside the database, so a leaked fingerprint cannot be matched by trying all million codes. The readable code is no longer stored. |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Users verifying their email. Users holding an unused code at release time must request a new one. |
| **Where** is the impact? | Identity module: email verification and resend flows. |

### Rules
| # | Rule |
|---|---|
| 1 | The database never holds the readable code. |
| 2 | The submitted code is checked by recomputing its fingerprint with the server secret and comparing in a way that does not reveal, through response time, how close a guess was. |
| 3 | Sending a new code marks all earlier unused codes of that user as used. Only the newest code works. |
| 4 | A correct, unused, unexpired code verifies the user and is marked used. |
| 5 | Existing rules stay as they are: failed-attempt counting, temporary lock after the attempt limit, code expiry, resend cooldown. |
| 6 | At release, all codes already stored are marked used. Affected users request a new code. |
| 7 | The server secret is read from configuration and is never committed to the repository. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Stored code is not readable | After a code is sent, the stored value differs from the emailed code |
| 2 | Valid code verifies | Submitting a valid code verifies the user and marks the code used |
| 3 | Wrong code is counted | A wrong code is rejected and the failed-attempt count rises by 1 |
| 4 | Lock still works | After the attempt limit, any code is rejected until the lock time passes |
| 5 | Expired or used code rejected | Submitting either is rejected |
| 6 | Only newest code works | After a resend, the first code is rejected and the newest is accepted |
| 7 | Old codes retired | After release, no pre-existing code can verify a user |
| 8 | Missing secret is caught | The application refuses to start, or refuses to send codes, when the secret is not configured |

## Out of Scope
| Item | Backlog file |
|---|---|
| None found | None |

## Backlog Impact
| Backlog file | Result |
|---|---|
| None | `/backlog/features/password-reset.md` mentions reusing the code pattern but is not solved by this spec; left unchanged |
