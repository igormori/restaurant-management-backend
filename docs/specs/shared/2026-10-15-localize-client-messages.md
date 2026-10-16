# Spec: Localize All Client-Facing Messages

**Type:** Fix   **Date:** 2026-10-15   **Requested by:** Igor Mori

## Business Goals
- Brazilian users see every error in Portuguese, never English text or internal codes.
- Canadian and US users keep seeing clear English messages.
- A bad login token gives a clear "not authorized" answer, not a generic failure.

## Objective
Make every message the system sends to a client available in English (en) and Portuguese (pt), and answer an invalid user identifier in the token with a localized "unauthorized" response instead of a server error.

## Current State
Most messages are already translated, but these cases are not:
| Case | What the client gets today |
|---|---|
| "Menu not found" (Menu) | The raw code `MenuNotFound`, in both languages |
| Phone number format error (registration) | English text when Portuguese is requested |
| "Menu category not found" (Menu) | Fixed English text |
| Menu category create/edit field errors (Menu) | Fixed English text |
| Color format errors on organization create/edit (Organization) | Fixed English text |
| Required/length errors on login, organization selection, locations, organization name and description, menu create/edit | Default English text from the platform, whatever the language |
| Unexpected server error (Web) | Fixed English "An unexpected error occurred." |
| User identifier in the token is not valid (locations) | A 500 server error with the generic English message |

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Every case in Current State returns a translated message. An invalid user identifier returns a 401 (not authorized) response. |
| **How** should it behave? | The message language follows the language the client asks for. Each message exists in both en and pt. An unsupported language falls back to English, as today. |
| **Who** is impacted? | All API clients and end users; mostly Portuguese speakers. No role or permission changes. |
| **Where** is the impact? | Shared (message catalog), Menu, Organization, Identity (login and registration), Web (unexpected-error response, 401 handling). |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | "Menu not found" is readable | A request for a missing menu returns a readable message in en and in pt, never `MenuNotFound` |
| 2 | "Menu category not found" is translated | The same request in pt returns the Portuguese text; in en, the English text |
| 3 | Phone format error is translated | Registering with a bad phone number in pt returns Portuguese text |
| 4 | Menu category field errors are translated | Invalid create and edit requests in pt return Portuguese text |
| 5 | Organization color errors are translated | Invalid color on create and edit in pt returns Portuguese text |
| 6 | Previously untranslated field errors are translated | Invalid login, organization selection, location, organization and menu requests in pt return Portuguese text; in en, English text |
| 7 | Unexpected-error message is translated | A forced unexpected failure returns the 500 with Portuguese text in pt, English in en |
| 8 | Invalid user identifier gives 401 | A token whose user identifier is invalid gets status 401, not 500, with a translated message in en and pt |
| 9 | No message is missing in either language | Every message the system can send exists in both languages; a check fails if one is missing |

## Out of Scope
| Item | Backlog file |
|---|---|
| None found | n/a |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/bugs/2026-10-15-unlocalized-client-messages.md` | Removed (fully solved) |
| `/backlog/bugs/2026-10-15-menu-not-found-message-missing.md` | Removed (fully solved: same missing "menu not found" message, covered by outcome 1) |
