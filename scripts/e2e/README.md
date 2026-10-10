# Assignment acceptance — isolated runtime only

The old `test_student_experience.cjs`, `test_browser_flow.cjs`, `test_page_render.cjs`,
`smoke_test.cjs`, and `debug_player.cjs` are historical diagnostics, **not acceptance
tests**. Their shared `ChromeClient` fails closed before launching a browser. They
logged missing actions without assertions, used the primary runtime, and could
mislabel an AI-loading screen as completion. Do not use their old screenshots as
evidence that these actions passed.

## Reproducible API/MySQL acceptance

From the repository root, with the current Docker images built:

```powershell
node scripts/e2e/acceptance_stack.cjs start
try {
    node scripts/e2e/assignment_api_acceptance.cjs
    if ($LASTEXITCODE -ne 0) { throw 'Acceptance failed; inspect api_results.json.' }
    # Perform the browser checks below before stopping the temporary stack.
} finally {
    node scripts/e2e/acceptance_stack.cjs stop
    if ($LASTEXITCODE -ne 0) { throw 'Cleanup failed; recover the owner manifest.' }
}
```

- Disposable web/API: `http://localhost:3002` / `http://localhost:5001`.
- Fresh schema `edutwin_e2e_<runId>`, separate DB user, JWT key, and storage.
- Only temporary seed accounts use `LocalTestOnly-20261006!`; no primary password
  is read by browser tests.
- Gemini credentials are deliberately absent: assertions exercise provider
  fallback, not the quality or availability of the live Gemini service.
- The owner manifest is stored in the OS temporary directory. Startup/cleanup
  refuses mismatched schema/user/container ownership. Cleanup drops only this
  disposable schema; it never disables primary foreign keys or audit triggers.
- `api_results.json` records individual PASS/FAIL outcomes and exits nonzero for
  any failure. These are real HTTP/MySQL integration checks, **not Chrome E2E**.

## Browser checks (separate evidence)

Sign in to the disposable web as `student02` in center `EDUTWIN_A`. Open the
`chromeServerHydrationUrl` printed by the API suite. Its draft was saved through
the API before the browser ever opened this assignment: seeing the seeded
answer/reasoning now proves server hydration rather than merely local cache.

Check initial answer/reasoning, switch questions, edit reasoning and wait for
the saved indicator, reload, confirm timer continuation and answer persistence,
then submit the whole assignment and wait for a terminal feedback screen.
Capture each verified state; do not label loading as completion. Scratchpad
upload, offline retries, multi-tab interaction, and mobile layout require their
own browser evidence; API tests alone do not prove those UI interactions.

Use only the browser-control mechanism approved for the current agent. The
deprecated diagnostic scripts must not be silently re-enabled to generate a
green report. Rebuilding MySQL is not a database upgrade: the API applies EF
migrations to the existing named volume at startup.
