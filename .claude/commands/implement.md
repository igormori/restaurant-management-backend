---
description: Implement an approved plan batch by batch, then review the branch (steps 4–5)
argument-hint: <module>/<spec-name> [branch or ticket]
---

Plan folder: docs/plans/$ARGUMENTS/ (the first word of the arguments is the plan; an optional
second word is the branch name or ticket).

If no plan was given, ask me for it and stop.

1. Check `scenarios.md` exists in the plan folder. If not, stop: tests come first (`/test`).
2. Run `git status`. The only uncommitted changes allowed are this feature's spec, plan folder,
   stubs and tests. If anything else is uncommitted (for example `.claude/`, `CLAUDE.md` or other
   work), stop and list it: I commit or stash it myself before implementing.
3. Run `git fetch origin`. If the branch (`<type>/<spec-name>`, or the branch or ticket I gave)
   already exists, switch to it and check it contains `origin/development`
   (`git merge-base --is-ancestor origin/development HEAD`); if not, stop and tell me. Otherwise
   create it from the latest development: `git switch -c <branch> origin/development`. Never
   create a branch from the current branch or from a local `development` or `main`.
4. For each batch in `plan.md`, in order, use a **new** implementer subagent with only the plan
   folder and the batch ID. Wait for it to finish before starting the next batch.
   If it reports "stopped" or any [Blocking] item, stop the whole flow and show me its message.
5. When every batch is done, use the pr-reviewer subagent on the branch. It builds only; the
   full test suite runs in CI (Continuous Integration) on the pull request.
6. If the verdict is "Fix first":
   - If **every** Fix first item is in `docs/` or `backlog/` (no code or test change), send them
     to a new implementer subagent to fix and commit, and do **not** run the pr-reviewer again.
   - Otherwise, send the review to a new implementer subagent once to fix the Fix first items,
     then run the pr-reviewer once more. Do not loop again.
   - In every fix round (including changes I ask for after reading the review), update
     `scenarios.md` for any new test and the affected tasks' Delivered notes, in the same
     commits as the fixes, with a `Scenarios:` line in each commit body.

Show me, exactly as returned and with nothing added before or after:
- each implementer's final message, in batch order
- the pr-reviewer's final message

No file lists, no paths beyond those in the messages, no summary. Then stop and wait (Gate 3).
Never push or merge; I do that after reading review.md, and merge only when CI is green.