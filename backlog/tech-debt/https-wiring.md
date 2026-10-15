# Wire HTTPS into the running app

## Problem
`UseHttpsOnlyOutsideDevelopment()` (src/RestaurantManagement.Web/Hosting/HttpsHostingExtensions.cs) exists and is tested, but `Program.cs` does not call it. Outside Development the API still serves plain HTTP and starts without a certificate. This is the unfinished part of the auth-hardening spec (docs/specs/identity/auth-hardening.md, rule 4).

## Why it was left
Calling it from `Program.cs` broke four Production startup tests in `StartupConfigurationTests` (scenarios 15, 18, 19, 20). They supply `Https:*` through `WebApplicationFactory` configuration, which is applied after `Program.cs` reads it.

## Fix
- Have those tests (or `AuthApiFactory`) supply `Https:*` through environment variables, so the values exist when `Program.cs` runs. No test expectations change.
- Call `builder.UseHttpsOnlyOutsideDevelopment()` in `Program.cs`.
- Deployment: mount the certificate into the Docker container and set `Https__CertificatePath`, `Https__CertificatePassword`, `Https__Port` (default 8443). Next.js must trust the certificate.

## Done when
- Outside Development the API serves HTTPS only and fails to start without a certificate.
- All tests pass.
