# Local Docker refresh — 2026-10-06

Scope: update the existing primary Docker runtime and database schema. The user
requested stopping browser/E2E work and will perform manual UI acceptance.

## Delivered runtime

- Primary web/API rebuilt from the current working tree (including uncommitted
  corrective code), not merely from the last Git commit. Both containers match
  their latest built images.
- Web: http://localhost:3000 — HTTP 200.
- API: http://localhost:5000 — reachable through the web proxy; invalid test
  credentials return structured HTTP 401, not a proxy error.
- MySQL: localhost:3307 — healthy, existing `edutwin_mysql_data` volume retained.
- Adminer: http://localhost:8080 — running.
- All **33/33 source EF migrations** are applied; no missing or extra migration.
  Reinitializing MySQL or importing the baseline SQL over live data was unnecessary.
- `edutwin-acceptance-web` / `edutwin-acceptance-api` and their owned disposable
  schema/user were removed. Remaining `edutwin_e2e_%` schemas: **0**.

## Approved local data repair

The exact orphan chain left by the 2026-10-05 test (attempt 1) was removed after
a full SQL backup and explicit user approval. No general orphan purge was run.
Foreign keys stayed enabled; the original evidence append-only delete trigger
was restored. Final orphan counts for analyses, evidence, twin history, generated
paths and knowledge-twin references are all **0**.

Backup (local, not committed):
`C:\Users\ACER\AppData\Local\Temp\EduTwin-backups\20261006_before_e2e_repair_b98bb2b69b9d4ce293eebf89c46858df.sql`

SHA256: `243bbfcbf012cf50b8d7e83ed37f2045d22ea8e9dcb7f6fd73d39fae40acd1c0`.

## Corrections and verification completed before the stop request

- Mapped assignment unavailable / missing reasoning / reused upload token to
  structured HTTP 409 / 422 / 409, instead of unhandled HTTP 500.
- Added three controller regression cases; targeted unit run: 15 passed, two
  optional live-MySQL cases skipped. A subsequent attempt to enable those two
  cases failed during runner connection-string parsing (`SslMode=None` is not
  supported by this driver); it is **not** claimed as a passing run.
- Frontend verify: 443 tests passed, lint 0 errors / 32 warnings, production build
  passed, bundle budget 3/3 passed.
- Isolated real HTTP/MySQL acceptance finished 24/24 before its cleanup. This is
  **not Chrome E2E acceptance** and exercises provider fallback without a Gemini
  API key. An earlier failed run is retained with its FAIL outcomes for traceability.
- Unsafe legacy browser diagnostics were disabled before browser startup: their
  old “100%” statement did not establish successful actions or terminal feedback.
- No further E2E execution was performed after the user requested manual testing.

At the end of that maintenance turn, changes remained local and uncommitted;
no Git push was performed then. Manual UI acceptance and live Gemini-quality evaluation remain
with the user, not represented as completed here.

## Follow-up: remove two demo assignments (user request)

Removed only fixed IDs `a1111111-1111-1111-1111-111111111111` and
`a2222222-2222-2222-2222-222222222222`: two assignments, two target rows, two
synthetic progress rows, and five assignment-question links. No attempts existed
for these assignments. Account/class/subject/question/center/migration inventory
was verified unchanged, and foreign keys remained enabled. API was paused for
the maintenance window and restarted afterward; no browser/E2E test was run.

Pre-deletion full backup:
`C:\Users\ACER\AppData\Local\Temp\EduTwin-backups\20261006_before_demo_reset_a0fb414c4c2b49b4a27f5f5f1a782627.sql`

SHA256: `1c1658895ea1f80f16a5787f1773ec8de77b4788506a1da092f4b9256c3c9d5a`.

## Follow-up: question numbering and submission HTTP 500

- Teacher quick view now uses the API's one-based `orderIndex` directly: two
  questions display as 1 and 2, rather than 2 and 3. Filtering preserves the
  original assignment numbering.
- API logs traced the user's HTTP 500 to the scratchpad-token reuse check:
  MySQL EF 10 could not assign a type mapping to the `nonceList.Contains(...)`
  collection parameter. Replaced it with a single tenant-scoped query composed
  of scalar comparisons; token validation and replay protection are retained.
- Frontend verify: **446/446 passed**, lint **0 errors / 32 existing warnings**,
  TypeScript/production build passed, bundle budget **3/3 passed**.
- Targeted backend run: **14 passed / 2 optional live-MySQL tests skipped**.
  The regression tests compile the query with the actual MySQL provider without
  connecting to the user's database, and verify tenant-scoped matching in memory.
  The initial `EF.Constant` attempt failed provider compilation and was replaced;
  it is not represented as a successful fix.
- API and web were rebuilt and recreated in the existing primary stack. Their
  running image IDs match the new images; attachment and data-protection volumes
  remain mounted. No database migration was needed (still **33 applied**).
- Read-only inventory confirmed the current assignment retains one in-progress
  student with a stored draft and four not-started students. No real assignment
  was submitted, no user data was rewritten, and no Chrome/E2E run was performed.
  The user will retry submission for runtime acceptance.

## Subsequent grading repair and delivery

The user successfully submitted `Test 1` during manual testing. The separate
`POST_R09_METHOD_AGNOSTIC_GRADING_20261006.md` report records the mathematical
equivalence correction, the 34th forward migration, current Docker images and
the final automated verification (3,674 BLL tests passed; 456 frontend tests
passed). The user then authorized committing/pushing the verified corrections
to `student/answer`; historical 33-migration results above describe the earlier
runtime, not the current schema. Local diagnostic artifacts are not part of
that Git delivery.
