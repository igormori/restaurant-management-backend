---
description: Implement against the approved failing tests, then review (steps 4–5, Gate 2)
argument-hint: <module>/<feature> [branch or ticket ID]
---

The first argument is `<module>/<feature>` (plan at docs/plans/<module>/<feature>.md); an
optional second argument is the branch name or ticket ID. Arguments: $ARGUMENTS

Check before starting:
- docs/plans/<module>/<feature>.md exists and has a `## Scenarios` table.
If not, stop and tell me to run `/feature` or `/test` first.

1. Use the implementer subagent to implement it, passing both arguments.
   If it stops early, show me why and stop.
2. Use the pr-reviewer subagent to review the branch against main.

⏸ Gate 2. Show me, in this order:
- Branch, and each commit with the scenarios it covers
- Deviations from the approved structure, and why
- What the tests do not cover
- The pr-reviewer's verdict, Critical and Warnings
- Final test results

Then stop and wait. Do not push or open a pull request.
