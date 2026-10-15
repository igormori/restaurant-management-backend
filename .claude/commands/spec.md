---
description: Write only the spec for a requirement (step 1)
argument-hint: <requirement in plain words>
---

Use the spec-writer subagent to write a spec for this requirement:

$ARGUMENTS

If no argument was given, ask me for the requirement and stop.

When it returns, show me:
1. The spec path and, if the module was not obvious, why it was chosen.
2. The spec's Rules and Acceptance criteria.
3. The `[Blocking]` questions, numbered, each with the assumption it used.
4. The `[Assumed]` questions, one line each. Do not ask me about these.

Stop. Update the spec only with my answers. Do not plan or write code.
