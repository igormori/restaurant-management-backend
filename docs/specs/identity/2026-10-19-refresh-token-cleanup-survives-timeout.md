# Spec: Daily expired-session cleanup keeps running after a timeout

**Type:** Fix   **Date:** 2026-10-19   **Requested by:** igormori

## Business Goals
- The whole application stays up when the daily cleanup hits a slow or timed-out database call.
- Expired login sessions (refresh tokens: the long-lived keys that keep a user signed in) keep being removed every day.

## Objective
Make the daily cleanup of expired login sessions log a timeout and carry on, instead of taking the whole application down, while still stopping quietly when the application is shutting down.

## Current State
- A background job removes expired login sessions at startup and once a day.
- It already logs ordinary failures and tries again the next day.
- A cancellation that is not a shutdown (for example a database timeout) is not treated as a failure. It escapes the job and, by default, stops the whole application.
- The password-reset email job was already fixed for this; the cleanup job was missed.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | The cleanup job tells a real shutdown apart from any other cancellation. |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Everyone using the application (it no longer risks stopping); operators reading the logs. |
| **Where** is the impact? | Identity module: the daily expired-session cleanup job. |

### Rules
| # | Rule |
|---|---|
| 1 | A cancellation or timeout while the application is not shutting down is logged as an error and the job keeps running; the next daily run tries again. |
| 2 | A cancellation while the application is shutting down ends the job without an error log. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | A timeout does not stop the application | With the cleanup raising a timeout cancellation and no shutdown requested, an error is logged and the job is still running |
| 2 | Shutdown still ends the job cleanly | With shutdown requested during a run, the job ends and no error is logged |
| 3 | Both cases are proven and recorded | The Identity security matrix row for the cleanup (T9) or a new row names one passing test per case |

## Out of Scope
| Item | Backlog file |
|---|---|
| None | n/a |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/bugs/2026-10-19-refresh-token-cleanup-worker-stops-host-on-timeout.md` | Removed (fully solved) |
