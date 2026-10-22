---
description: Build several small, low-risk items in one branch — one commit each — then one affected test run and one review
argument-hint: <item; item; item>  (plain words or backlog file paths, separated by ";")
---

Items: $ARGUMENTS

If no items were given, ask me for them and stop.

1. Run `git status`. If anything is uncommitted, stop and list it: I commit or stash it first.
2. Run `git fetch origin`. Pick the branch name, but do not create it yet:
   `backlog-fixes/<YYYY-MM-DD>`, using today's date. If that branch already exists locally or on
   origin, add `-2`, `-3`, and so on (`backlog-fixes/2026-10-16-2`).
3. Use the small-batch subagent with the full list of items and the branch name. It creates the
   branch from `origin/development` only after its size check keeps at least one item. If it kept
   no items, show me its message and stop: no branch was created.
4. Use the pr-reviewer subagent on the branch. There is no plan folder: it reviews the diff
   against the skills only, writes its review to `docs/reviews/<branch-name>.md` (with `/`
   replaced by `-`, e.g. `docs/reviews/backlog-fixes-2026-10-16.md`), and checks each commit is
   one item. It builds only; the full test suite runs in CI (Continuous Integration) on the pull
   request.

Show me, exactly as returned and with nothing added before or after:
- the small-batch agent's final message
- the pr-reviewer's final message

Then stop and wait. Never push or merge; I merge only when CI is green.