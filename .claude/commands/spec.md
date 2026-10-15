---
description: Write only the spec for a requirement (step 1)
argument-hint: <requirement in plain words>
---

Use the spec-writer subagent to write a spec for this requirement:

$ARGUMENTS

If no argument was given, ask me for the requirement and stop.

When it returns, show me only this, with paths relative to the repository root.
Omit any section that is empty. Add nothing before or after it:

Spec: <path>   (plus one line on why, only if the module was not obvious)
Backlog: created / updated / removed <paths>, or "None"
[Blocking]  numbered questions, 3 lines max each: question, options, recommendation
[Assumed]   max 5 items, one line each

End with "Ready for review." only when there are no [Blocking] questions.
Do not summarize or repeat the spec. Then stop and wait.

If I give answers or changes, send them to the spec writer. It updates the spec only with
those answers. Do not plan or write code.