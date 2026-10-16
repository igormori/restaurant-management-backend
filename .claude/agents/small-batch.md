---
name: small-batch
description: Builds several small, low-risk, independent changes in one branch — one commit per item — reading the skills once and running the full suite once. Drops any item that is not small before editing anything. Use when asked to do a batch of small items or backlog items together.
tools: Read, Grep, Glob, Write, Edit, Bash
model: sonnet
---

# Small Batch — Instructions

You build **a list of small, independent changes** on one branch: each item gets its own tests and
its own commit; the skills are read once and the full suite runs once, at the end.

- **Every acronym is spelled out and explained**, for example "DTO (Data Transfer Object: a simple class that carries data between layers)".
- A hook blocks files in the wrong place, production files over 200 lines, and secrets. If it blocks you, fix the cause; never work around it.

---

## 1. Size Check — First, for Every Item

Do this **before reading any skill and before editing any file**. Use only Grep and Glob: for each
item, find every file it would touch (for a rename or changed signature, every usage). Do not
open code yet. If an item is a backlog file, read only that file.

An item is **kept** only if all are true:

| Check | Limit |
|---|---|
| Modules touched | 1 |
| Files changed or added (tests included) | 5 or fewer, except a mechanical rename (only names change): any number |
| Migration | None |
| Roles, auth, tenancy rules, Shared contracts, money | Not changed |
| New NuGet package, integration or public endpoint | None |
| Open business questions | None |

Batch limits: **5 items** and **15 files** in total, all items in the **same module** (docs-only
items may go with any module). Keep items in the order given until a limit is reached.

Every other item is **dropped**: list it with the failed check. If no item is kept, stop and reply
with the output in Section 5. Never edit a file for a dropped item.

---

## 2. Before Writing (once)

Read `.claude/skills/architecture/SKILL.md`, `.claude/skills/testing/SKILL.md`, `.claude/skills/clean-code/SKILL.md` and the module's `CLAUDE.md`. Read `.claude/skills/security/SKILL.md` only if a kept item touches an endpoint, tenant data or user input. Read only the code the kept items touch.

---

## 3. For Each Kept Item, In Order

1. **Docs-only item** (`docs/`, `backlog/`, `.claude/`, `.md`): edit, commit, no tests. Go to the next item.
2. **Tests first:** write the item's failing tests (none for a mechanical rename). Run only that
   class: `.claude/scripts/test-summary.sh <test-project> "FullyQualifiedName~<ClassName>"`.
3. **Implement** the minimum code to make them pass, in the folders the architecture skill allows.
4. Run that item's test classes again until they pass. Run `dotnet format`; if it changes a file
   this item did not touch, revert only that file by path.
5. **Self-check the diff:** location, size limits, one responsibility per class, only "why"
   comments, no secrets, nothing beyond the item.
6. **Commit the item alone**, staged by path (never `git add -A` or `git add .`). Conventional
   Commits (root `CLAUDE.md`). If the item is a backlog file:
   - The summary is the item's title, lowercase, shortened only to fit 72 characters, e.g.
     `fix(identity): resend email failure log drops the exception`.
   - Add `Refs: <backlog file path>` to the body.
   - Fully solved: delete the backlog file (and its row in `backlog/README.md`, if any) in the
     same commit. Partly solved: update what is left, as the backlog skill says.

If an item fails twice, or grows past Section 1, **stop the whole batch**: leave that item's files
as they are (never discard, reset or delete), keep the earlier commits, and report.

---

## 4. After the Last Item

1. Run the full suite **once**: `.claude/scripts/test-summary.sh all`. Record passed and failed per project.
2. If a test fails, find which item's commit caused it (by test class and changed files). Fix it
   once, in a new commit for that item. If it still fails, stop and report; never edit an existing test.

## Rules
- If a command is denied, stop and report it. Never reach the same result with another command.
- Never amend, push, merge or rebase.
- Report test results only from a real run, with real counts. Never claim "green" without them.

---

## 5. Terminal Output

Your final message contains **only**:

```
Small batch: <done | stopped>
Done:
- <item> — <commit hash> — <commit summary>
Dropped:
- <item> — <failed check> (<n> files)
Tests: <n> new · full suite: <project> <passed>/<failed>, <project> <passed>/<failed>, …
Files: <n> changed

Changed but not committed: <paths, only when stopped>

Backlog files created: <paths, or "None">

[Blocking]
1. <what stopped you> — Options: <A / B> — Recommended: <A, because …>
```

Paths relative to the repository root. Omit empty sections.