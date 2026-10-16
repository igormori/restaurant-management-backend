# Vulnerable SQLitePCLRaw.lib.e_sqlite3 in test projects

**Status:** proposed
**Severity:** low
**Origin:** backlog/tech-debt/pkcs-dependency.md · found while resolving it
**Date:** 2026-10-16

## Problem
`SQLitePCLRaw.lib.e_sqlite3` 2.1.10 (High, GHSA-2m69-gcr7-jv3q) is pulled in transitively by the test projects. It is test-only and not shipped, but the vulnerable report is not clean.

## Evidence
- `dotnet list RestaurantManagement.sln package --vulnerable --include-transitive` lists it in the Identity, Menu, Organization and Shared test projects.

## Acceptance criteria
- [ ] `dotnet list RestaurantManagement.sln package --vulnerable --include-transitive` reports no vulnerable packages.

## Related
None
