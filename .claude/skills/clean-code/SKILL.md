---
name: clean-code
description: Clean code rules for the RestaurantManagement backend — size limits, SOLID, separation of concerns, comments and change discipline. Use when writing or reviewing production C# code.
---

# Clean code

## 1. Size limits
Generated migrations and `Program.cs` are exempt. Above a limit, split by responsibility; never just move code around.

| Unit | Limit | When over the limit |
|---|---|---|
| File | 200 lines (enforced by hook) | The class does more than one job: split it |
| `Program.cs` | No line limit | Prefer extension methods per module (e.g. `AddIdentityModule()`) to keep it readable |
| Class | One responsibility | If you need "and" to describe it, split it |
| Method | 30 lines | Extract well-named private methods |
| Controller action | 10 lines | Business logic belongs in `Services/` |
| Parameters | 4 | Group them into a request model |
| Nesting | 2 levels | Guard clauses and extracted methods |

## 2. SOLID (Single responsibility, Open/closed, Liskov substitution, Interface segregation, Dependency inversion)
- **Single responsibility:** one reason to change per class
- **Open/closed:** add behaviour with new classes, not by growing `switch`/`if` chains on a type
- **Liskov substitution:** an implementation never throws `NotSupportedException` for an interface member
- **Interface segregation:** small interfaces; a caller never depends on methods it does not use
- **Dependency inversion:** services depend on interfaces, injected through the constructor

## 3. Separation of concerns
Follow the folder rules in the architecture skill. In short: controllers handle HTTP only,
services hold business rules, `Data/` holds database configuration, entities never leave the
service, and controllers accept and return `Models/` only.

## 4. Writing code
- Clarity over cleverness: code should read like a description of what it does
- Guard clauses first: handle invalid and failure cases at the top and return early,
  then write the happy path without nesting

  ```csharp
  if (request is null) return Error("Request is required");
  if (!organization.IsActive) return Error("Cannot add items to an inactive organization");

  // happy path
  ```

- Avoid `else` after a `return`
- Intention-revealing names; no abbreviations except well-known ones (`Id`, `Dto`)
- No magic strings or numbers; use named constants
- Async all the way; never `.Result` or `.Wait()`
- Constructor injection into `private readonly` fields
- Name the string localizer `_localizer` when it is a constructor-injected field, and `localizer`
  when it is a local variable (for example inside a validation attribute). Look up messages with a
  quoted key, `_localizer["Key"]`, so the automated "no message missing" check can see them; a key
  read from a property needs its own test proving the message exists in every language
- Models are classes with `{ get; set; }` properties, matching the existing Models folders

## 5. Comments
A comment is a last resort. First try a better name or a smaller method.

| Allowed | Not allowed |
|---|---|
| **Why** a non-obvious decision was made (business rule, workaround, constraint) | Restating **what** the code does (`// get the user`) |
| `///` XML documentation on public interfaces in `Shared/` (cross-module contracts) | `///` on private members or obvious properties |
| A link to a backlog file for known follow-up work | `TODO` with no backlog file |
| `// Arrange`, `// Act`, `// Assert` in tests (testing skill) | Commented-out code (Git keeps history) |
| | Change logs, author names or dates in comments |

One line, plain English, above the code it explains.

```csharp
// Locations are closed, never deleted, so sales history stays intact (spec 2026-10-15).
location.Status = LocationStatus.Closed;
```

## 6. Change discipline
- Minimum code that solves the request: no speculative features, abstractions or config
- Surgical changes: don't refactor or reformat code outside the task; mention issues instead
- Remove only the unused code your own change created
