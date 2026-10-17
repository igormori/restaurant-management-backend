---
name: backlog
description: Rules for recording work found outside the current task — when to fix inline, when it blocks, when to write a backlog item, how to avoid duplicates, and the required file format. Read it only when you are about to create or update a backlog item.
---

# Backlog rules

Capture, don't chase: work found outside the current task is recorded, not fixed on the side.
But not everything deserves a ticket. Decide in this order:

## 1. Decide what to do with a finding

| Finding | Action |
|---|---|
| It breaks the current spec's Outcomes or the current task's DoD (Definition of Done) | **Blocking.** Fix it in this task, or stop and report it as [Blocking]. Never send it to the backlog. |
| **Too small to ticket:** a typo, a wrong name, a missing or wrong comment, or a one-line fix with no behaviour change, in a file this task already changes | **Fix inline** (implementer and small-feature only) and mention it in the Delivered note or final message. Never in test files after Gate 2. |
| Anything else outside the task's scope | **Backlog item** (below) |

## 2. Search before creating

1. Search the backlog for the same file, class, endpoint or problem:
   `grep -ril "<keyword>" backlog/` with two or three keywords.
2. **Same problem already listed:** add your evidence and origin to that file instead of creating
   a new one.
3. **Related but different:** create the item and link the related file under "Related".

## 3. File name and folder

`backlog/<folder>/<YYYY-MM-DD>-<short-kebab-title>.md`, always with the date prefix.

| Folder | Use for |
|---|---|
| `bugs/` | Behaviour that differs from what was intended |
| `features/` | A new capability |
| `improvements/` | Works as designed, should work better |
| `tech-debt/` | Internal quality: naming, missing tests, fragile design, docs |
| `pre-production/` | Must be done before launch, outside the code: deployment, certificates, secrets, infrastructure |

## 4. File template (every field required)

```markdown
# <Title>

**Status:** proposed
**Severity:** critical | high | medium | low
**Origin:** <spec or plan path> · <phase or "Phase 0 — Task N">
**Date:** YYYY-MM-DD

## Problem
What is wrong or missing, in plain words.

## Evidence
- `<file>:<line>` — what it shows

## Acceptance criteria
- [ ] <measurable result that proves it is fixed>

## Related
<other backlog files, or "None">
```

| Severity | Meaning |
|---|---|
| critical | Security, data leak between organizations, data loss |
| high | Wrong behaviour users will hit |
| medium | Wrong behaviour in edge cases, or blocks a planned feature |
| low | Quality, naming, docs |

## 5. Status

- Agents always write **`proposed`**. Only the user changes it to **`accepted`**, merges items, or
  deletes them.
- When work fully solves an item, commit the fix with `Refs: <backlog file path>` in the body,
  then delete the item file but **never commit the
  deletion**: the user checks what was solved and commits it. Never push.
- When a spec fully solves an item, the spec lists it under "Backlog Impact" and the item is
  deleted, uncommitted, once that work is done. Partially solved: update Problem and Acceptance criteria to
  what is left.

## 6. Limits

- Never more than 3 new backlog items from one task. More than that means the task's scope was
  wrong: say so in your output instead.
- Never write a backlog item for something you are not sure is a problem. Check it first.
