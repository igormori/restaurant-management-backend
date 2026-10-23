# Spec: Login lockout per IP address

**Type:** Fix   **Date:** 2026-10-23   **Requested by:** igormori

## Business Goals
- Nobody can keep a real user out of their account just by knowing their email.
- Attacks that spread guesses over many IP addresses (IP: the network address a request comes from) are still stopped.
- Users learn when their account was locked and are told how to regain control.

## Objective
Replace the single account-wide login lock with two limits: a per-IP-address lock and a higher whole-account lock. Email the user when the whole account is locked.

## Current State
- 5 wrong passwords for an account lock it for 15 minutes for everyone, wherever they sign in from.
- An attacker who knows an Owner's email can repeat 5 wrong passwords every 15 minutes and keep the Owner out all day.
- The user is not told that their account was locked.
- Resetting the password already clears the lock.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | The login lock is split into a per-IP-address lock and a whole-account lock, and a locked account triggers an email to its owner. |
| **How** should it behave? | See the Rules table. |
| **Who** is impacted? | Every user who signs in with email and password (Owner and all roles); attackers guessing passwords. |
| **Where** is the impact? | Identity module: login, account lock, emails sent to users. |

### Rules
| # | Rule |
|---|---|
| 1 | 5 wrong passwords for one account from one IP address lock that IP address out of that account for 15 minutes. The same account can still be signed in to from other addresses. |
| 2 | A number of wrong passwords for one account within a time window (shipped as 20 within 1 hour), from any mix of addresses, lock the whole account for 15 minutes for every address. One address adds at most 5 (its per-address limit) to that number within the window, so locking the whole account takes at least 4 different addresses. Number and window are configurable. |
| 3 | Every refused login, whatever the cause (wrong password, either lock, unknown email, unverified, deactivated), gets the same generic 401 answer. Nothing hints which lock applies. |
| 4 | While a lock applies, wrong passwords are not counted and the lock is not extended. A correct password during a lock is still refused. |
| 5 | A correct password clears the failed-attempt count for that account and address. Once a lock expires, a correct password signs in. |
| 6 | When the whole account becomes locked, the user gets one email: someone tried to sign in; if it was not them, reset the password. At most one such email is sent per account per 24 hours: a repeat whole-account lock within 24 hours of the previous lock email sends none. No email is sent when only an IP address is locked. |
| 7 | The email holds no code or link that unlocks the account. Only a password reset (which already clears the lock) does. |
| 8 | The IP address is determined by the same trusted-proxy rules as login rate limiting (an `X-Forwarded-For` header is trusted only from configured proxies). |
| 9 | The security notes of the Identity module are updated to describe the new limits. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | One address cannot lock others out | After 5 wrong passwords from address A, a correct password from address B signs in; from A it is refused for 15 minutes |
| 2 | Spread-out attacks are stopped | Wrong passwords from many addresses reaching the account limit refuse a correct password from every address for 15 minutes |
| 3 | No lock is revealed | Every refusal returns the same 401 body and status as a wrong password today |
| 4 | Lock email sent once | Whole-account lock sends exactly one email; per-IP lock sends none; email has no unlock code or link |
| 5 | Locked attempts are not counted | Wrong passwords during a lock leave counts and lock end unchanged |
| 6 | Address rules are shared | A forwarded address from an untrusted sender is ignored; from a trusted proxy it is used |
| 7 | Shipped limits match documentation | Shipped values are 5 per address, 20 per hour per account, 15-minute locks, as written in the Identity security notes |

## Out of Scope
| Item | Backlog file |
|---|---|
| Recording lockouts in the audit log (a feature the user decides on) | `/backlog/features/audit-log.md` (unchanged) |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/improvements/2026-10-16-login-lockout-per-ip-address.md` | Removed (fully solved) |

## Assumptions
| Type | Item |
|---|---|
| [Assumed] | Failed attempts from a locked address still count toward the whole-account limit only until that address is locked; attempts during any lock are not counted (Rule 4). |
| [Assumed] | The lock email follows the language rules of other emails the product sends; no new language setting is created. |
| [Assumed] | A whole-account lock more than 24 hours after the last lock email sends a new email; a repeat lock within 24 hours sends none (decided at Gate 2). |
