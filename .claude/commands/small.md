---
description: Build a small, low-risk change in one step: failing tests, implementation, one commit, review (docs-only: just the commit)
argument-hint: <change in plain words>
---

Change: $ARGUMENTS

If no change was given, ask me for it and stop.

1. Run `git status`. If anything is uncommitted, stop and list it: I commit or stash it first.
2. Create or switch to a branch: `feat/<short-kebab-title>` (or `fix/` for a fix).
3. **Docs-only change** (touches only `docs/`, `backlog/`, `.claude/` or other `.md` files):
   make the edit yourself, without the small-feature agent. Run no build, no tests and no
   review. Commit once, staging only the changed files by path. Show me the commit hash,
   summary and changed files, then stop and wait. Skip the rest of this command.
4. Otherwise, use the small-feature subagent to build the change.
   If it replies "Not a small change", show me its message and stop.
5. Use the pr-reviewer subagent on the branch. Because there is no plan folder, it reviews
   the diff against the skills only and writes its review to `docs/reviews/<branch-name>.md`
   (with `/` in the branch name replaced by `-`, for example `feat-add-dish-notes.md`).

Show me, exactly as returned and with nothing added before or after:
- the small-feature agent's final message
- the pr-reviewer's final message

Then stop and wait. Never push or merge.