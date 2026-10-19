# Spec: Resend verification answers every email the same way

**Type:** Fix   **Date:** 2026-10-19   **Requested by:** igormori

## Business Goals
- Nobody can find out which emails belong to unverified accounts by using "resend verification".
- A verification email that fails to send is not lost.
- Closes a critical launch blocker, the same kind of leak already closed for "forgot password".

## Objective
Make "resend verification" return the same answer, in the same time, for every email and every account state, and deliver the verification email separately from the request, retrying if delivery fails.

## Current State
- An unverified account gets a different message ("code resent") from every other case (unknown email, verified account, locked account, or within the 60-second wait), which all get one shared acknowledgement.
- For an unverified account the request also waits for the email to be sent, so it takes longer.
- If sending fails, the request returns a server error (500) and the email is not retried. No other case can produce this error.
- Anyone comparing the message, the time or the error learns which emails are unverified accounts.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Every request gets the same status and message. The request no longer waits for the email: it is delivered afterwards from the queue of pending emails already used by "forgot password". |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Anonymous visitors using "resend verification"; users waiting for a verification code. |
| **Where** is the impact? | Identity module: resend-verification flow and verification email delivery. |

### Rules
| # | Rule |
|---|---|
| 1 | The response (status and message) is identical for every email, whatever the account state or the email outcome. |
| 2 | The response never depends on sending the email, and a sending failure never reaches the requester. |
| 3 | The request does the same amount of work for every email, so response times cannot be told apart. |
| 4 | A verification email that fails to send is retried later and is not lost. |
| 5 | A verification email is sent only for an unverified, unlocked account outside the 60-second wait. |
| 6 | Existing resend rules stay unchanged: older codes cancelled, new code stored only as a fingerprint, failed-attempt counter reset, lock expiry, rate limit (429 Too Many Requests). |
| 7 | A sending failure is still logged with the user ID and never the email address. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | All emails get the same answer | Unknown, verified, locked, within-wait and eligible unverified emails return the same status and body |
| 2 | Response does not wait for email delivery | With sending slowed or failing, the response and its time stay the same |
| 3 | Emails cannot be told apart by time | Over many requests, an eligible unverified email and an unknown email show no measurable time difference |
| 4 | A failed delivery is not lost | After a simulated sending failure, the email is sent on a later retry |
| 5 | Eligible account still gets its code | An eligible unverified email receives exactly one email and the code verifies the account |
| 6 | Other accounts get no email | Unknown, verified, locked and within-wait emails cause no email |
| 7 | Security matrix rows RS1 to RS6 describe the new behaviour | Each row is updated and names a passing test |

## Out of Scope
| Item | Backlog file |
|---|---|
| Saving audit entries in the same pending-work queue | `/backlog/improvements/2026-10-16-audit-entries-through-outbox.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/bugs/2026-10-17-resend-verification-reveals-unverified-account.md` | Removed (fully solved) |
