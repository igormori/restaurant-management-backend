---
description: Build several small, low-risk items in one branch — one commit each — then one full test run and one review
argument-hint: <item; item; item>  (plain words or backlog file paths, separated by ";")
---

Items: $ARGUMENTS

If no items were given, ask me for them and stop.

1. Run `git status`. If anything is uncommitted, stop and list it: I commit or stash it first.
2. Run `git fetch origin`, then create the branch from the latest development:
   `git switch -c <type>/<short-kebab-title> origin/development` (`fix/`, `chore/` or `refactor/`).
3. Use the small-batch subagent with the full list of items.
   If it kept no items, show me its message and stop.
4. Use the pr-reviewer subagent on the branch. There is no plan folder: it reviews the diff
   against the skills only, writes its review to `docs/reviews/<branch-name>.md` (with `/`
   replaced by `-`), and checks each commit is one item. The small-batch agent already ran the
   full suite: the reviewer runs only `dotnet build RestaurantManagement.sln` and the test
   projects of the module changed.

Show me, exactly as returned and with nothing added before or after:
- the small-batch agent's final message
- the pr-reviewer's final message

Then stop and wait. Never push or merge.