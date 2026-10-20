# AGENTS.md — Omnom contributor guide

Read [ROADMAP.md](ROADMAP.md) before selecting future work. Use stable `OM-xxx` IDs and update status, acceptance criteria, and verification evidence in the same change. [README.md](README.md) contains setup; [DEV_README.md](DEV_README.md) explains the current architecture. This guide was reconciled with the working tree on October 20, 2026.

## Product rules

1. **Fast natural-language logging:** support text/voice descriptions and an editable review. Keep clarification chips optional; avoid multi-turn chat as the primary logging flow.
2. **Time is optional:** do not require meal time, infer meal names from the clock, or sort meals by clock time. Preserve insertion order and original dates/positions when editing or undoing.
3. **Ground nutrition:** prefer local/USDA references and checked user values; distinguish them from estimates. Preserve raw/cooked/dry, diet, lean ratios, and explicit weights. Never substitute an unrelated food or invent generic calorie density for an unknown item.
4. **User amounts win:** explicit weights override model arithmetic and remembered portions. Unsupported foods remain unresolved until reviewed, replaced, entered manually, or removed.
5. **Athletic, focused tone:** avoid cartoon emoji clutter and infantile copy. The active interface is light with green accents; older dark Tailwind tokens remain for compatibility. Extend the current UI consistently unless a redesign is requested.
6. **Immediate feedback:** loading, save/error toasts, and Undo should reflect actual persistence. Never silently reset corrupt data or announce a failed write as saved.
7. **Free-only AI:** preserve zero-price limits on every model attempt, bounded requests, robust fenced/substring JSON extraction, semantic validation, and local fallback. The key/model list is server-managed.
8. **Local-first privacy:** preserve account-free browser diaries, backups, favorite snapshots, and opt-in defaults. Do not upload diaries or treat the existing server PIN as multi-user auth.

## Current stack and boundaries

- Backend: .NET 8 Web API, EF Core; SQLite locally, PostgreSQL when `DATABASE_URL` is configured. `DbInitializer` uses `EnsureCreated` plus targeted compatibility SQL.
- Extraction: `OpenRouterService` uses a fixed server-owned free-only model list. Read code and dated review evidence rather than assuming model availability or reusing historical provider capabilities.
- Nutrition: seeded/cached `FoodReference` records, optional USDA lookup in AI mode. `/api/foods/search` searches local/cached references; remote branded search is future work.
- Frontend: Vue 3 Composition API, TypeScript, Vite, Pinia, Tailwind plus active light-theme CSS, Lucide icons, optional Web Speech API input.
- Active storage: `DiaryApi` and `TargetsApi` in `frontend/src/services/api.ts` are aliases for `localDiary`. Meals/targets/library data do not use server CRUD in the current shell.
- Public access: `PUBLIC_DIARY=true` allows AI parsing and food-reference search, returning 404 for legacy private endpoints. Legacy auth/settings components and controllers exist but are not mounted by `App.vue`.
- Tests: xUnit (including EF InMemory and targeted relational checks), Node regressions, and browser verification. Mocked tests do not prove live model accuracy.

## Code map

| Area | Files and responsibility |
|---|---|
| Startup | `backend/Program.cs`, `EnvironmentFile.cs`, `HostedDatabase.cs`: configuration, DI, database selection/seeding, static SPA serving |
| API | `backend/Controllers/AiController.cs`, `FoodsController.cs`: active parse and reference search endpoints |
| Legacy APIs | `DiaryController.cs`, `TargetsController.cs`, `SettingsController.cs`, `AuthController.cs`: private server diary/settings/PIN paths |
| Parsing | `backend/Services/MealParserService.cs`, `OpenRouterService.cs`, `ParseLimits.cs`: extraction, validation, exact/heuristic paths, nutrition resolution, chips, time budgets |
| Access/limits | `backend/Services/PasscodeAccess.cs`, `RequestBudget.cs`: legacy PIN/JWT access filter and shared request counters |
| Nutrition | `backend/Services/UsdaFoodService.cs`, `backend/Data/DbInitializer.cs`: matching, reference cache, seed data |
| Entities/contracts | `backend/Data/Entities/{MealEntry,MealItem,DailyTarget,FoodReference,UserSetting}.cs`; `backend/Models/{DTOs,AiParsingModels}.cs` |
| Active UI | `frontend/src/App.vue`, `components/{QuickLogInput,MealConfirmCard,FoodSearch,MealCard,TimelineFeed,MealLibrary,WeeklyProgress}.vue` |
| Targets/dates/backups | `components/{DailySummaryHeader,QuickTargetModal,DateNavigator,BrowserDataModal}.vue` |
| State/storage | `frontend/src/stores/{diaryStore,toastStore}.ts`, `services/{api,localDiary}.ts`, `utils/{foodDefaults,nutritionReview,useModal}.ts` |
| Legacy client | `components/{SettingsModal,PasscodeModal}.vue`, `stores/{settingsStore,authStore}.ts`: not wired into the active app shell |
| Verification | `tests/Omnom.Tests/`, `frontend/tests/regressions.test.mjs`, `frontend/tests/smoke/`, `docs/verification/` |

## Workflows to preserve

- **AI:** user text → validated extraction or heuristic fallback → compatible reference matching/explicit estimates → review → browser save. Keep first-`{` to last-`}` extraction and fallback when a model returns chatter or invalid content.
- **Exact:** backend deterministic parser and local reference matching; no AI or remote USDA lookup. Unsupported fragments stay visible for correction. Exact is not an offline client-side mode.
- **Review:** replacement changes all nutrients/source IDs at the current weight and clears incompatible chips. Renaming alone does not establish a new reference match.
- **Defaults/reuse:** aliases match exactly after normalization; remembered portions are opt-in; explicit mass wins. Favorites contain snapshots, not live links to an original meal.
- **Dates/targets:** use local `YYYY-MM-DD`, not UTC-derived dates. Resolve targets by latest `effectiveDate <= selectedDate`, then latest ID. Pass the selected diary date when updating targets. Weekly averages use logged days; missing logs are not observed zero intake.
- **Persistence:** `omnom_diary_v1` is scoped to browser profile/origin. Backups include optional favorites/foods; version-1 backups without them remain supported. Import replaces existing data. Preserve validation, quota/error handling, and stale-Undo guards.

## Run and verify

From the repository root, with a compatible .NET SDK/.NET 8 runtime and Node (Vite requires `^20.19.0 || >=22.12.0` as inspected October 20, 2026):

```sh
dotnet restore backend/Omnom.Api.csproj
npm --prefix frontend ci
dotnet run --project backend/Omnom.Api.csproj --launch-profile http
```

In another terminal:

```sh
npm --prefix frontend run dev -- --strictPort
```

Backend: `http://localhost:5027`; frontend: `http://localhost:5173`. The Vite proxy matches the backend HTTP profile. Use [isolated startup overrides](docs/verification/OM-001-local-startup.md#isolated-startup) when a root `.env` or existing databases would affect verification. Never print keys or include them in reports.

Required checks:

```sh
# After backend edits; also run before committing application changes
dotnet test tests/Omnom.Tests

# After frontend behavior changes
npm --prefix frontend test

# Always after frontend .vue or .ts edits
npm --prefix frontend run build
```

The build replaces `backend/wwwroot/`; keep generated bundles synchronized. Check browser-visible changes in a disposable profile/origin with isolated server data. Describe the actual result, skipped checks, and environment in the task evidence; do not substitute historical test counts for a fresh run. The executable release browser gate is `npm --prefix frontend run test:smoke` (one-time browser setup: `npm --prefix frontend run test:smoke:install`). See [the suite guide](frontend/tests/smoke/README.md) for isolation, reports, and failure diagnosis. Docs-only changes need path/link/command checks. Do not run live provider evaluations unless that is part of the authorized task; use synthetic inputs and preserve free-only limits.

For backend schema changes, inspect `DbInitializer` and both configured providers; do not assume a migration command exists. Preserve public/private endpoint boundaries. Account ownership and sync require the OM-010 design before implementation.

## Meal evaluation

See [the OM-003 corpus guide](tests/fixtures/meal-evaluation/README.md). Run the heuristic and stubbed-AI modes from the repository root; both forbid provider networking and use frozen references. A completed baseline can contain measured correctness failures; do not change expectations to conceal them. Live mode requires explicit opt-in and a real HTTP-attempt cap; do not run it without live-evaluation authorization.

## Release versioning

Every new deployment must have a unique, increasing beta version, starting at **beta 0.0.1**. The next deployment after that is **beta 0.0.2**, then **beta 0.0.3**; increment the patch number for every deployment, including previews and redeployments. Do not reuse a version for a new deployment after a failed build; retrying inspection or promoting the same deployment does not consume another version.

Use `frontend/package.json` as the source of truth (`0.0.1-beta` in package SemVer, displayed as `beta 0.0.1`). Update it and the lockfile together with `npm --prefix frontend version <next-version>-beta --no-git-tag-version` before building. The footer reads this value. Record each deployment attempt, version, target, result, and URL in [RELEASES.md](RELEASES.md); for CLI releases include `appVersion` and `releaseLabel` Vercel metadata, and include the human-readable version in the final handoff. For Git-triggered releases, include the version in the release commit subject (propagated into Vercel Git metadata), keep the same footer version, and record the resulting deployment ID/URL. Check Git integration before pushing: this project's `main` branch is connected to production, so a push is a deployment attempt and needs the next version. Follow the [release procedure](RELEASES.md#release-procedure) without exposing credentials or pushing unrelated changes.
