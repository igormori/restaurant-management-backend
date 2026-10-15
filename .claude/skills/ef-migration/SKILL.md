---
name: ef-migration
description: How to create, review, apply, and undo EF Core migrations in this solution. Use whenever an entity, DbContext, or EF configuration changes, or when a migration needs to be applied or rolled back.
---

# EF Core migrations

Each module has its own DbContext and its own migration history. A migration always names both
the module project and the context.

| Module | Context |
|---|---|
| Identity | IdentityDbContext |
| Organization | OrganizationDbContext |
| Menu | MenuDbContext |

Run every command from the repository root.

## Create a migration

```bash
dotnet ef migrations add <Name> \
  --project src/RestaurantManagement.Modules.<Module> \
  --startup-project src/RestaurantManagement.Web \
  --context <Module>DbContext
```

`<Name>` is PascalCase and describes the change: `AddMenuItemPrice`, `UniqueUserEmail`.

## Review it before applying

Open the generated file under `src/RestaurantManagement.Modules.<Module>/Migrations/` and check:

- Only the intended tables and columns changed. An unrelated change means the model and the
  database were already out of sync.
- No column is dropped and recreated when a rename was intended, which loses data.
- New non-nullable columns on an existing table have a default value, or the migration fails
  on a table that already has rows.
- Column names follow the existing snake_case convention.

Never hand-edit a generated migration. If it is wrong, remove it, fix the model, and generate again.

## Apply it

```bash
dotnet ef database update \
  --project src/RestaurantManagement.Modules.<Module> \
  --startup-project src/RestaurantManagement.Web \
  --context <Module>DbContext
```

## Undo

Before it is applied, or after reverting the database:

```bash
dotnet ef migrations remove \
  --project src/RestaurantManagement.Modules.<Module> \
  --startup-project src/RestaurantManagement.Web \
  --context <Module>DbContext
```

After it is applied, roll the database back to the previous migration first, then remove it:

```bash
dotnet ef database update <PreviousMigrationName> \
  --project src/RestaurantManagement.Modules.<Module> \
  --startup-project src/RestaurantManagement.Web \
  --context <Module>DbContext
```

## Rules

- One migration per feature. Do not bundle unrelated schema changes.
- A migration belongs to exactly one module. A feature that needs schema changes in two modules
  needs two migrations, and that usually means the design is crossing a module boundary.
- Never add a foreign key to another module's table. Store the other module's key as a plain
  `Guid` column.
- Commit the migration file together with the entity change that caused it.
- If `dotnet ef` reports more than one DbContext, the `--context` argument is missing.
