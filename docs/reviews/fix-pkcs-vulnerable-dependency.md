# Review: Pin patched System.Security.Cryptography.Pkcs

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 690 passed, 0 failed

Scope: small change (`/small`), no spec or plan folder. Resolves `backlog/tech-debt/pkcs-dependency.md`.
Branch `fix/pkcs-vulnerable-dependency`, one commit `2d53146`. Three files changed: one line in
`src/RestaurantManagement.Shared/RestaurantManagement.Shared.csproj`, one backlog item deleted, one
added. No code, test, migration, endpoint or Shared contract changes.

## Checks

| Area | Result |
|---|---|
| Tests | No file under `tests/` changed. 143 Shared + 106 Organization + 47 Menu + 148 Identity + 246 Web = 690 passed |
| Vulnerable packages | `dotnet list RestaurantManagement.sln package --vulnerable --include-transitive`: Pkcs is no longer listed in any project. All five `src/` projects are clean. Only `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 (High) remains, in 4 test projects |
| Package origin | `dotnet nuget why`: MailKit 4.18.1 → MimeKit 4.18.1 → Pkcs, now resolved to 10.0.13 everywhere. `dotnet list package --outdated` shows no newer MailKit/MimeKit, so "upgrade the parent package first" (the backlog item's preferred fix) was not possible |
| Scope / Structure | Only the change the backlog item prescribed. No new folders, no new source files |
| Architecture / Design / Comments | Nothing in scope (no C# changed) |
| Security | Reviewer checklist items on tenancy, authorization, input and secrets do not apply. Dependencies section: see "New NuGet package?" below |
| Commits | `fix(deps): ...`, 64 characters, lowercase, imperative. Body explains why. No scenarios apply |
| Secrets | None in the diff |
| Backlog | New item `2026-10-16-vulnerable-sqlite-test-dependency.md`: status `proposed`, every field filled in, date prefix correct, evidence confirmed by the command above (Identity, Menu, Organization and Shared test projects; Web.Tests is clean). No duplicate found (`grep -ril "sqlite\|vulnerab" backlog/`). Resolved item deleted correctly |

## New NuGet package?

CLAUDE.md lists "new NuGet package" as high risk, and the security skill says "No new NuGet package
without a flag in the plan". **This change is not a new package in that sense**:
- `System.Security.Cryptography.Pkcs` was already in every project's dependency graph and in every
  build output, through MimeKit. The direct reference adds no new code author, no new supply chain
  and no new capability. It only raises the resolved version from 10.0.0 to 10.0.13 (patch release,
  same major version, Microsoft-published).
- It is a package **update** of an existing dependency, so the rule that applies is "run
  `dotnet list package --vulnerable` before merging". That was done and is clean for `src/`.
- The backlog item itself named this exact fix as the fallback.

The real cost is maintenance, not risk: a direct pin outlives its reason. See Watch out 1.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Shared/RestaurantManagement.Shared.csproj:15` | Nothing in the file says why Pkcs is referenced directly. Shared code never uses it, so someone may later remove it as "unused" (which brings the vulnerable 10.0.0 back), or keep it long after MimeKit no longer needs it. The reason is only in the commit body | Add a one-line "why" comment above it, for example `<!-- Security pin: MimeKit 4.18.1 pulls Pkcs 10.0.0 (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). Remove once MailKit/MimeKit require >= 10.0.13. -->` |
| 2 | `backlog/tech-debt/2026-10-16-vulnerable-sqlite-test-dependency.md:12` | Evidence is a command, not `file:line` as the backlog skill's template shows | Optional: add the test `.csproj` lines that bring it in, e.g. `tests/RestaurantManagement.Modules.Identity.Tests/RestaurantManagement.Modules.Identity.Tests.csproj:13` (`Microsoft.EntityFrameworkCore.Sqlite` 9.0.2) |

## Check before merging
1. **Email still sends.** The backlog item's "Done when" includes "email sending still works". The
   only Pkcs consumer is MimeKit, used by `src/RestaurantManagement.Shared/Services/Email/EmailService.cs`.
   The tests replace the email service with a fake, so the real SMTP (Simple Mail Transfer Protocol)
   path is not covered. Send one verification email against a real or local SMTP server before merging.
   Risk is low: plain HTML mail does not use S/MIME (signed or encrypted email), the part of MimeKit
   that uses Pkcs.
2. **The `src/` vulnerable report is clean, but the solution-wide one is not.** The SQLite advisory
   remains in test projects. It is not shipped and is tracked in the new backlog item.

## Pull request description

**Fix vulnerable System.Security.Cryptography.Pkcs dependency**

`dotnet list package --vulnerable --include-transitive` reported `System.Security.Cryptography.Pkcs`
10.0.0 in every project (High: GHSA-846f-f764-76mx, Moderate: GHSA-2fmv-rv2c-59x7). It comes in
through MailKit 4.18.1 → MimeKit 4.18.1, and no newer MailKit is available yet.

What changed:
- **Shared:** direct `PackageReference` to `System.Security.Cryptography.Pkcs` 10.0.13 (patched),
  which all projects now resolve. This updates an existing dependency; it does not add new code.
- **Backlog:** deleted `tech-debt/pkcs-dependency.md` (resolved). Added
  `tech-debt/2026-10-16-vulnerable-sqlite-test-dependency.md` for the remaining test-only advisory
  in `SQLitePCLRaw.lib.e_sqlite3` 2.1.10.

Results: build passes, 690 tests pass, no vulnerable packages in any `src/` project.

Risk: low. Patch-level version bump of a package already in the build. No code, migration,
endpoint or contract changes. Remove the pin once MailKit/MimeKit require 10.0.13 or later.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
