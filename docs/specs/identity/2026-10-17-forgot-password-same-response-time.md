# Spec: Forgot password takes the same time for every email

**Type:** Fix   **Date:** 2026-10-17   **Requested by:** igormori

## Business Goals
- Nobody can find out which emails have an account by timing the "forgot password" request.
- A reset email that fails to send is not lost.
- Closes a critical launch blocker.

## Objective
Make "forgot password" answer in the same time for every email, known or not, and deliver the reset email separately from the request, retrying if delivery fails.

## Current State
- "Forgot password" returns the same text for every email (see the password reset spec, rule 1), but not in the same time.
- For an email with an eligible account (active user, verified email), the request waits to store the reset code and to send the email. For an unknown, inactive, unverified or locked account, it answers at once.
- Anyone who measures the difference learns which emails have accounts.
- If the email fails to send today, the failure happens during the request and no later retry exists.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | The request no longer waits for the email to be sent. Every email causes the same work during the request, and the reset email is delivered afterwards from a queue of pending emails. |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Anonymous visitors using "forgot password"; users who receive reset emails. |
| **Where** is the impact? | Identity module: forgot-password flow and reset email delivery. |

### Rules
| # | Rule |
|---|---|
| 1 | The response to "forgot password" never depends on sending the email. |
| 2 | The request does the same amount of work for a known eligible email and for any other email, so the response times cannot be told apart. |
| 3 | A reset email that fails to send is retried later and is not lost. |
| 4 | All existing reset rules stay unchanged (same response text, single-use code, expiry, attempt limit, only the newest code works, eligibility). |
| 5 | A reset email is never sent for an unknown, inactive or unverified account. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Response does not wait for email delivery | With email sending slowed or failing, the response time stays the same |
| 2 | Known and unknown emails cannot be told apart by time | Over many requests, the response times for a known eligible email and an unknown email show no measurable difference |
| 3 | A failed delivery is not lost | After a simulated sending failure, the email is sent on a later retry |
| 4 | Eligible email still gets its code | A known eligible email receives exactly one reset email, and the code works |
| 5 | Other emails get no email | Unknown, inactive and unverified emails cause no email to be sent |

## Out of Scope
| Item | Backlog file |
|---|---|
| Saving audit entries in the same pending-work queue | `/backlog/improvements/2026-10-16-audit-entries-through-outbox.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/bugs/2026-10-16-forgot-password-response-time-reveals-account.md` | Removed (fully solved) |
