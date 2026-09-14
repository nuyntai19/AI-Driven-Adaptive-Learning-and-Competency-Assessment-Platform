# MySQL Integration Test Execution Summary

- **Timestamp**: 2026-09-14 (independent rerun after Docker restart and clean Release build)
- **Test Command**: `dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --filter 'FullyQualifiedName~MySql' -c Release`
- **Target MySQL Container**: Docker `edutwin-mysql` (MySQL 8.0.35 on `127.0.0.1:3307`)
- **Total Tests Discovered**: 51
- **Passed**: 51
- **Failed**: 0
- **Skipped**: 0
- **Duration**: 10m 14s

> The earlier 74-test figure came from a stale pre-restart test assembly. A fresh `--list-tests` against the current Release build discovered exactly 51 MySQL tests; all 51 passed. This current-build result is authoritative.

## Platform MySQL Tests Breakdown (19 / 19 Passed)
1. `Migration_AddPrimaryManagerToCenters_FailsBeforeDDL_WhenActiveCenterLacksActiveManager`: **PASS** (preflight validation aborts before DDL, zero temporary table left in database)
2. `Migration_EnforceActiveCenterPrimaryManagerDataIntegrity_Fails_WhenActiveCenterHasNullOrInvalidPrimaryManager`: **PASS** (corrective migration fails on invalid/null primary manager, zero temporary table left)
3. `Migration_EnforceActiveCenterPrimaryManagerDataIntegrity_SucceedsOnFreshDatabase`: **PASS**
4. `Migration_Down_RollsBackPlatformAdmin_WithoutFkViolation`: **PASS**
5. `ExecuteInCenterLockAsync_ConcurrentOperationsOnSameCenter_AreSerializedViaPessimisticRowLock`: **PASS** (concurrent `UpdateCenterStatusAsync`, `UpdateCenterMetadataAsync`, and `MakePrimaryCenterManagerAsync` serialized via row lock with 1 winner and 2 OCC conflicts)
6. `UpdateCenterStatusAsync_ConcurrentUpdates_OneWins_OneReturnsConcurrencyConflict`: **PASS**
7. `ResetCenterManagerPassword_ConsecutiveResets_UsingReturnedRowVersions_SucceedsSequentially_And_StaleVersion_ReturnsConflict`: **PASS**
8. `ResetCenterManagerPasswordAsync_RelationalOCCRace_WhenDbUpdateConcurrencyExceptionThrown_ReturnsConcurrencyConflict`: **PASS**
9. `UpdateCenterStatusAsync_Suspended_EvictsSessionsAndRevokesRefreshTokensInMySql`: **PASS**
10. `UpdateCenterStatusAsync_RelationalOCCRace_WhenDbUpdateConcurrencyExceptionThrown_ReturnsConcurrencyConflict`: **PASS**
11. `PlatformCenter_CanonicalPrimaryManagerFields_AreCorrectlyPopulatedAndTransferred`: **PASS**
12. `CreateCenterAsync_AtomicTransaction_PersistsCenterUserRoleAndAudit`: **PASS**
13. `CreateCenterAsync_ConcurrentDuplicateCenterCode_ReturnsDuplicateResource`: **PASS**
14. `CreateCenterAsync_RelationalDuplicateKeyRace_WhenMySql1062Thrown_ReturnsDuplicateResource`: **PASS**
15. `CreateCenterAsync_RelationalDuplicateKeyRace_WhenMySql1062OnOtherIndex_RethrowsException`: **PASS**
16. `PlatformAudit_NullTargetUserId_Succeeds_And_NonNullCrossTenantTarget_ThrowsFkError`: **PASS**
17. `ListCentersAsync_SearchFilter_MatchesManagerUsernameAndDisplayName`: **PASS**
18. `BootstrapPlatformAsync_FailClosed_RejectsMalformedPlatformTenantState`: **PASS**
19. `CheckConstraints_PermitsPlatformAdmin_AndRejectsInvalidRolesOnLiveMySql`: **PASS**
