# Deploy the HTTPS certificate

**Status:** proposed
**Severity:** high
**Origin:** docs/specs/identity/auth-hardening.md · rule 4 (code wired on branch feat/wire-https)
**Date:** 2026-10-16

## Problem
Outside Development the API serves HTTPS only and refuses to start without a certificate. No
environment outside Development has a certificate yet, so a production deployment of the API
image will not start. The frontend must also trust the certificate.

## Evidence
- `src/RestaurantManagement.Web/Program.cs:42` — calls `UseHttpsOnlyOutsideDevelopment()`
- `src/RestaurantManagement.Web/Hosting/HttpsHostingExtensions.cs:32` — throws when `Https:CertificatePath` is missing
- `infra/docker/Dockerfile` — the API image exposes 8443; nothing mounts a certificate

## Acceptance criteria
- [ ] The deployment mounts the certificate and sets `Https__CertificatePath`, `Https__CertificatePassword` and `Https__Port` (default 8443)
- [ ] A non-Development deployment starts and serves HTTPS on `Https__Port`
- [ ] The Next.js frontend calls the API over HTTPS without certificate errors

## Related
None
