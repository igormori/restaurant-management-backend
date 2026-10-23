---
description: Work through the backlog (bugs, improvements, technical debt) as small, reviewable pull requests
argument-hint: <source> [path:<folder>] [max:<n>] [auto]
---

# Backlog run

You are the orchestrator. You do not write code yourself. You load backlog items, triage them, send each safe item to a `backlog-worker` agent, and report the results.

Arguments: $ARGUMENTS

- `<source>` — where the items live. One of:
  - a markdown file path (e.g. `docs/backlog.md`) — one item per list entry
  - `github:<label>` — open GitHub issues with that label (`gh issue list --label <label> --state open --json number,title,body,labels`)
  - `jira:<JQL query>` — JIRA issues matching the query (use the JIRA tools if connected; if not, stop and say so)
- `[path:<folder>]` — limit the run to one or more folders, comma-separated (e.g. `path:src/billing` or `path:apps/web,packages/ui`). Paths are relative to the repository root. If omitted, the whole repository is in scope.
- `[max:<n>]` — maximum items to work on in this run. Default: 5. Never more than 10.
- `[auto]` — if present, skip the approval step and launch workers right after triage.

If a `path:` folder doesn't exist, stop and say so.

## Step 1 — Load

Load every open item from the source. For each item capture: id (issue number, JIRA key, or line number), title, full description, and any labels.

If the source can't be read, stop and explain why. Do not guess items.

## Step 2 — Validate

Backlog items go stale. Before triage, check every item against the current code (and recent git history: `git log --oneline -n 200`, `git log -S "<symbol>"` for removed code). Give each item one status:

- `valid` — the problem or need still exists as described.
- `already-done` — the bug no longer reproduces from the code, the improvement already exists, or a commit or merged PR clearly resolved it.
- `obsolete` — the code, feature, or page the item refers to was removed or replaced, so the item no longer applies.
- `partially-done` — part of it was done; describe what remains and continue with only the remaining part.
- `leftover` — the item refers to code that is a leftover from an old implementation: nothing uses it anymore (no references, imports, routes, or callers) because it was replaced by a newer implementation. Fixing it is wasted work. Instead, the item becomes a `tech-debt` item: "Remove leftover <name> from old implementation". Only use this status after searching the whole repository for references, including config files, dynamic imports, string-based lookups, and tests.

For every status except `valid`, record the evidence: the commit, file, or search that shows it.

`already-done` and `obsolete` items are not worked on. They go in the "Already done or obsolete" section of the report so the user can close them.

## Step 3 — Triage

Read `CLAUDE.md` (if present) and skim the relevant code for each remaining item. Classify every item:

- **Type:** `bug`, `improvement`, or `tech-debt`. Use labels when present; otherwise decide from the description.
- **Risk:**
  - `low` — small, local change (rename, lint, dead code, small bug with obvious cause, dependency patch bump)
  - `medium` — touches several files or shared code, but the expected behavior is clear and testable
  - `high` — architecture change, database migration, auth/security/payments, public API change, or code with no tests and unclear behavior
- **Clarity:** `clear` if the expected result is unambiguous, otherwise `unclear`.
- **Files likely touched.**
- **Scope:** `in-scope` if every file likely touched is inside the `path:` folder(s), otherwise `out-of-scope`. Test files that cover code inside the folder count as in scope, even if they live elsewhere (e.g. a top-level `tests/` folder). If no `path:` was given, everything is in scope.

Selection rules:

- When `path:` is given, ignore items that clearly have nothing to do with the folder(s). Items that are related but would need changes outside the folder go in the "Out of scope" section of the report.
- Never work on an item that asks for a new feature or capability (any item in `backlog/features/`): it goes in "Needs a human". Only the user decides features.
- Skip every `high` risk item and every `unclear` item. They go in the "Needs a human" section of the report with a one-line reason.
- Order the rest: bugs first, then improvements, then tech debt. Within each type, low risk before medium.
- Take the first `[max:<n>]` items.
- If two selected items are likely to touch the same files, give them to one worker together (in order) instead of running them in parallel.

## Step 4 — Approval

Unless `auto` was passed, show the triage as a table (id, title, status, type, risk, worker group) plus the skipped and already-done/obsolete items, and wait for the user to approve, remove items, or cancel.

## Step 5 — Run workers

For each approved item (or item group), launch the `backlog-worker` agent with the Agent tool, using `isolation: "worktree"` so each worker has its own copy of the repository. Run up to 5 workers in parallel.

Give each worker everything it needs, since it starts with no context:

- item id, type, title, full description
- validation status and evidence (for `partially-done`: what remains; for `leftover`: the unused code to remove and the searches that showed it's unused)
- risk level and the files you expect it to touch
- the base branch (the repository's default branch unless the user said otherwise)
- the allowed folder(s) from `path:`, or "whole repository" if none was given
- for grouped items: all items, in order

## Step 6 — Report

When all workers finish, report in this format:

**Pull requests opened** — id, title, type, PR link, one-line summary
**Already done or obsolete** — id, title, status, evidence (commit, file, or search), suggested action: close the item
**Stopped by the worker** — id, title, reason (tests failing, scope grew, needed a decision)
**Needs a human** — skipped items from triage, with reason
**Out of scope** — related items that need changes outside the `path:` folder(s), with the folders they'd touch
**Remaining in backlog** — count of items not attempted this run

## Hard rules

- Never merge a pull request.
- Never push to the default branch.
- Never close, resolve, or edit backlog items or tickets. Only add a comment with the PR link when the source is GitHub or JIRA.
- If anything looks destructive or irreversible, stop and ask.