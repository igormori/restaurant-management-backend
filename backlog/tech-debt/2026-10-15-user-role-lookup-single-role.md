# UserRoleLookup returns one arbitrary role per organization

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Description
`UserRoleLookup.GetRoleAsync` returns the first role row it finds for a user in an organization. With location-specific assignments, a user can hold several rows in one organization (for example Manager for all locations and Waiter for one location), and the result depends on row order.

## Why it matters
Menu, Location and Organization services decide access from this single value, so the same user can be allowed or refused depending on which row the database returns first. The lookup should return all roles (or the permissions they grant) for the organization.
