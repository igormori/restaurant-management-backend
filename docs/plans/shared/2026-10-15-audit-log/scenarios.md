# Scenarios: Audit Log

**Plan:** docs/plans/shared/2026-10-15-audit-log/plan.md
**Coverage:** 25 scenarios — 9 happy · 3 edge · 4 failure · 4 security · 5 guard

Terms: DTO (Data Transfer Object: a plain request or response shape). IP address (Internet Protocol
address: the caller's network address). Guard (a behaviour that is already correct and must stay so;
its test passes before and after the change).

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | A recorded entry keeps organization, location, user, action, changed item, before and after values, reason, IP address and time | `RecordAsync_AllFieldsGiven_StoresEveryField` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 2 | T1 | Edge | An entry with no location, no reason and no before value is stored with those left empty | `RecordAsync_NoLocationReasonOrBeforeValue_StoresEntryWithThoseEmpty` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 3 | T1 | Security | When the changed item is another organization's location, the entry is still stored in the signed-in organization | `RecordAsync_ChangedItemBelongsToAnotherOrganization_StoresEntryInTokenOrganization` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 4 | T1 | Failure | Changing or removing a stored entry is refused and the stored row stays exactly as it was (2 cases) | `SaveChanges_StoredEntryChangedOrRemoved_IsRefusedAndRowUnchanged` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 5 | T1 | Happy | The database has an index on organization and time, so reading by date stays fast | `Migrate_AuditLogMigrations_CreatesOrganizationAndTimeIndex` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 6 | T1 | Happy | The audit log migrations match the code model exactly | `Migrate_AuditLogMigrations_ModelHasNoPendingChanges` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 7 | T1 | Security | An entry recorded in one organization is not found when reading in another organization | `ReadEntries_EntryRecordedInCantinaReadInTrattoriaScope_FindsNothing` | `tests/RestaurantManagement.Web.Tests/AuditLogStorageTests.cs` |
| 8 | T2 | Security | With entries in two organizations, a user reads only their own organization's entries, even when the address names the other organization | `GetAuditLog_EntriesInTwoOrganizationsAndQueryNamesTheOther_ReturnsOnlyTokenOrganizationsEntries` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 9 | T2 | Security | Cashier, Waiter and Kitchen, and an Owner with no organization selected, are refused with 403 (4 cases) | `GetAuditLog_WithoutReportsViewInSelectedOrganization_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 10 | T2 | Guard | Reading the log without signing in returns 401 | `GetAuditLog_NoToken_ReturnsUnauthorized` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 11 | T2 | Happy | Owner, Admin and Manager can read the log (3 cases) | `GetAuditLog_RoleWithReportsView_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 12 | T2 | Edge | A from/to date range (entries exactly on both ends included) or an action filter returns only matching entries (2 cases) | `GetAuditLog_FilterGiven_ReturnsOnlyMatchingEntriesIncludingRangeEnds` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 13 | T2 | Happy | Entries come newest first | `GetAuditLog_NoFilters_ReturnsEntriesNewestFirst` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 14 | T2 | Edge | Asking for 500 entries per page returns 100; asking for no page size returns 50 (2 cases) | `GetAuditLog_PageSizeAboveLimitOrNotGiven_ReturnsCappedOrDefaultPage` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 15 | T2 | Failure | A "from" date after the "to" date, or an unknown action, is refused with 400 and a translated message (2 cases) | `GetAuditLog_InvalidFilter_ReturnsBadRequestWithLocalizedMessage` | `tests/RestaurantManagement.Web.Tests/AuditLogEndpointTests.cs` |
| 16 | T3 | Happy | Changing only the service charge (10% to 12%), only the discount approval limit (5% to 8%) or only the business day cutoff (04:00 to 05:30) records one "settings changed" entry with the old and new settings (3 cases) | `EditOrganizationAsync_OneSettingChanged_RecordsOneSettingsChangedEntry` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceAuditTests.cs` |
| 17 | T3 | Guard | An edit that keeps all three settings records nothing, whether the name changes or nothing changes at all (2 cases) | `EditOrganizationAsync_SettingsUnchanged_RecordsNothing` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceAuditTests.cs` |
| 18 | T3 | Guard | A refused edit (Waiter, or no organization selected) records nothing (2 cases) | `EditOrganizationAsync_EditRejected_RecordsNothing` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceAuditTests.cs` |
| 19 | T3 | Failure | If the log cannot be written, the settings change stays saved, an error is logged and the edit still succeeds | `EditOrganizationAsync_RecordingFails_KeepsSettingsSavedAndLogsError` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceAuditTests.cs` |
| 20 | T3 | Happy | After a settings edit through the API, the entry appears in the organization's log and not in another organization's log | `EditOrganization_ServiceChargeRateChanged_EntryVisibleOnlyToSameOrganization` | `tests/RestaurantManagement.Web.Tests/AuditLogRecordingEndpointTests.cs` |
| 21 | T4 | Happy | Editing a location from Active to Temporarily closed, or deleting it (Active to Closed), records one "location status changed" entry with the location and both statuses (2 cases) | `ChangeLocationStatus_StatusChangesFromActive_RecordsOneLocationStatusChangedEntry` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceAuditTests.cs` |
| 22 | T4 | Guard | An edit that keeps the same status, or deleting an already closed location, records nothing (3 cases) | `ChangeLocationStatus_StatusUnchanged_RecordsNothing` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceAuditTests.cs` |
| 23 | T4 | Guard | A refused change records nothing: Waiter edit or delete (403), or editing another organization's location (404) (3 cases) | `ChangeLocationStatus_RequestRejected_RecordsNothing` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceAuditTests.cs` |
| 24 | T4 | Failure | If the log cannot be written, the location stays closed, an error is logged and the delete still succeeds | `DeleteLocationAsync_RecordingFails_KeepsLocationClosedAndLogsError` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceAuditTests.cs` |
| 25 | T4 | Happy | After deleting a location through the API, the log shows the entry from Active to Closed | `DeleteLocation_ActiveLocation_EntryShowsActiveToClosed` | `tests/RestaurantManagement.Web.Tests/AuditLogRecordingEndpointTests.cs` |

## Decisions accepted at Gate 2 (user, 2026-10-16)
1. Spec Outcome #1 (role change recorded) stays without a test until `backlog/features/2026-10-16-member-role-change.md` is built. Do not flag the missing test in review.
2. The T2 endpoint tests (#8, #9, #11–#15) fail during setup (no `audit_log_entries` table) until T1's migration exists. That is accepted. After T1 is committed, each of them must fail on its status check (404, endpoint missing) before T2 work starts.

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Role change is recorded | Not checkable in this plan: no "change member role" feature exists (plan Decisions). T1 defines the action; the feature is in `backlog/features/2026-10-16-member-role-change.md` |
| #2 Only own organization is visible | 7, 8, 20 |
| #3 Permission required (403 without reports.view) | 9 (also 10, 11) |
| #4 Entries cannot be changed | 4; "no feature offers update or delete" is checked at review (pr-reviewer, plan Overall Definition of Done) |

| Rule | Scenarios |
|---|---|
| 1 Fields of an entry | 1, 2, 3 |
| 2 Logged actions | 16, 20 (settings), 21, 25 (location status); member role change: see Outcome #1 |
| 3 Append-only | 4 |
| 4 Own organization, filtered by date and action | 7, 8, 12, 13, 14, 15 |
| 5 reports.view permission | 9, 10, 11 |

## Existing tests updated
Forced by the new constructor parameters (`IAuditLog` on `OrganizationService`; `IAuditLog` and a logger on `LocationService`). Each call now passes substitutes; no assertion changed.
- `tests/RestaurantManagement.Modules.Organization.Tests/`: `OrganizationServiceEditTests.cs`, `OrganizationServiceCreateTests.cs`, `OrganizationServiceReservedNameTests.cs`, `OrganizationServiceNotFoundTests.cs`, `OrganizationSettingsTests.cs`, `OrganizationCreationScopeTests.cs`, `OrganizationTenantFilterTests.cs`, `LocationServiceTests.cs`, `LocationFieldsTests.cs`
- `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs`: migrates the audit log database context and gains `SeedAuditEntriesAsync` (stores entries at chosen times, without the stamping interceptor)
