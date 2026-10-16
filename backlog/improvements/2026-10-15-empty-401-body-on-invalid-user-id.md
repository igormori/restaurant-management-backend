# Empty 401 body when the token's user identifier is invalid (Organization and Menu controllers)

**Found during:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md
**Date:** 2026-10-15

## Description
`OrganizationController`, `MenuController` and `MenuCategoryController` answer an unreadable user identifier in the token with a bare 401 and no message body. After the localize-client-messages plan, the locations endpoints answer the same case with a 401 and a translated message, so the API becomes inconsistent.

## Why it matters
Clients cannot show a translated reason for these 401s, and each controller repeats its own user-identifier parsing. One shared way to read the user and answer with the localized "unauthorized" message would remove the duplication.
