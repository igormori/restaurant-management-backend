# Spec: Pending-email queue gets neutral names

**Type:** Fix   **Date:** 2026-10-19   **Requested by:** igormori

## Business Goals
- Anyone reading the system (developers, operators, reviewers) understands that the queue sends every kind of pending email, not only password reset emails.
- The rename happens before launch, while it costs nothing and no real data has to be kept.

## Objective
Rename the queue of pending emails, and everything that carries its old password-reset name, to neutral names. Behaviour stays exactly the same.

## Current State
- The queue of pending emails now holds password reset emails and verification emails (resend verification uses it too).
- Its stored data, the background job that sends the emails, the on/off and interval settings, and the security rules and tests that describe it are all still named after password reset.
- A reader assumes the queue only handles password reset, and may misjudge what a change to it affects.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | The queue's stored data, its sending job, its settings and the security rules and tests that refer to them get neutral names, for example "pending emails". |
| **How** should it behave? | Exactly as today: same emails, same timing, same retries, same answers to users. Only names change. |
| **Who** is impacted? | Developers and operators. The operators' on/off and interval settings for the sending job get new names and must be set under them. End users see no change. |
| **Where** is the impact? | Identity module (queue, sending job, security rules and tests) and the application's startup settings. |

### Rules
| # | Rule |
|---|---|
| 1 | No name tied to one kind of email remains on the shared queue, its sending job or its settings. |
| 2 | Names that are truly about password reset (the reset request itself, reset codes) keep their names. |
| 3 | The database holds no real data, so the stored data is renamed with no copying or preservation. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | The queue, sending job and settings have neutral names | Searching the code and configuration for the old password-reset queue names finds nothing |
| 2 | Password reset and resend verification emails still go out and retry as before | The existing tests for both flows pass without any change to what they check |
| 3 | The security rules list matches the new names | The security-rules consistency check passes |
| 4 | The full build and test suite pass | Build and tests succeed |

## Out of Scope
| Item | Backlog file |
|---|---|
| Any change to what emails are sent, when, or how | None |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/tech-debt/2026-10-19-pending-email-queue-has-password-reset-names.md` | Removed (fully solved) |

## Gaps
None.
