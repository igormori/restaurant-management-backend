---
description: Implement a feature against its approved failing tests (steps 4 and 5)
argument-hint: <module>/<feature> [branch or ticket ID]
---

Only run this after I approved the scenarios and tests in docs/plans/<module>/<feature>.md.

Use the implementer subagent to implement: $ARGUMENTS
The first argument is `<module>/<feature>` (plan at docs/plans/<module>/<feature>.md);
an optional second argument is the branch name or ticket ID.

When it returns, show me the code review summary: the branch, each commit with the scenarios
it covers, the files changed, any deviation from the approved structure and why, what the
tests do not cover, and the final test results. Then stop and wait for me.
Do not push or open a pull request.
