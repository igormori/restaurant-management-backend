---
name: security
description: Security rules for the RestaurantManagement backend. Use when writing, planning, testing or reviewing any endpoint, query, authentication or authorization code, migration, configuration, logging, or anything that handles user input, tenant data, or secrets.
---

# Security rules

Applies to every module. A violation is a Critical finding in review, never a style note.
Based on the OWASP (Open Worldwide Application Security Project) API Security Top 10.

## 1. Multi-tenancy (broken object-level authorization)
- Every query on tenant data filters by `OrganizationId` taken from the JWT (JSON Web Token)
  claims, never from the request body, route, or query string.
- A lookup by `Id` alone is a bug: `Where(x => x.Id == id && x.OrganizationId == orgId)`.
- Another organization's record returns not found (`null` → 404), never 403. Do not reveal
  that it exists.
- Every feature with tenant data has a test proving organization A cannot read or change
  organization B's data.

## 2. Authorization (broken function-level authorization)
- Every controller action declares the permission it needs through an authorization policy
  (`[Authorize(Policy = ...)]`), never a role name. No action relies on a default. Existing
  actions that check role names are legacy: don't copy them.
- `[AllowAnonymous]` only on login, registration and health checks. Anything else needs a
  flag in the plan.
- Each role the spec disallows has a test proving it is rejected.
- A user can never grant themselves a higher role, or change their own `OrganizationId`.

## 3. Input and mass assignment
- Controllers bind request models only, never entities.
- Request models contain only the fields the caller may set. Never accept `Id`,
  `OrganizationId`, `Role`, `CreatedAt` or similar server-owned fields from the client.
- Validate in guard clauses: required, length, range, format. Reject before any work.
- Set maximum lengths on every string and maximum sizes on every collection and upload.

## 4. Data access
- No string-concatenated or interpolated SQL. Use LINQ, or `FromSql` / `ExecuteSql` with
  interpolation (EF Core parameterizes it). Never `FromSqlRaw` with user input.
- List endpoints are paginated with a hard maximum page size.

## 5. Authentication and passwords
- Passwords are hashed with BCrypt, work factor 12 or more, through one password hashing
  interface. No other class calls BCrypt directly.
- Passwords are 64 characters maximum **and** 72 UTF-8 bytes maximum, checked before hashing
  (BCrypt ignores everything past 72 bytes, and accented letters and emoji take 2 to 4 bytes
  each, so 64 characters can exceed 72 bytes). A longer password is rejected, never truncated.
  Enforce it both in validation (`PasswordRulesAttribute` for new passwords, `MaxPasswordBytesAttribute`
  on the login password and the change-password current password so they are rejected before the user lookup or `Verify`; 400 with a translated message) and in
  the hasher (`Hash` throws, `Verify` returns false).
- Login failures return one generic message. Never say whether the email exists.
- JWT: validate issuer, audience, lifetime and signing key. Short-lived access tokens.
- Rate-limit login, registration and password reset endpoints.

## 6. Secrets and configuration
- No connection strings, JWT keys, API keys or Sentry DSNs (Data Source Names) in tracked
  files. Use user-secrets locally and environment variables in deployment.
- `appsettings.json` holds placeholders only.
- If a secret is found in a diff, stop and report it. Do not commit.

## 7. Responses, errors and logging
- Response models never expose password hashes, tokens, internal IDs of other tenants, or
  stack traces.
- Unhandled exceptions return a generic 500. Details go to Sentry only.
- Never log or send to Sentry: passwords, tokens, full JWTs, personal documents (CPF), card
  data. Emails only when needed to debug.

## 8. Dependencies
- Adding a package not already in the dependency graph is high risk: flag it in the plan.
  Pinning an existing transitive package to a patched version is low risk.
- Run `dotnet list package --vulnerable` before merging a change that adds or updates packages.

## Checklist for reviewers
- [ ] Every tenant query scoped by `OrganizationId` from claims
- [ ] Permission policy declared on every new action (no role names), with a rejection test per disallowed role
- [ ] Cross-tenant access test exists and returns not found
- [ ] Request models contain no server-owned fields
- [ ] No raw SQL with user input
- [ ] Passwords hashed only through the password hashing interface; no direct BCrypt call
- [ ] No secrets, tokens or personal data in code, logs, or responses