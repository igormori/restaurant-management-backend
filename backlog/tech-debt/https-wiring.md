# Wire HTTPS into the running app

## Problem
`Program.cs` now calls `UseHttpsOnlyOutsideDevelopment()` and the API image exposes 8443, so outside Development the API serves HTTPS only and refuses to start without a certificate (docs/specs/identity/auth-hardening.md, rule 4). What is left is deployment: no environment outside Development has a certificate yet, so the API cannot start there.

## Fix
- Deployment: mount the certificate into the Docker container and set `Https__CertificatePath`, `Https__CertificatePassword`, `Https__Port` (default 8443).
- Next.js must trust the certificate.

## Done when
- A non-Development deployment starts with the mounted certificate and serves HTTPS on `Https__Port`.
- The Next.js frontend calls the API over HTTPS without certificate errors.
