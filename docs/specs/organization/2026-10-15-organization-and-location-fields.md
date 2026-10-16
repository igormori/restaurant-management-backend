# Spec: Organization and location fields

**Type:** Feature   **Date:** 2026-10-15   **Requested by:** igormori
**Source:** Phase 0 — Task 7   **Size:** S   **Depends on:** Task 1 (base entity)

> **No live data:** the app is not launched and the database holds no real data. Fields and
> tables may be changed freely; no data migration or backfill is needed.

## Business Goals
- Launch in Brazil, Canada and the USA (United States of America) from day one, with each restaurant using its own currency and local time.
- Close each location's cash on the right business day, even when service runs past midnight.
- Tell stores apart from the central kitchen, and apply each restaurant's service charge and discount approval limit consistently.

## Objective
Add currency, time zone, location type and code, and the service charge, discount and business-day settings that Phase 1 needs, with no country-specific defaults.

## Current State
- Organizations have no currency and locations have no time zone.
- Locations have no type (store or central kitchen) and no short code.
- Settings have no service charge rate, discount approval limit or business-day cutoff.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Currency on organizations, time zone, type and code on locations, new settings, a way to compute a location's business day, and these fields in the organization and location endpoints. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | Owners and Admins creating and configuring organizations and locations. |
| **Where** is the impact? | Organization module: organizations, locations, settings and their endpoints. |

### Rules
| # | Rule |
|---|---|
| 1 | Each organization has one currency, chosen when it is created: BRL (Brazilian real), USD (US dollar) or CAD (Canadian dollar). There is no default currency. |
| 2 | Each location has a time zone, chosen when it is created. It must be a valid IANA (Internet Assigned Numbers Authority) time zone name, for example America/Sao_Paulo, America/Toronto or America/New_York. There is no default time zone. |
| 3 | Each location has a type: Store or Central kitchen (default Store). |
| 4 | Each location has a code of up to 10 characters, unique within its organization. Two organizations may use the same code. |
| 5 | Settings have a service charge rate (default 0, from 0 to 100), a discount approval limit (a percentage, default 0, from 0 to 100: every discount needs approval until an Owner or Admin raises it), and a business-day cutoff time (default 04:00). Existing plan and trial fields are kept. |
| 6 | A location's business day for a given moment uses that location's time zone and the cutoff: a sale before the cutoff belongs to the previous day. |
| 7 | Missing or invalid values are rejected with a validation message. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Currency is required | Creating an organization without a currency, or with one other than BRL, USD or CAD, is rejected |
| 2 | Time zone is required and valid | Creating a location without a time zone, or with an invalid one, is rejected |
| 3 | Defaults are set | A new organization gets service charge 0, discount approval limit 0 and cutoff 04:00; a new location gets type Store |
| 4 | Code unique per organization | Creating a second "MM" in Org A is rejected |
| 5 | Code reusable across organizations | Creating "MM" in Org B is allowed |
| 6 | Service charge validated | A rate of 120 is rejected (must be 0 to 100) |
| 7 | Business day is correct in each country | With cutoff 04:00, a sale at 02:30 local on 15 Oct belongs to 14 Oct, for a location in America/Sao_Paulo and for one in America/Toronto |

## Open Questions
[Decided]
- Discount approval limit defaults to 0, so every discount needs approval until an Owner or Admin raises it (igormori, 2026-10-15).

[Assumed]
- The discount approval limit is a percentage of the sale (0 to 100), like the service charge rate, so it does not depend on the currency.
- Currency is set per organization and time zone per location — Canada and the USA span several time zones, so one restaurant group can have locations in different zones; a group sells in one currency.
- Service charge defaults to 0 — it is common in Brazil (usually 10%) but not in Canada or the USA, so each organization sets its own.

## Out of Scope
| Item | Backlog file |
|---|---|
| Taxes per country or region | Planned for a later phase (no backlog file) |
