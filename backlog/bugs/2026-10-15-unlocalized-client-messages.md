# Some client-facing messages are not localized

**Found during:** localization check of server responses (ad hoc request)
**Date:** 2026-10-15

## Description
Most client messages go through `IStringLocalizer<SharedResource>` (en + pt), but these do not:

- **Missing resource key:** `MenuNotFound` is used in `MenuService` (4 places) but is not in
  `SharedResource.resx` or `SharedResource.pt.resx`, so the client receives the raw key
  `"MenuNotFound"`.
- **Missing pt key:** `PhoneAttribute_ValidationError` (used in `RegisterRequest`) is missing
  from `SharedResource.pt.resx`.
- **Hardcoded English in Menu:** `MenuCategoryController` returns
  `"Menu category not found"` (3 places); `CreateMenuCategoryRequest` and
  `UpdateMenuCategoryRequest` use English `ErrorMessage` texts.
- **Hardcoded English in Organization:** the hex color `ErrorMessage` texts in
  `CreateOrganizationRequest` and `EditOrganizationRequest`.
- **Validation attributes with no `ErrorMessage`:** data-annotations localization only applies
  when `ErrorMessage` is set, so the default English .NET text is returned for `LoginRequest`,
  `SelectOrganizationRequest`, `LocationModels`, `Create/EditOrganizationRequest` (Name,
  Description) and `Create/EditMenuRequest`.
- **Generic 500:** `ExceptionHandlingMiddleware` returns the hardcoded
  `"An unexpected error occurred."`.
- **Invalid user ID:** `LocationController.GetCurrentUserId` throws
  `UnauthorizedAccessException("Invalid User ID")`, which is not a `BusinessException`, so the
  client gets a 500 with the generic English message instead of a localized 401.

## Why it matters
Brazilian users (pt) see English text or raw keys in some errors. Each case should use a
resource key present in both `.resx` files, with a test that requests `pt` and checks the
translated message.
