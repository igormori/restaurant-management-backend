---
name: clean-code
description: Clean code rules for the RestaurantManagement backend. Use when writing or reviewing production C# code.
---

# Clean code

- Clarity over cleverness: code should read like a description of what it does
- Guard clauses first: handle invalid and failure cases at the top and return early,
  then write the happy path without nesting

  ```csharp
  if (request is null) return Error("Request is required");
  if (!organization.IsActive) return Error("Cannot add items to an inactive organization");

  // happy path
  ```

- Avoid `else` after a `return`; keep nesting to 2 levels max
- Small methods with intention-revealing names
- No magic strings or numbers; use named constants
- Comments explain why, not what; no commented-out code
- Async all the way; never `.Result` or `.Wait()`
- Constructor injection into `private readonly` fields
- Models are classes with `{ get; set; }` properties, matching the existing Models folders
- Prefer polymorphism over switch/if chains on a type code
- Minimum code that solves the request: no speculative features, abstractions or config
- Surgical changes: don't refactor or reformat code outside the task; mention issues instead
- Remove only the unused code your own change created
