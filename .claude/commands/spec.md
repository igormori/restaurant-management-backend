---
description: Write the spec for a requirement and ask about anything ambiguous (step 1)
argument-hint: <requirement in plain words>
---

Use the spec-writer subagent to write a spec for this requirement:

$ARGUMENTS

It picks the owning module and the feature name and saves the spec to
docs/specs/<module>/<feature>.md. If no argument was given, ask me for the requirement and stop.

When it returns:
1. Tell me the spec path and, if the module was not obvious, why it was chosen.
2. Show me the spec's Rules and Acceptance criteria.
3. Ask me about anything ambiguous, missing, or contradictory: roles, organization and
   location scoping, validation, units, edge cases. Include the spec's Open questions. Number
   the questions and, for each, say what you would assume if I don't answer.

Stop after the questions. Do not plan or write code. Update the spec only with my answers.
