# Paginate the organization and location lists

**Found during:** docs/plans/organization/2026-10-15-organization-and-location-fields/scenarios.md
**Date:** 2026-10-15

## Description
`GET api/locations` and `GET api/organizations` return every row in one response, with no page size. The security skill requires list endpoints to be paginated with a hard maximum page size. The organization and location fields spec only adds fields to these responses, so pagination was left out of it.

## Why it matters
A restaurant group with many locations, or a user in many organizations, gets an unbounded response. Unbounded lists are an OWASP (Open Worldwide Application Security Project) API risk (unrestricted resource consumption) and slow the front end as data grows.
