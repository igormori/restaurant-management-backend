# Plan: Unique email index
Spec: docs/specs/identity/unique-email-index.md

## Approach
Add a stored computed shadow column `email_normalized` (`lower(email)`) on `users` with a
unique index, configured in `IdentityDbContext.OnModelCreating` so EF generates the whole
migration. `RegistrationService` keeps the `AnyAsync` fast path and adds a guard clause that
catches the Postgres unique violation on save and rethrows the existing `BusinessException`.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| Data/IdentityDbContext.cs | Modified | Shadow computed column, unique index, `UniqueEmailIndexName` const |
| Services/RegistrationService.cs | Modified | Catch unique violation on `SaveChangesAsync`, throw `BusinessException` |
| Migrations/<ts>_UniqueUserEmail.cs | New | Generated: add column + unique index |
| tests/.../RegistrationServiceTests.cs | Modified | Casing-duplicate cases |
| tests/.../IdentityPostgresFixture.cs | New | Testcontainers Postgres, applies migrations |
| tests/.../UniqueEmailIndexTests.cs | New | Concurrency and migration tests |
| tests/.../*.Tests.csproj | Modified | Add `Testcontainers.PostgreSql` |

## API
No change.

## Data
`User` entity unchanged. In `OnModelCreating`: `Property<string>("EmailNormalized")`
with `HasComputedColumnSql("lower(email)", stored: true)`, and
`HasIndex("EmailNormalized").IsUnique().HasDatabaseName(UniqueEmailIndexName)` where the
const is `ix_users_email_normalized`.

Before deploying, confirm the target database is clean; the migration must fail, not dedupe:
`SELECT lower(email), count(*) FROM users GROUP BY 1 HAVING count(*) > 1;` must return no rows.

```
dotnet ef migrations add UniqueUserEmail --project src/RestaurantManagement.Modules.Identity --startup-project src/RestaurantManagement.Web --context IdentityDbContext
```

## Authorization
No change; registration is anonymous and `users` is not organization-scoped.

## Steps
1. Add the computed column, index and name const to `IdentityDbContext` -> verify: `dotnet build RestaurantManagement.sln`
2. Add `IsDuplicateEmailViolation(DbUpdateException)` private static helper in `RegistrationService` -> verify: build
3. Wrap `SaveChangesAsync`/commit in try-catch using that helper, throwing the same `BusinessException` -> verify: build
4. Generate the migration with the command above -> verify: file contains only the new column and index
5. Run the duplicate-check query, then apply the migration locally -> verify: `dotnet ef database update ...`
6. Add `Testcontainers.PostgreSql` to the Identity test project -> verify: `dotnet build`
7. Add `IdentityPostgresFixture` applying `Database.MigrateAsync()` -> verify: `dotnet test`
8. Add the tests below -> verify: `dotnet test`
9. Run `dotnet format` -> verify: clean

## Tests
| Acceptance criterion | Test name and assertion |
|---|---|
| Concurrent duplicates create one row, loser gets the same error | `RegisterAsync_TwoConcurrentRequestsSameEmail_CreatesOneUserAndRejectsOther` (Postgres): one `BusinessException`, `users` count is 1 |
| DB conflict never a raw error or 500 | `RegisterAsync_UniqueViolationOnSave_ThrowsBusinessExceptionWithPreCheckMessage` (Postgres): type is `BusinessException`, 400, message equals `EmailAlreadyRegistered` |
| `User@Example.com` rejected when `user@example.com` exists | `RegisterAsync_ExistingEmailDifferentCasing_RejectsDuplicate` (Postgres): throws, `users` count stays 1 |
| Migration applies to clean data without loss | `Migrate_DatabaseWithDistinctEmails_SucceedsAndKeepsRows` (Postgres): seed 2 distinct emails before the migration, both rows present after |
| New unique email still succeeds | Existing `RegisterAsync_ValidRequest_SendsVerificationEmailWithFirstNameAndConfiguredExpiry` passes unchanged |
| Login, verify, resend unaffected | Existing `SessionServiceVerificationTests` and `VerificationServiceTests` pass unchanged |

## Decisions
- Stored computed column over `citext` or a raw-SQL expression index: EF generates the migration, no extension privileges, lookup semantics unchanged.
- The test project gains `Testcontainers.PostgreSql`; SQLite cannot prove Postgres index and concurrency behaviour.
- Only `PostgresException` SqlState `23505` on `ix_users_email_normalized` is translated; every other `DbUpdateException` propagates.
- Caller-facing message is identical to the pre-check message, per the spec assumption.
- Adding a stored column rewrites `users`; acceptable at current table size, and a duplicate check is required before deploy in each environment.

## Follow-ups
- Login, verify and resend still match email case-sensitively, so `User@Example.com` cannot sign in.
- The `users` table has no case-insensitive lookup index for those flows.
- Items listed in docs/follow-ups.md remain open.
