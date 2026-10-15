---
description: Write and review only the stubs, scenarios and failing tests (step 3, Gate 1)
argument-hint: <module>/<feature>
---

Check that docs/plans/$ARGUMENTS.md exists. If it does not, stop and say so.

1. Use the tester subagent to scaffold the stubs and write the scenarios and tests for
   docs/plans/$ARGUMENTS.md
2. Use the test-reviewer subagent to review them. If the verdict is "Fix first", send its
   Fix first findings to the tester and review again, once.

⏸ Gate 1. Show me only: the Scenarios table, the test-reviewer's verdict, "Read closely before
approving" and "Questions for you". Then stop and wait.

If I add, remove or change scenarios, send the changes to the tester so it updates the table
and the tests together, then show me the table again.
