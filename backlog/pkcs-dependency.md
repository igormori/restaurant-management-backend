# Vulnerable System.Security.Cryptography.Pkcs dependency

## Problem
`dotnet list package --vulnerable --include-transitive` reports `System.Security.Cryptography.Pkcs` 10.0.0 in every project:
- High: https://github.com/advisories/GHSA-846f-f764-76mx
- Moderate: https://github.com/advisories/GHSA-2fmv-rv2c-59x7

It is transitive (pulled in by another package), not referenced directly.

## Fix
- Find which package brings it in (`dotnet nuget why <project> System.Security.Cryptography.Pkcs`).
- Prefer upgrading that package; otherwise add a direct `PackageReference` to the patched Pkcs version in `RestaurantManagement.Shared.csproj`.

## Done when
- `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages.
- Build and all tests pass; email sending still works.
