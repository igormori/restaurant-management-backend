---
description: Build a small, low-risk change in one step: failing tests, implementation, one commit, review
argument-hint: <change in plain words>
---

Change: $ARGUMENTS

If no change was given, ask me for it and stop.

1. Run `git status`. If anything is uncommitted, stop and list it: I commit or stash it first.
2. Create or switch to a branch: `feat/<short-kebab-title>` (or `fix/` for a fix).
3. Use the small-feature subagent to build the change.
   If it replies "Not a small change", show me its message and stop.
4. Use the pr-reviewer subagent on the branch. Because there is no plan folder, it reviews
   the diff against the skills only and writes its review to `docs/reviews/<branch-name>.md`
   (with `/` in the branch name replaced by `-`, for example `feat-add-dish-notes.md`).

Show me, exactly as returned and with nothing added before or after:
- the small-feature agent's final message
- the pr-reviewer's final message

Then stop and wait. Never push or merge.