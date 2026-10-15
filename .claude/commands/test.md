---
description: Create stubs, write the scenario table and failing tests (step 3)
argument-hint: <module>/<feature>
---

Only run this after I approved the structure plan in docs/plans/$ARGUMENTS.md.

Use the tester subagent to scaffold the stubs and write the scenarios and tests for
docs/plans/$ARGUMENTS.md

When it returns, show me, in this order: the scenario table, the test file paths, and the
failing test output. Then stop and wait for my approval.

If I add, remove or change scenarios, send the changes back to the same tester agent so it
updates the table and the tests together, then show me both again.
