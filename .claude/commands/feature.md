---
description: Requirement to Gate 1 — spec, plan and failing tests, with automatic reviews
argument-hint: <requirement in plain words>
---

Run steps 1–3 of the CLAUDE.md workflow for this requirement:

$ARGUMENTS

If no requirement was given, ask me for it and stop.

Run each step with its subagent, in order. Do not ask me anything between steps unless a stop
condition below is met. Do not show me intermediate reports unless you stop.

1. spec-writer: write the spec.
   STOP if its Open questions contain any `[Blocking]` item. Show me the spec path and only the
   blocking questions, numbered, each with the assumption you would use. When I answer, update
   the spec with my answers and continue from step 2.
2. planner: write the plan for that spec.
3. plan-reviewer: review the plan.
   STOP if the verdict is "Fix first" or the plan's Risk is High. Show me the Risk line, both
   tables, the Signatures, the Flags and the review. When I approve (or give changes, which you
   send to the planner and re-review), continue from step 4.
4. tester: write the stubs, the Scenarios table and the failing tests.
5. test-reviewer: review them.
   If the verdict is "Fix first", send its Fix first findings to the tester, then run the
   test-reviewer again. Do this once. If it is still "Fix first", stop and show me the report.

⏸ Gate 1. Show me only:
- The spec path and its `[Assumed]` questions, one line each
- The plan's Risk line and file counts (new / modified)
- The Scenarios table
- The test-reviewer's verdict, "Read closely before approving" and "Questions for you"

Then stop and wait. If I change scenarios, send the changes to the tester, then show me the
updated table again. When I approve, tell me the `/implement` command to run next.
