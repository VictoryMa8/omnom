# Omnom developer guide

Architecture reconciled with the working tree on **October 20, 2026**. This describes the active implementation; current external provider availability and production deployment state require separate checks. See [README.md](README.md) for startup, [AGENTS.md](AGENTS.md) for contributor rules, and [ROADMAP.md](ROADMAP.md) for planned work.

## Architecture and data flow

The Vue SPA builds into the .NET API's `backend/wwwroot/`. Development runs Vite on port 5173 with `/api` proxied to the backend HTTP profile on port 5027. Production can serve the SPA and API from one process/container.

The active client stores diary data in the browser. The server performs extraction and food-reference lookup and retains legacy private diary/settings endpoints. These are separate persistence paths:

```text
Browser: App.vue + Pinia
  Text/voice → POST /api/ai/parse → MealParserService
                                  ├─ AI mode: OpenRouter → validated JSON
                                  │           or local heuristic fallback
                                  ├─ Exact mode: deterministic local extraction
                                  └─ local references; optional USDA in AI mode
  Review ← parsed foods, source IDs, assumptions, clarification chips
  Replace food → GET /api/foods/search → local/cached FoodReferences
  Confirm → diaryStore → localDiary → browser localStorage
  Timeline / targets / favorites / weekly view ← localDiary

Server database: reference/cache records, settings, request counters,
                and legacy private meal/target records
```

Saving, editing, deleting, repeating, or favoriting a meal in the current UI does not call `/api/diary`. `DiaryApi` and `TargetsApi` are aliases for `localDiary` in [api.ts](frontend/src/services/api.ts). There is no account ownership model or cross-device sync.

## Server startup and configuration

[Program.cs](backend/Program.cs) loads configuration, chooses a database, registers typed HTTP clients and access/budget filters, initializes reference data, then serves controllers and static SPA assets. Swagger and permissive development CORS are enabled only in Development.

[EnvironmentFile.cs](backend/Services/EnvironmentFile.cs) loads `.env` in the current directory, or its parent when running from `backend/`. Already defined environment variables, including empty values, win over that file. ASP.NET configuration uses `Jwt__Secret` for `Jwt:Secret`.

The supported local launch command is:

```sh
dotnet run --project backend/Omnom.Api.csproj --launch-profile http
```

The checked-in HTTP profile binds `http://localhost:5027`. [vite.config.ts](frontend/vite.config.ts) proxies there. `--launch-profile https` additionally binds `https://localhost:7252`; HTTPS development certificates are not needed for the documented HTTP workflow. If you override backend URLs, update the proxy too. Use `--strictPort` for Vite so a busy port does not silently create another browser origin/diary.

The README's [configuration table](README.md#configuration) lists settings. For reproducible no-provider work, use the explicit [isolated startup](docs/verification/OM-001-local-startup.md#isolated-startup) rather than inheriting real keys or a hosted database from `.env`.

## Server storage and access

[AppDbContext.cs](backend/Data/AppDbContext.cs) exposes `MealEntries`, `MealItems`, `DailyTargets`, `FoodReferences`, and `UserSettings`. [RequestBudget.cs](backend/Services/RequestBudget.cs) uses a separate SQL `RequestBudgets` table.

| Entity | Meaning |
|---|---|
| [MealEntry](backend/Data/Entities/MealEntry.cs) | Legacy server meal with `DateOnly` date, nullable time, totals, and item relationship |
| [MealItem](backend/Data/Entities/MealItem.cs) | Food/nutrients associated with a server meal; cascade deletion |
| [DailyTarget](backend/Data/Entities/DailyTarget.cs) | Effective-date server targets |
| [FoodReference](backend/Data/Entities/FoodReference.cs) | Seeded staple or cached USDA record with per-100g nutrition and default serving metadata |
| [UserSetting](backend/Data/Entities/UserSetting.cs) | Legacy key/value settings; old OpenRouter values do not control current inference |

Empty `DATABASE_URL` selects SQLite at `DATA_DIR/omnom.db`, or the process working directory when no data directory is provided. A nonempty URL selects PostgreSQL through [HostedDatabase.cs](backend/Services/HostedDatabase.cs). `DATABASE_URL_UNPOOLED`, when supplied, is used for startup initialization. PostgreSQL initialization takes an advisory lock to serialize concurrent schema creation/seeding.

[DbInitializer.cs](backend/Data/DbInitializer.cs) uses `EnsureCreated`, creates the request-counter table, applies targeted SQLite time-nullability compatibility SQL, and seeds settings/targets/reference foods. This is not a general EF migration system. New schema work must account for existing data and both providers instead of assuming `dotnet ef database update` is sufficient.

[PasscodeAccess.cs](backend/Services/PasscodeAccess.cs) implements legacy PIN comparison, JWT creation/validation, and the authorization filter. It does not use the earlier described BCrypt/AuthService architecture. A configured secret or process-local signing material is combined with the PIN so changing it invalidates sessions. This PIN system protects shared legacy server endpoints; it is not per-user authorization.

| Mode | Accessible endpoints |
|---|---|
| `PUBLIC_DIARY=true` | `/api/ai/parse` and `/api/foods/search`; legacy diary, targets, settings, and auth endpoints return 404 |
| Public mode off, no PIN | Legacy endpoints and parse/search are accessible locally |
| Public mode off, PIN configured | Auth endpoints allow status/unlock; other controller actions require a valid bearer token |

Forced hosted PIN mode (`REQUIRE_PASSCODE=true` with public mode off) requires `APP_PASSCODE`, `Jwt__Secret`, and `DATABASE_URL` at startup. The current shell does not mount `PasscodeModal` or run the legacy unlock flow, so this is not the default setup for browser-diary users. Public mode does not automatically expose a login endpoint.

Request budgets are shared across instances through the server database: parsing is capped at 30 requests/hour and unlock attempts at 10/minute. The controller caps meal descriptions at 4,000 characters. These are application settings in the inspected code, not provider quotas or per-user entitlements.

## Parsing and nutrition contracts

[AiController.cs](backend/Controllers/AiController.cs) accepts `AiParseMealRequest` at `POST /api/ai/parse` with `prompt`, optional `mealTypeHint`, and `mode` (`ai` or `exact`). [AiParsingModels.cs](backend/Models/AiParsingModels.cs) defines foods, assumptions, match status, and clarification replacement/additive fields. [DTOs.cs](backend/Models/DTOs.cs) defines reference-search and legacy endpoint contracts. Keep matching TypeScript interfaces in `api.ts` synchronized.

### AI mode

[OpenRouterService.cs](backend/Services/OpenRouterService.cs) reads only the server's `OPENROUTER_API_KEY`. It ignores client-requested models and old database AI settings. As inspected October 20, 2026, the source configures `inclusionai/ling-3.1-flash` followed by `apodex/apodex-1.1-mini:free`. Every attempt requires zero maximum prompt/completion/request/image prices and requests reasoning with a 6,000-token shared budget. The backup requests JSON-object mode. The October 20 [OM-004 preflight](docs/verification/OM-004-safe-diagnostics.md) found paid endpoints for Ling and none listed for Apodex; live evaluation was deferred with zero completion requests. The [intelligence review](docs/intelligence-review.md) retains older provider evidence.

[MealParserService.cs](backend/Services/MealParserService.cs) preserves first-open/last-close JSON extraction and fence handling, then validates items and excerpts against the input. Invalid/empty/truncated responses and unavailable providers can trigger backup/fallback. A source excerpt does not prove that every food or amount was interpreted correctly.

Explicit mass conversions are deterministic where supported. Meaningful food qualifiers survive normalization and constrain matches. Multi-weight corrections, volume interpretation, broad branded matching, and mixed-dish accuracy remain roadmap work; do not claim the model resolves all ambiguity.

If AI fails or has no key, heuristic parsing runs and `aiSummary` explains fallback. Unmatched AI foods can retain usable model estimates labeled `Estimated`. Unknowns without usable nutrition stay `Unparsed` rather than inheriting arbitrary calories. Known zero-calorie drinks are handled separately.

### Exact mode

[ParseDiagnostics.cs](backend/Services/ParseDiagnostics.cs) emits flat structured `ParseDiagnostic` events (event ID 4004) with a shared correlation ID from the ASP.NET activity trace, or a generated ID outside HTTP. Events cover model attempts, extraction, nutrition matching, remote USDA lookups, total parsing, and API deadline/rate-limit failures. They contain fixed outcome categories, elapsed milliseconds, item/source counts, approved model IDs, HTTP status, and nullable token/cost metadata. Missing usage stays unknown; arbitrary actual-model strings become `other`. Exceptions, prompt/search text, URLs, credentials, provider bodies, and reasoning are excluded. Both typed HttpClients remove their default URL loggers even if HTTP logging is enabled. No external analytics or diary storage is added. The [evaluation guide](tests/fixtures/meal-evaluation/README.md#safe-diagnostics-and-latency-om-004) describes collection, sample counts, repetitions, and nearest-rank percentiles.

[MealParserService.Amounts.cs](backend/Services/MealParserService.Amounts.cs) shares literal correction, exclusion and amount rules across Exact, heuristic fallback and AI-output validation. An immediately following `actually` / `make that` numeric amount replaces one prior amount; `more` adds a separate portion and can inherit a food only from the immediately preceding matching unit. Unsupported food-changing corrections, multiple weights and ambiguous container arithmetic remain unresolved. Negated excerpts cannot ground AI foods. Explicit mass wins; numeric `ml`, litres and US `fl oz` stay volume. Recognized reference beverages disclose the approximate `1 g/ml` density assumption, while missing density or container size needs review. Checked reference serving weights handle matching slice/scoop/container units. Remembered nutrition cannot turn drink ounces into mass ounces or fill an unresolved volume with an unrelated usual portion. Original source text stays on the review/saved item. See [OM-005 evidence](docs/verification/OM-005-corrections-and-units.md).

Exact mode uses deterministic extraction and local reference matching. It does not call OpenRouter or remote USDA, but still requires the backend API and applies its request budget. Unrecognized fragments remain visible for manual correction/replacement; Exact is not a guarantee that arbitrary natural language will parse.

### Reference matching and clarification

[UsdaFoodService.cs](backend/Services/UsdaFoodService.cs) scores seeded/cached references with identity/preparation/qualifier safeguards. AI-mode misses may query USDA Foundation, SR Legacy, and Survey records. `/api/foods/search`, implemented in [FoodsController.cs](backend/Controllers/FoodsController.cs), searches only local/cached rows. Remote branded search is not yet implemented.

Reference records provide per-100g nutrients scaled by the selected grams. Review exposes `VerifiedStaple`, `UsdaApiMatch`, `Estimated`, and `Unparsed` statuses with user-facing source labels. Editing nutrition or just renaming a food must not retain an unwarranted reference claim. Food replacement updates calories, protein, carbs, fat, fiber, source IDs, and incompatible chips at the current weight.

Preparation/lean choices appear only when suitable reference records exist. Cooking-fat chips can add reference-backed ingredients; repeatedly changing a chip must not accumulate duplicate oil/butter. Unsupported variants remain unresolved instead of using arbitrary raw/cooked multipliers.

### Time budgets

[ParseLimits.cs](backend/Services/ParseLimits.cs) defines a 40-second shared AI budget, 50-second API deadline, an 8-second window for starting remote nutrition lookups, and 2 seconds per USDA lookup. The primary model has a 15-second cap; backup gets the remaining shared AI budget. Axios allows 55 seconds. Preserve cancellation propagation and bounded work when changing this pipeline.

## Browser persistence and state

[localDiary.ts](frontend/src/services/localDiary.ts) validates and stores version-1 JSON under `omnom_diary_v1`. Data includes meals, effective-date targets, next meal ID, and optional `favorites`/`foods`. Browser profile and origin define isolation; this is neither encryption nor account ownership. Root server database backups do not include it.

- Each meal has an ID and local calendar date. Timeline order is ascending ID, independent of optional time.
- Totals, including fiber, are recalculated from foods; running totals follow insertion order.
- Targets resolve by latest effective date not after the selected date, with descending ID breaking same-day ties. The browser default predates all real dates.
- Seven-day ranges end at the selected date. Averages include logged days only and goals use each day's effective target.
- Favorites snapshot confirmed foods. Normalized exact aliases can reuse checked nutrition; remembered portions are opt-in and explicit weights override them.
- Save/edit/delete return Undo functions. Snapshot/ID guards prevent Undo from blindly undoing a changed meal. Toast actions expire after 12 seconds, paused on hover/focus.
- Export preserves browser data. Import validates before replacing it and clears active Undo actions. Existing version-1 files without optional library fields remain readable.
- Read failures do not silently clear data; quota/disabled-storage writes report errors. Recovery previews and cross-tab concurrency are OM-008, not existing guarantees.

[diaryStore.ts](frontend/src/stores/diaryStore.ts) owns selected date, daily/weekly data, meal library, busy/error state, and mutations. Request sequencing ignores stale timeline responses. Use local year/month/day when formatting diary dates; UTC `toISOString()` can shift the intended day. Target changes pass the selected date explicitly.

## Active UI and design

[App.vue](frontend/src/App.vue) coordinates parse/review/edit sessions, cancellation, remembered defaults, entry-date capture, loading states, and global toasts. Its current component tree includes:

| Component | Responsibility |
|---|---|
| [QuickLogInput](frontend/src/components/QuickLogInput.vue) | AI/Exact toggle, text, browser speech input, Enter/Shift+Enter, quick food descriptions |
| [MealConfirmCard](frontend/src/components/MealConfirmCard.vue) / [FoodSearch](frontend/src/components/FoodSearch.vue) | Review, gram/nutrient edits, reference replacement, optional chips, remembered foods, optional time |
| [DailySummaryHeader](frontend/src/components/DailySummaryHeader.vue) / [QuickTargetModal](frontend/src/components/QuickTargetModal.vue) | Calorie ring, macro progress, target presets/edits |
| [DateNavigator](frontend/src/components/DateNavigator.vue) | Local diary date navigation |
| [TimelineFeed](frontend/src/components/TimelineFeed.vue) / [MealCard](frontend/src/components/MealCard.vue) | Saved meals, details, running totals, edit/repeat/favorite/delete actions |
| [MealLibrary](frontend/src/components/MealLibrary.vue) | Recent/favorite meals and remembered foods |
| [WeeklyProgress](frontend/src/components/WeeklyProgress.vue) | Seven-day logged-day averages, protein goals, daily navigation |
| [BrowserDataModal](frontend/src/components/BrowserDataModal.vue) | Settings button's active backup/export/restore dialog |

`SettingsModal.vue`, `PasscodeModal.vue`, `settingsStore.ts`, and `authStore.ts` remain legacy client modules, not children of the active shell. Axios still contains legacy auth/settings methods and token handling; their presence does not mean the shell uses those flows.

[style.css](frontend/src/style.css) supplies the active light background, white cards, green primary actions, responsive layout, focus states, and reduced-motion rules. [tailwind.config.js](frontend/tailwind.config.js) retains older dark-theme tokens/aliases. Do not reconstruct a dark UI merely from those unused tokens. Keep concise athletic copy, visible labels, keyboard access, loading/error feedback, and optional times; avoid emoji clutter.

## Verification and releases

Use the README's startup commands. Required automated checks are:

```sh
dotnet test tests/Omnom.Tests
npm --prefix frontend test
npm --prefix frontend run build
```

xUnit covers deterministic extraction, matching, server configuration/routing, clarification references, serialization, access, and controller behavior. Frontend Node regressions exercise extracted component scripts, local persistence, dates/targets, defaults, references, Undo, and store races. They are not a full rendered-browser suite. [ComplexPromptDiagnostics.cs](tests/Omnom.Tests/ComplexPromptDiagnostics.cs) prints diagnostic output and is not a substitute for corpus assertions.

The build runs TypeScript checks and writes the Vue bundle into `backend/wwwroot/` with `emptyOutDir`. Always rebuild after frontend `.vue`/`.ts` edits. Static assets are served by the backend with SPA fallback; this app currently uses component/date state rather than Vue Router.

For visible changes, verify the relevant browser → API → reference → review → browser-storage flow. Use a disposable browser profile/origin and isolated database. Record actual commands, revision/dirty-tree context, skipped checks, and results. See [OM-001 verification](docs/verification/OM-001-local-startup.md); OM-002 provides the [executable desktop/mobile smoke suite](frontend/tests/smoke/README.md), and OM-003/OM-004 track offline/live accuracy measurement.

[Dockerfile](Dockerfile), [Dockerfile.vercel](Dockerfile.vercel), [docker-compose.yml](docker-compose.yml), and [vercel.json](vercel.json) are the checked-in hosting definitions. Container SQLite volumes preserve server records, not browser data. Configure hosted database/key values through the intended deployment environment. Runtime availability/pricing and deployment protection must be checked against that environment before release; local verification does not establish production status.
