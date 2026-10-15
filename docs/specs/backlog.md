# Backlog

Tasks that were agreed but don't have a spec yet. Write the spec with `/spec`, then remove the entry.

## Select organization
Module: Identity · Depends on: base-entity-and-audit-interceptor part 1 · Must ship before: base-entity-and-audit-interceptor part 2

Why. Part 1 adds the organization claim at login only for users with exactly one organization. Users with several get no claim, and once part 2 lands, every tenant save fails for them.

What was already decided (from Task 1):
- With several organizations, the user chooses one, and a select-organization endpoint issues a new token with that organization's claim (`AppClaimTypes.OrganizationId`).
- Refreshing the token keeps the same organization.
- Login reads user_roles across organizations to list the user's choices.

Still to decide in the spec: checking that the user belongs to the chosen organization, the response when they don't, and whether a user can switch organizations later.
