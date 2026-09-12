# ADR-POST-R08-PLATFORM: Platform Administration, Root Tenant PLATFORM and Center Lifecycle Management

**Status:** APPROVED  
**Date:** 2026-09-12  
**Scope:** Post-R08 Extension — Track 1 (Platform Administration)  
**Author:** EduTwin Architecture Team  

---

## 1. Context and Problem Statement

Following the completion of Release R08 (Hardening, Multi-tenant Verification, and Demonstration Readiness), EduTwin operates as a Shared Database, Shared Schema multi-tenant platform. In the R00–R08 baseline:
- Each educational center is isolated by `center_id` via Global Query Filters.
- Tenant administration is bounded within each center via the `CenterManager` role.
- Centers were provisioned purely via database seeds or administrative migrations without runtime lifecycle management.

To support production onboarding, partner center provisioning, and tenant lifecycle governance (suspension, reactivation, initial credentials reset), a governed Platform Administration capability is required. However, introducing a global administrative role poses severe multi-tenant security and privilege escalation risks if not strictly constrained.

---

## 2. Decision Drivers

1. **Multi-Tenant Invariant Preservation:** The core invariant (CONSTITUTION §8: `center_id` isolation) must remain unbreakable. Platform administrators must not inadvertently or maliciously access student private records, attempts, or pedagogical twins of customer tenants.
2. **Privilege Escalation Prevention:** Tenant administrators (`CenterManager`) must not be able to elevate themselves or subordinate users to platform administrators or assign platform-level capabilities.
3. **Deterministic Provisioning & Zero Hardcoded Secrets:** Platform administrator accounts must be provisioned securely via environment variables (`PlatformBootstrap__AdminPassword`), failing closed if unconfigured, with zero fallback secrets in source code.
4. **Atomic Lifecycle Operations:** Center creation, manager user provisioning, role assignments, and status transitions must execute atomically under explicit concurrency control.

---

## 3. Architectural Decisions

### 3.1. Dedicated Root Tenant `PLATFORM`
- A dedicated, reserved system tenant is established:
  - `CenterId`: `00000000-0000-0000-0000-000000000001` (Reserved Constant `ReservedPlatformCenterId`).
  - `CenterCode`: `PLATFORM`.
  - `CenterName`: `EduTwin Platform Administration`.
- The `PLATFORM` tenant is completely isolated from normal learning operations.
- All mutations targeting `PLATFORM` (e.g., suspension, renaming, deletion) are blocked server-side (`ErrorCodes.ForbiddenResource`, HTTP 403).

### 3.2. Dedicated Account Type and Role: `PlatformAdmin`
- Added `PlatformAdmin` to `UserRole` / Account Type domain.
- Five MySQL CHECK constraints are updated to permit `'PlatformAdmin'`:
  1. `ck_users_role_name`
  2. `ck_roles_account_type`
  3. `ck_permission_account_types_account_type`
  4. `ck_user_roles_account_type`
  5. `ck_role_permissions_account_type`
- Platform capabilities:
  - `platform.centers.read`: View centers and metadata.
  - `platform.centers.manage`: Provision centers, toggle suspension.
  - `platform.managers.manage`: Reset center manager credentials with optimistic concurrency control.
- Invariant: All `platform.*` capabilities have `IsDelegable = false`. They cannot be created, edited, or granted inside ordinary centers.
- Privilege Escalation Guard: Any attempt by a tenant user to create a role with `PlatformAdmin` account type, assign `PlatformAdmin` role, or grant `platform.*` permissions throws `ConflictException` / `ForbiddenException` with `ErrorCodes.AuthPrivilegeEscalation` (HTTP 403 Forbidden).

### 3.3. Independent `PlatformAdminProvisioner`
- Separated entirely from test/demo seeders (`DataSeeder`).
- Runs during host startup if database migrations are applied.
- Reads password from environment variable `PlatformBootstrap__AdminPassword`. If running in `Development` mode, fallback configuration must also be explicitly sourced from environment variables; zero plaintext fallback strings in code.
- Idempotency & Safety: If a valid platform administrator already exists in the `PLATFORM` tenant, the provisioner validates the record and skips provisioning; it **never** resets or overwrites existing administrator passwords automatically. If absent, provisions the user and assigns `PlatformAdmin` role within `PLATFORM` tenant.

### 3.4. Center Lifecycle Management
- **List Centers (`GET /api/v1/platform/centers`):**
  - Caller must possess `platform.centers.read` claim and belong to `PLATFORM` tenant.
  - Executes intentional cross-tenant query using `IgnoreQueryFilters()`, filtering `center_id != ReservedPlatformCenterId` and `is_deleted == false`.
  - Returns canonical model fields: `centerId`, `centerCode`, `centerName`, `status`, `timezone`, `createdAt`, `rowVersion`, `initialManagerUserId`, `initialManagerUsername`, `initialManagerDisplayName`, `initialManagerUserRowVersion` (Change Proposal CP-POST-R08-01: added to provide current manager `users.row_version` for client OCC password reset without separate lookup).
  - Empty State: When 0 customer centers exist, returns HTTP 200 OK with `items: []`, `totalCount: 0`.
- **Create Center (`POST /api/v1/platform/centers`):**
  - Caller must possess `platform.centers.manage` claim and belong to `PLATFORM` tenant.
  - Request fields match existing domain model: `centerCode`, `centerName`, `timezone`, initial manager `username`, `displayName`, `password`.
  - Transactional boundary: Inserts `Center`, initial `User` (`CenterManager`), provisions default roles for the new center (`CenterManager`, `Teacher`, `Student`), and assigns `CenterManager` role to the initial user.
- **Center Status Transition (`PATCH /api/v1/platform/centers/{id}/status`):**
  - Caller must possess `platform.centers.manage` claim.
  - Allows toggling `Active` $\leftrightarrow$ `Suspended`.
  - Concurrency token: Enforces Optimistic Concurrency Control (OCC) via `row_version`.
  - Target protection: Mutating `PLATFORM` center is strictly forbidden (`ErrorCodes.ForbiddenResource`).
- **Center Manager Password Reset (`POST /api/v1/platform/centers/{centerId}/managers/{managerUserId}/reset-password`):**
  - Caller must possess `platform.managers.manage` claim and belong to `PLATFORM` tenant.
  - Request fields: `newPassword`, `expectedUserRowVersion`.
  - Validates `expectedUserRowVersion` matches manager's current `users.row_version` to prevent clobbering concurrent user edits.
  - Updates password hash, increments `users.row_version`, atomically increments `users.auth_version` to invalidate all active JWT sessions, and bulk revokes all active refresh tokens for the user by setting `revoked_at = utcNow`.

### 3.5. Data Boundary & Privacy Guarantee
- `PlatformAdmin` possesses administrative metadata authority only.
- `PlatformAdmin` cannot read:
  - Student attempt responses, reasoning texts, or scratchpad attachments.
  - AI reasoning analysis records or Evidence assessments.
  - Learning Digital Twins (Knowledge Twin, Behavior Twin) or Personalized Recommendations.
- Global Query Filters in standard BLL use cases prevent access unless an explicit tenant context is bound.

### 3.6. Platform Authorization Audit Invariant & Cross-Tenant Isolation
- In `authorization_audit_logs`:
  - `center_id` is strictly the Root Tenant `PLATFORM` (`00000000-0000-0000-0000-000000000001` / `ReservedPlatformCenterId`).
  - `actor_user_id` is the `PlatformAdmin` user ID (belonging to tenant `PLATFORM`).
  - **Cross-Tenant Target Isolation (`target_user_id = null`):**
    - The database enforces a tenant-safe composite foreign key `(center_id, target_user_id) REFERENCES users(center_id, user_id)`.
    - Because the targeted entity (such as a customer center or its `CenterManager`) belongs to a different center, storing that user ID in `target_user_id` would violate the composite foreign key.
    - Therefore, cross-tenant platform actions MUST persist `target_user_id = null`.
    - The target entity identity is captured unambiguously via `target_type` (e.g. `'Center'`, `'CenterManager'`), `target_id` (e.g. `{centerId}` or `{centerId}:{managerUserId}`), and redacted metadata (`before_data` / `after_data`).
  - **Credential Privacy Invariant:**
    - Absolutely NO plaintext passwords, new passwords, temporary credentials, password hashes, or raw bearer tokens are ever written to `authorization_audit_logs.before_data`, `after_data`, or `reason`.

---

## 4. Consequences and Verification

### Positive:
- Secure, auditable onboarding of new learning centers.
- Zero credential leaks into source control.
- Concurrency-safe state transitions prevent conflicting operations.

### Negative / Complexity:
- Cross-tenant queries in `PlatformCenterService` must use `IgnoreQueryFilters()` explicitly with rigorous manual filtering.
- Requires maintenance of 5 MySQL CHECK constraints across database migrations.

### Verification Plan:
- Unit & integration tests in `PlatformCenterServiceTests`, `PlatformTenantIsolationTests`, `PlatformPrivilegeEscalationTests`, `PlatformAuditCrossTenantTests`, and `PlatformAdminMigrationTests`.
- Database constraint verification: Cross-tenant audit insertion with non-null `target_user_id` fails FK; audit insertion with `target_user_id = null` succeeds and redacts sensitive credentials.
- E2E tests verifying token invalidation on password reset and HTTP 403 on tenant escalation attempts.
