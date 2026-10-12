# Math input corrective recovery — 2026-10-04

## Scope and recovered work

Gemini stopped with uncommitted changes in VisualMathField.tsx,
visualMathFieldLifecycle.ts, and visualMathFieldToggles.test.ts. Its selection-state
helper and selection-aware imperative insertion were retained and completed.

- All public insert/executeCommand guards and toolbar/Casio insertion now share
  production insertion preparation. Selected expressions retain `#0`; empty templates
  use `#?`. Original command options and trailing arguments are preserved.
- Text-mode insertion bypasses mathematical template conversion and retains literal
  text, hashes, pipes, and whitespace.
- Regression tests call the production guards rather than a duplicated wrapper.
- The bundle budget resolves the actual module entry from dist/index.html, not the
  first filename beginning with index-. The 150 KiB entry budget was not relaxed.
- Added `npm run test:math-browser`: isolated Vite + real React VisualMathField +
  installed MathLive, native mouse/key events, exact structure/state assertions,
  disposable Chrome profile, and fail-closed cleanup. No login credentials required.

## Independent verification

| Check | Result |
| --- | --- |
| Frontend unit tests | 415 passed, 0 failed |
| ESLint | 0 errors, 31 pre-existing warnings; no warning in added files |
| TypeScript + Vite build | Passed; entry approximately 105.97 kB (decimal) |
| Bundle tests | 3 passed, 0 failed; entry below 150 KiB |
| BLL tests | 3562 passed, 57 skipped, 0 failed; 3619 total |
| Real Chrome component smoke | 7 exact behavior checks passed |
| Git whitespace check | Passed |
| Docker frontend | Built and recreated with `--no-deps`; API/MySQL untouched |

Chrome checks: radical typing, exiting a radical, absolute-value typing, wrapping a
real Ctrl+A selection, fraction Tab navigation, clicking the real virtual-keyboard
radical key then typing, and exact Vietnamese/plain-text onChange payload.

This is component integration/browser verification, not full actor/API E2E. The
57 skipped BLL tests require separately configured live relational infrastructure.
The existing deferred MathLive chunk warning (>600 kB) remains; this work did not
upgrade dependencies or remediate the npm audit findings reported by npm ci.

## Runtime confirmation

HTTP localhost:3000 now serves `/assets/index-CxBmUcU6.js` and
`ModeAwareAnswerEditor-CKrFNy_y.js`. The latter contains the new selection-aware
insertion code. Previously served `ModeAwareAnswerEditor-C04-KnTq.js` was stale.
Windows and Linux build hashes differ; runtime confirmation uses the Docker build.

## Authorized local history cleanup

Eight unpublished commits were recreated, preserving authors, messages, dates,
the final tracked tree, and all uncommitted edits. Only the historical hardcoded
connection-string fallback was removed. No remote refs were changed and no push ran.

| Original local SHA | Sanitized local SHA |
| --- | --- |
| f88ebf5 | 6cdfa47 |
| cf72372 | e2c9f04 |
| 99f86bd | f0bce0d |
| bbcb611 | 7c92d81 |
| 9d53741 | 8bb9d48 |
| 64a23d1 | e9a6cf7 |
| b458442 | 374c81d |
| 0ae429d | c5cc29c |

The affected test file was checked across all eight reachable branch snapshots:
zero remaining inline `Password=` values. Original objects may remain in local
reflogs; they are not ancestors of the branch to be pushed. If the old credential
is active or has been shared elsewhere, rotate it separately. No database password
was changed in this corrective recovery.
