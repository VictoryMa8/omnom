# OM-002 release smoke checks

This suite loads the **built production SPA served by the actual .NET API**. It does not use the in-memory preview page. It runs ten scenarios twice: Chromium desktop (1280×900) and a narrow mobile viewport (390×844, touch enabled), with America/Chicago dates and a fixed synthetic application date of October 20, 2026.

## Install and run

From the repository root, install the README's .NET/Node prerequisites, then:

```sh
npm --prefix frontend ci
npm --prefix frontend run test:smoke:install
npm --prefix frontend run test:smoke
```

The browser install is needed once and again when the locked Playwright browser revision changes. Linux runners also need Chromium's system libraries; install those through your runner's supported Playwright setup. The suite uses its managed Chromium revision rather than an arbitrary locally installed Chrome. Network is needed to restore/install packages and download the browser; the application checks themselves use local data and stubbed providers.

`test:smoke` type-checks/builds the frontend into `backend/wwwroot/`, builds the backend, then runs the suite. It requires permission to start loopback listeners and Chromium. Port **5187** must be free. To use another available loopback port:

```sh
OMNOM_SMOKE_PORT=5188 npm --prefix frontend run test:smoke
```

The port is validated and the runner refuses to reuse an existing server. There is no production/base-URL override. `server.mjs` creates a new temporary SQLite directory, explicitly clears AI/database/PIN environment values even when a repository `.env` exists, sets public mode, and starts the built .NET app. The runner explicitly uses graceful SIGTERM shutdown so the wrapper can stop .NET and remove that temporary database. A forcibly killed process/host crash can leave a temporary `omnom-smoke-*` directory; inspect and remove only that run's test directory if necessary. Every test has a fresh browser context, so it never clears or imports into a user's real browser diary.

## What is verified

| Scenario | Boundary/evidence |
|---|---|
| Real API Exact parse → review → save → reload | Actual controllers, parser, seeded SQLite references, source IDs, grams, every returned nutrient, selected date, optional time, and browser persistence; private diary/target/settings/auth endpoints return 404 in public mode |
| Corrections and drink units | Real Exact API corrections replace earlier weights; US fluid-ounce/density assumptions remain visible and survive save/reload; unknown bottle size prevents saving |
| Review replacement | Stubbed parse/search contracts; changing food updates calories, all macros, fiber and provenance; remembered defaults survive reload |
| Meal library and Undo | Favorite snapshots survive original edits/reload; repeat, recent meals, edit/delete/save Undo recalculate daily/weekly data and preserve original date/position |
| Remembered portions | Unresolved alias uses checked nutrition; explicit mass overrides usual portion; totals persist |
| Targets and navigation | Same-day target edits, historical effective dates, missing days, rapid date navigation, goals dialog focus trap/Escape/return focus |
| Backups | Download contains meals/library; invalid/oversized imports cannot overwrite; valid old-style version-1 restore persists; backup dialog keyboard behavior |
| Storage write failures | Injected quota errors prevent both meal save and restore, preserve existing data, and do not show a false success |
| Storage read/corruption failures | Error remains visible, corrupt raw data is exportable and never silently cleared |
| Feedback and responsive controls | Parse failure/loading states, searchable references and search errors, named inputs/buttons, no horizontal overflow on both viewports |

Synthetic search/parse fixtures in `fixtures.mjs` have test-only food and FDC IDs; they are not real USDA nutrition claims. The remaining real API case selects Exact and known local references, so OpenRouter and remote USDA are not called. Browser requests outside loopback are blocked. No live AI key is needed or used. This tests UI/API contracts and persistence, **not** live model accuracy. Backend provider stubs and controlled stale timeline promise/error tests remain in the xUnit and Node suites. The browser navigation check verifies rapid real UI navigation, while Node regressions deterministically exercise stale resolutions.

## Reports and failure diagnosis

Git, Docker, and Vercel exclude the transient report directory. Each run overwrites these local artifacts in `frontend/.smoke/`:

- `summary.json` / `summary.md`: revision, dirty working-tree paths, app version, time, Node/.NET/Playwright versions, command, per-test result/duration/browser version, skipped tests, and limitations.
- `results.json`: full Playwright results and project configuration.
- `html/`: interactive report.
- `server.log`: the isolated server's output.
- `artifacts/`: failure screenshots, DOM context, and retained traces.
- `real-api-desktop.png`, `real-api-mobile.png`, `om-005-units-*.png`, and `controls-*.png`: successful smoke screenshots.

Open the HTML report from the frontend directory with `npm exec -- playwright show-report .smoke/html`. A failing result's output provides the trace command. Report a failure and inspect the screenshot/context/trace before changing assertions; do not hide a real regression with retries or skipped tests. Retries are disabled and the release gate requires **zero failed, timed-out, interrupted, or skipped cases**. Record environment/install failures separately from application failures.

## Release checklist

1. Run `dotnet test tests/Omnom.Tests` and `npm --prefix frontend test`.
2. Run `npm --prefix frontend run test:smoke`; require all desktop/mobile cases to pass. This command includes the required frontend build.
3. Review `summary.json`, `results.json`, screenshots, and server output. Confirm no unexpected real-provider work, failures, or skipped cases. Full `git status` in the report describes local changes; a SHA alone does not identify an uncommitted release.
4. Save a dated, source-controlled report under `docs/verification/` with exact commands, totals, relevant evidence, limitations, and links to the source. Keep large transient traces/downloads ignored. Never publish real provider credentials, auth state, or personal diary backups.
5. For an authorized deployment, follow [RELEASES.md](../../../RELEASES.md): allocate a new beta version before the final release build. Local test runs do not increment the version or deploy. Inspect the deployed version separately; this suite's pass is not proof of production deployment, real-device/Safari coverage, hosted PostgreSQL, or live AI accuracy.
