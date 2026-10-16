# Review: Fix the spelling of the English 401 message

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 600 passed, 0 failed

Scope reviewed: commit 98c1e58, `git diff development...HEAD`.
Small-change path: no spec, plan or scenarios; checked against `CLAUDE.md` and the architecture,
security, clean-code and testing skills. Fixes `backlog/bugs/2026-10-15-unauthorized-message-typo.md`.

Test totals per project: Shared 106, Organization 102, Menu 47, Identity 148, Web 197.
Build had 0 warnings and 0 errors. No packages were added or updated, so the vulnerable-package
check was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Shared/Localization/SharedResource.pt.resx:49 | The Portuguese text for the same key has a similar error: "Usuàrio não autorizado!" uses a grave accent; correct Portuguese is "Usuário" (acute accent). Out of scope for this change. | Fixed after review in 0157891, with a new test asserting the Portuguese text. |
| 2 | src/RestaurantManagement.Shared/Localization/SharedResource.resx:49 | The message changes for every 401 (unauthorized) response in English. A client app that matches on the exact old text "Unautorized user." would stop matching. | Unlikely before launch, but mention it in the pull request so client developers know. Clients should rely on the status code, not the text. |

## Checks passed
- **Scope:** one resource value, one test constant, and the resolved backlog file. No migration,
  package, endpoint or Shared contract (a type other modules depend on) changed. The resource
  key `UnauthorizedMessage` is unchanged.
- **Tests:** one test file was edited: `tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs:20`.
  The backlog item required this edit in the same change. The test still asserts the exact
  text, now the correct one, so it was not weakened. No test was deleted or skipped.
  No other test, source file or spec still contains "Unautorized".
- **Structure and architecture:** no new files or folders.
- **Security:** no secrets, tokens or personal data in the diff. The 401 message stays generic
  and reveals nothing about why the request failed.
- **Comments and style:** no comments, no `TODO`s added.
- **Commit:** a single Conventional Commit (`fix(shared): ...`), lowercase, imperative, under
  72 characters. The body explains the why. No Scenarios line needed for a small change.
- **Backlog:** the resolved bug file was deleted in the same commit.

## Check before merging
- `SharedResource.resx:49`: the user-visible text of every English 401 response changes.

## Pull request description
**Fix the spelling of the English "not signed in" message**

The English text returned with every 401 (unauthorized) response read "Unautorized user."
It now reads "Unauthorized user."

- **Shared:** corrected `UnauthorizedMessage` in `SharedResource.resx`. The key is unchanged;
  the Portuguese text is untouched.
- **Web tests:** `PrivateByDefaultTests` now asserts the corrected text, as the backlog item required.
- **Backlog:** removed the resolved `backlog/bugs/2026-10-15-unauthorized-message-typo.md`.

Risk: low. Only the text changes. Any client matching the old misspelled text exactly would
need updating. No migration, package, endpoint or Shared contract changes.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
