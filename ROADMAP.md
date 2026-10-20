# Omnom roadmap

Last reconciled with the working tree: **October 20, 2026**.

This is the canonical plan for future work. It replaces the priority list in [the intelligence review](docs/intelligence-review.md#historical-priorities). The review preserves the investigation and earlier verification evidence; [README.md](README.md) covers setup and features, and [AGENTS.md](AGENTS.md) covers contributor rules.

The goal is to make logging food fast, nutrition traceable, and diary data dependable for weightlifters. Improve those foundations before adding accounts or expanding into unrelated fitness features.

## Start here

1. Read [the current baseline](#current-baseline) and [product constraints](#product-constraints). Check the working tree before editing: this snapshot includes existing uncommitted implementation work.
2. Pick a **Ready** task from the execution table whose dependencies are satisfied. The recommended next task is **OM-004**. Keep each change focused on one task or a documented slice of it.
3. Read that task's entry, inspect its code touchpoints, and record an owner, start date, and implementation/issue link when work starts. Reconcile any drift between this document and the code before proceeding.
4. Implement the scope, satisfy its acceptance criteria, and follow [verification and completion](#verification-and-completion).
5. Update the task status and evidence in this file in the same change. Record remaining work explicitly; do not mark a whole milestone complete after delivering only part of it.

The order below is a proposed implementation sequence, without calendar promises. **Ready** means sufficiently scoped to start; it does not mean implemented. **Needs decision** means a named product or architecture choice must be resolved before implementation. **Proposed** means optional future scope. Other statuses are **In progress**, **Blocked** (record the dependency), **Done** (evidence required), and **Deferred** (record why). Task ownership and verification are recorded below each task when work starts.

## Product constraints

- Meal time stays optional. Preserve insertion order and user-supplied meal names; never infer meal slots from the clock.
- Prefer explicit user amounts and qualifiers over guesses. Distinguish raw/cooked, dry/cooked, lean ratios, diet/regular, and brand identity when evidence supports them.
- Keep reference nutrition, user-entered label values, and estimates distinguishable. An unknown food must never inherit an unrelated food's macros or arbitrary calorie density.
- Keep resilient JSON extraction, local heuristic fallback, optional clarification chips, and an editable review before saving.
- Preserve free-only AI routing, zero-price provider limits on every attempt, bounded requests, and visible fallback. Verify provider capabilities when changing integrations; historical model notes are dated evidence.
- Keep the current browser diary usable without an account. Do not upload existing diaries as a side effect of adding sync.
- Preserve backups, favorite snapshots, opt-in remembered foods, local calendar dates, effective-date targets, and Undo behavior.
- Keep a focused athletic tone, accessible controls, and clear loading, success, and failure feedback.

## Current baseline

“Implemented” below means present in the October 20 working tree. It does **not** establish that those exact changes are committed, deployed, or freshly verified. The October 12 review reports historical checks. The October 20 [OM-001 verification](docs/verification/OM-001-local-startup.md) freshly passed 92 backend tests, 25 frontend regressions, the frontend build, and a scoped local browser save/reload check; broader release/live accuracy checks remain open.

| Capability | Status and limits | Main code / evidence |
|---|---|---|
| Free-only extraction and local fallback | Implemented; broad live accuracy comparison remains open | [OpenRouterService](backend/Services/OpenRouterService.cs), [MealParserService](backend/Services/MealParserService.cs), [routing tests](tests/Omnom.Tests/FreeModelRoutingTests.cs) |
| Explicit mass handling, qualifier preservation, unresolved unknown foods | Implemented safeguards; multi-weight corrections and beverage ounces still need broader coverage | [intelligence regressions](tests/Omnom.Tests/IntelligenceRegressionTests.cs), [deterministic parser tests](tests/Omnom.Tests/DeterministicParserTests.cs) |
| Replace reference foods during review; edit grams and nutrition | Implemented; reference search currently searches local/cached records | [FoodSearch](frontend/src/components/FoodSearch.vue), [MealConfirmCard](frontend/src/components/MealConfirmCard.vue), [UsdaFoodService](backend/Services/UsdaFoodService.cs) |
| Repeat/favorite meals and remembered food aliases/portions | Implemented in browser storage; exact alias matching, explicit weight wins, favorites are snapshots | [MealLibrary](frontend/src/components/MealLibrary.vue), [food defaults](frontend/src/utils/foodDefaults.ts), [local diary](frontend/src/services/localDiary.ts) |
| Raw/cooked and lean clarification choices | Implemented where matching reference records exist; unsupported variants remain unresolved | [reference clarification tests](tests/Omnom.Tests/ReferenceClarificationTests.cs) |
| Saved-meal editing, Undo, and seven-day progress | Implemented; averages use logged days and each day's effective target | [MealCard](frontend/src/components/MealCard.vue), [WeeklyProgress](frontend/src/components/WeeklyProgress.vue), [frontend regressions](frontend/tests/regressions.test.mjs) |
| Browser persistence and backup export/import | Implemented; version-1 backups include optional favorites/defaults; import replaces the diary | [local diary](frontend/src/services/localDiary.ts), [BrowserDataModal](frontend/src/components/BrowserDataModal.vue) |
| Server persistence and access boundaries | SQLite locally or PostgreSQL when configured; public reference/parse access differs from legacy private diary/settings endpoints | [Program](backend/Program.cs), [PasscodeAccess](backend/Services/PasscodeAccess.cs), [public-mode tests](tests/Omnom.Tests/PublicDiaryTests.cs) |

The current frontend explicitly binds `DiaryApi` and `TargetsApi` to browser storage in [api.ts](frontend/src/services/api.ts). Existing server diary controllers and hosted database configuration are **not** device sync or account ownership. USDA parsing lookups request Foundation, SR Legacy, and Survey data; branded search is not implemented. Existing logs identify model success and some failures, but do not provide the full evaluation described below.

## Execution order

All IDs are stable references for issues, PRs, commits, and agent handoffs. Dependencies mean completed tasks unless explicitly described as decision gates.

| Milestone | ID | Deliverable | Status | Dependencies |
|---|---|---|---|---|
| M1 — Establish a reproducible baseline | OM-001 | Reconcile developer docs and local startup | Done | None |
| M1 | OM-002 | Reproducible release smoke checks | Done | OM-001 |
| M1 | OM-003 | Representative meal evaluation corpus and runner | Done | None |
| M2 — Improve measurable accuracy | OM-004 | Safe parsing diagnostics and live baseline report | Ready | OM-003 |
| M2 | OM-005 | Deterministic corrections, negations, and beverage units | Ready | OM-003 |
| M2 | OM-006 | Branded reference search and replacement | Ready | OM-003; evaluate against OM-004 before closing |
| M2 | OM-007 | Source-backed portion coverage | Ready | OM-003; OM-005 for unit rules |
| M3 — Strengthen daily use | OM-008 | Backup recovery and storage concurrency hardening | Ready | OM-002 |
| M3 | OM-009 | Longer history and target adherence | Proposed | OM-002; decide reporting semantics first |
| M4 — Optional cross-device use | OM-010 | Account/sync architecture decision | Needs decision | OM-008; product decision to pursue sync |
| M4 | OM-011 | Incremental opt-in sync implementation | Proposed | Accepted OM-010 design; OM-002, OM-008 |

Recommended sequence: complete M1, establish OM-004's baseline, then deliver OM-005 through OM-007 as separate changes. OM-008 can follow M1 independently of accuracy work. OM-009 and M4 can remain deferred without blocking improvements to local logging. Any newly confirmed data-loss, access-control, or nutrition-identity bug takes priority over feature work; record the reproducer and affected task.

## M1 — Establish a reproducible baseline

**Exit condition:** a contributor can start the app, run repeatable offline checks, and identify exactly which behavior has been verified on a particular revision.

### OM-001 — Reconcile developer docs and local startup

**Outcome:** agents and developers follow the current app architecture without relying on obsolete model, storage, or setup descriptions.

**Scope:** update `AGENTS.md`, `DEV_README.md`, and relevant README setup sections. Document the browser diary versus legacy server endpoints, actual entity/controller names, server-managed AI settings, current reference/estimate behavior, and current UI. Resolve the original backend/proxy mismatch (HTTP profile 5027, Vite previously 5000). The chosen local launch path now uses port 5027 for both the HTTP profile and Vite API proxy. Preserve the product rules above.

**Acceptance criteria:**

- [x] Setup commands use a backend URL that matches the frontend proxy and work from a fresh environment with documented prerequisites.
- [x] Instructions distinguish no-key local parsing from live AI mode, and browser backups from server database persistence.
- [x] Code paths and configuration names resolve; docs do not claim a client model selector or automatic device sync is active.
- [x] Legacy architecture examples are updated or explicitly labeled historical; provider names/capabilities carry a verification date when documented.

**Touchpoints:** [launch settings](backend/Properties/launchSettings.json), [Vite config](frontend/vite.config.ts), [API bindings](frontend/src/services/api.ts), [Program](backend/Program.cs). **Evidence:** exact startup commands, observed URLs, and a successful browser parse → review → save → reload check. Any startup code change uses the relevant verification commands below.

**Completion record (October 20, 2026):** Owner: Codex; started/completed October 20. Status: Done. Updated README, developer/agent/frontend guides and the Vite proxy; preserved existing uncommitted application work. Verified dependency install, 92 backend tests, 25 frontend regressions, TypeScript/build, local Exact parse → review → browser save → reload, no-key AI fallback, and public endpoint restrictions. No live provider check or deployment. Revision/commands/environment, screenshot, and limitations are in the [OM-001 report](docs/verification/OM-001-local-startup.md). Next concrete step: OM-002.

### OM-002 — Reproducible release smoke checks

**Outcome:** every release can verify the complete logging flow, rather than relying only on isolated parser tests or an old test count.

**Scope:** add an executable browser smoke suite or a repeatable browser harness with assertions, deterministic reference data, and stubbed AI/USDA responses. Keep live-provider checks separate. Document how to run it against an isolated local database and browser profile. Use the current Node regression and xUnit suites as foundations, not substitutes for browser checks.

**Acceptance criteria:**

- [x] Parse → review → replace a food → save → reload preserves weights, nutrients, sources, and selected diary date.
- [x] Repeat/favorite, remembered alias, explicit-weight override, saved-meal edit, deletion, and Undo update daily and weekly totals correctly.
- [x] Changing targets does not rewrite earlier days; missing days remain unlogged; rapid date navigation does not show stale results.
- [x] Invalid imports and storage failures preserve existing data; public mode does not expose private server diaries/settings.
- [x] Desktop and a narrow mobile viewport cover keyboard navigation, dialog focus/escape, visible labels, and success/error feedback.
- [x] A fresh run records revision/working-tree state, environment, commands, results, and any checks skipped. Document the release checklist and how to repeat it.

**Touchpoints:** [frontend regressions](frontend/tests/regressions.test.mjs), [preview harness](frontend/tests/preview.html), [App](frontend/src/App.vue), [public access tests](tests/Omnom.Tests/PublicDiaryTests.cs), [hosting tests](tests/Omnom.Tests/HostingSecurityTests.cs). **Evidence:** a reproducible command plus a dated report, including browser results; a passing script-extraction test alone does not close this task.

**Completion record (October 20, 2026):** Owner: Codex; started/completed October 20. Status: Done. Added a Playwright release gate loading the built SPA from an isolated .NET/SQLite host, deterministic API fixtures, per-test browser contexts, desktop/mobile projects, failure artifacts, metadata reports, and the [release checklist](frontend/tests/smoke/README.md). Final run passed 18 browser scenarios with zero retries/skips, 92 backend tests, 25 Node regressions, and the frontend/.NET builds. The reporter rejects skipped scenarios. No live provider evaluation, deployment, or app logic change. See the [dated verification report](docs/verification/OM-002-smoke-checks.md) and [machine-readable results](docs/verification/OM-002-results.json). Next concrete step: OM-003.

### OM-003 — Representative evaluation corpus and runner

**Outcome:** accuracy changes have comparable evidence and failures can become deterministic regression cases.

**Scope:** add versioned synthetic fixtures under `tests/fixtures/meal-evaluation/` and a runner with separate heuristic, stubbed-AI, and explicitly enabled live-AI modes. These are proposed new paths. Begin with at least 60 reviewed prompts, including at least six in each of: explicit/fractional mass, raw/cooked/dry, diet/nonfat/lean qualifiers, branded foods, vague portions, mixed dishes, corrections/negations, and unknown foods. Include full-day descriptions and the complex meal example from the project guide.

**Acceptance criteria:**

- [x] Each case has a stable ID, expected foods/qualifiers, excluded foods, known quantities, expected unresolved fields, and the source of any nutrition expectation.
- [x] Vague portions and mixed dishes allow explicit uncertainty; the corpus does not invent an exact nutritional truth for them.
- [x] The offline runner has no provider/network dependency. Live mode is opt-in, accepts a request cap, retains zero-price safeguards, and can stop cleanly on quota exhaustion.
- [x] Reports show item precision/recall, fully correct meals, explicit-amount accuracy, qualifier errors, forbidden substitutions, and unresolved/estimated outcomes. Define denominators and tolerances in the fixture README.
- [x] Explicit gram conversions use a stated rounding tolerance (initially 0.1 g); kcal/macros use fixture-specific reference tolerances, not one blanket accuracy claim.
- [x] Every mode and dataset version is recorded. The diagnostic test that only prints a complex prompt's output is not treated as an assertion-based evaluation.

**Touchpoints:** [ComplexPromptDiagnostics](tests/Omnom.Tests/ComplexPromptDiagnostics.cs), [intelligence regressions](tests/Omnom.Tests/IntelligenceRegressionTests.cs), [meal matching tests](tests/Omnom.Tests/MealMatchingTests.cs). **Evidence:** fixture README, reproducible runner command, and an offline baseline report. Live results belong to OM-004 and are not required to finish this task.

**Completion record (October 20, 2026):** Owner: Codex; started/completed October 20. Status: Done. Added dataset 1.0.0 (66 prompts/86 foods), frozen local references, the heuristic/canned/live runner, one-to-one scorer, JSON/Markdown reports, strict CLI controls, and live cap/price/quota guards. Both offline modes completed 66 cases with zero network/provider requests; measured 41/66 heuristic and 52/66 canned-pipeline fully correct meals (not live-model quality). All 103 backend tests, 25 Node regressions, and 18 browser smoke checks passed. [Verification/baselines](docs/verification/OM-003-meal-evaluation.md), [runner guide](tests/fixtures/meal-evaluation/README.md). No live provider run. Next concrete step: OM-004.

## M2 — Improve measurable accuracy

**Exit condition:** each delivered improvement has a regression case and a before/after evaluation. Hard rules about explicit weights, incompatible food identity, unknown-food fallback, and free-only routing pass all applicable deterministic cases. Report broader model accuracy and latency honestly, including failures.

### OM-004 — Safe diagnostics and a live baseline

**Outcome:** maintainers can tell whether failures come from extraction, matching, provider quotas, or request deadlines.

**Scope:** extend existing structured logs with a request correlation ID, stage timings, requested/actual model, fallback category, reference/estimate/unresolved counts, and token/cost metadata when supplied. Include rate limits, invalid output, truncated output, timeouts, missing configuration, and USDA failures. Redact raw descriptions, search strings, credentials, reasoning, and provider bodies; inspect the existing USDA warning, which currently includes the query. Do not require an external analytics service.

**Acceptance criteria:**

- [ ] Deterministic tests exercise success, primary failure/backup success, rate limiting, total fallback, invalid output, and cancellation without raw meal or secret data appearing in logs.
- [ ] A fixed OM-003 subset runs through the complete application pipeline, not just a generic model prompt. Record dataset/revision, model IDs, sample counts, successful/failed/limited requests, and repetitions.
- [ ] The report separates extraction from reference matching and shows fallback rate, p50/p95 end-to-end latency with sample counts, and observed usage/cost. Missing usage is unknown, not zero.
- [ ] Establish measured latency/accuracy baselines and explicit targets for subsequent changes. Keep current deadlines unless measurements justify a separately explained change.
- [ ] Verify current official provider documentation and free availability before the live run. Stop if zero-price routing is unavailable; record that limitation rather than enabling paid fallback.

**Touchpoints:** [OpenRouterService](backend/Services/OpenRouterService.cs), [MealParserService](backend/Services/MealParserService.cs), [UsdaFoodService](backend/Services/UsdaFoodService.cs), [ParseLimits](backend/Services/ParseLimits.cs), [RequestBudget](backend/Services/RequestBudget.cs). **Evidence:** log-redaction tests and a dated evaluation report. Instrumentation can land separately, but this task stays partial until the live report or an explicit deferral is recorded.

### OM-005 — Corrections, negations, and beverage units

**Outcome:** explicit corrections and exclusions survive both AI and local parsing without unsafe arithmetic.

**Scope:** extend deterministic quantity/unit handling and validation using OM-003 failures. Cover `200g chicken, actually 150g`, `no butter`, `rice without oil`, `2 more slices`, and beverages expressed in `oz`, `fl oz`, `ml`, or named containers. Distinguish mass from volume; convert volume to mass only with a food-specific reference or clearly disclosed assumption. Preserve ambiguity when there is insufficient evidence.

**Acceptance criteria:**

- [ ] Tests specify whether each correction replaces an earlier amount, adds another item, or remains unresolved; excluded foods do not reappear as default ingredients.
- [ ] Explicit grams/ounces of mass still override model arithmetic and remembered portions.
- [ ] Fluid ounces are not automatically multiplied by the mass-ounce conversion. Ambiguous drink units and unsupported densities remain reviewable with visible assumptions.
- [ ] Raw/cooked/diet/lean identity and fallback behavior remain intact in both AI and heuristic paths.
- [ ] Before/after corpus results identify improvements and regressions by case ID; no hard-rule regression is accepted silently.

**Touchpoints:** [MealParserService](backend/Services/MealParserService.cs), [AI models](backend/Models/AiParsingModels.cs), [food defaults](frontend/src/utils/foodDefaults.ts), parser/matching regressions. **Evidence:** focused tests plus the OM-003 comparison; live validation of changed AI instructions uses OM-004's method.

### OM-006 — Branded reference search and replacement

**Outcome:** a user can find and select a specific branded product with traceable nutrition instead of silently receiving a generic staple.

**Scope:** extend local/cached search with bounded USDA branded search, preserve brand/product identity, normalize serving nutrients consistently, and expose source/brand/serving information in review. Keep checked manual-label entry and remembered aliases as useful paths. Start with user-selected search results; automatic brand matching must meet stricter identity tests before it replaces a generic record.

**Acceptance criteria:**

- [ ] Verify the current official USDA response contract and serving units; test normalization using captured synthetic or public reference fixtures without requiring a live key in routine tests.
- [ ] Search distinguishes exact branded, generic reference, and estimate results; a brand term alone is insufficient evidence for a product match.
- [ ] Choosing a result updates all nutrients, grams/quantity semantics, source IDs, and incompatible clarification state. Previously saved meals and favorite snapshots remain unchanged.
- [ ] Missing fiber/serving mass, malformed records, no results, API failures, and slow/stale search responses have explicit behavior. Missing data is not presented as verified zero.
- [ ] Cached results retain their actual product identity; an earlier search query does not become evidence for a later match. Anonymous search stays bounded and respects existing access rules.
- [ ] Corpus results cover similar product names, multiple brands, raw/cooked variants, and label rounding. Existing version-1 backups remain readable if metadata expands.

**Touchpoints:** [UsdaFoodService](backend/Services/UsdaFoodService.cs), [FoodsController](backend/Controllers/FoodsController.cs), [FoodReference entity](backend/Data/Entities/FoodReference.cs), [DTOs](backend/Models/DTOs.cs), [FoodSearch](frontend/src/components/FoodSearch.vue), [MealConfirmCard](frontend/src/components/MealConfirmCard.vue), [api.ts](frontend/src/services/api.ts). **Evidence:** service/API regressions, browser replacement check, backup compatibility check, and OM-003/OM-004 comparison. Document any new reference-storage migration for both configured database providers.

### OM-007 — Source-backed portions

**Outcome:** common cups, slices, scoops, and containers have defensible conversions, while vague portions remain visibly approximate.

**Scope:** identify the most frequent unsupported units in the corpus, then extend reference serving metadata and deterministic conversions. Begin with at least ten reviewed food/unit combinations; document the serving source and preparation state. Reuse verified defaults rather than adding broad multipliers for unrelated foods.

**Acceptance criteria:**

- [ ] Tests cover fractional cups/slices, named container sizes, and conflicts between explicit weights and reference serving sizes.
- [ ] Raw/dry versus cooked serving weights remain distinct; conversion rules cannot leak between foods with different densities.
- [ ] Vague descriptions such as `handful` or `medium plate` display the assumed grams and stay editable; missing evidence does not create a verified portion.
- [ ] Remembered portion preferences are opt-in, resettable, and subordinate to explicit weights; nutrient scaling includes fiber and source metadata.
- [ ] The evaluation shows conversion error for known weights and reports uncertainty separately for vague portions.

**Touchpoints:** [DbInitializer](backend/Data/DbInitializer.cs), [FoodReference](backend/Data/Entities/FoodReference.cs), [MealParserService](backend/Services/MealParserService.cs), [food defaults](frontend/src/utils/foodDefaults.ts), portion regressions. **Evidence:** reference provenance table, deterministic tests, and corpus comparison.

## M3 — Strengthen daily use

**Exit condition for the committed scope:** OM-008's recovery/concurrency scenarios pass and the M1 smoke checks remain green. OM-009 is an optional extension and does not block M3 unless explicitly selected.

### OM-008 — Backup recovery and storage concurrency

**Outcome:** a failed restore or overlapping browser edits cannot quietly erase a diary.

**Scope:** build on existing validation, import confirmation, export, storage errors, and Undo guards. Add a backup preview with counts/date range, preserve a recoverable pre-import snapshot or downloadable export, and detect stale writes from another tab. Choose the smallest storage design that satisfies the scenarios; moving to IndexedDB is a design option, not an assumed prerequisite.

**Acceptance criteria:**

- [ ] Valid version-1 backups with or without optional library fields round-trip meals, targets, favorites, defaults, source metadata, and insertion order.
- [ ] Invalid/oversized/unsupported backups and failed writes leave the original diary readable. A corrupt diary can be exported as raw data without silently resetting it.
- [ ] Restore preview states that import replaces existing data; recovery works if a valid but unintended backup was chosen. If a pre-import snapshot cannot be saved, do not overwrite the diary.
- [ ] Two tabs editing/deleting/importing do not silently overwrite one another. Detect conflicts and offer a clear reload/retry path; invalidate stale Undo actions.
- [ ] Blocked/full storage shows a useful error and does not announce a save that did not persist. Test a documented large-diary fixture to characterize limits.

**Touchpoints:** [local diary](frontend/src/services/localDiary.ts), [BrowserDataModal](frontend/src/components/BrowserDataModal.vue), [diary store](frontend/src/stores/diaryStore.ts), [toast store](frontend/src/stores/toastStore.ts). **Evidence:** import-failure, quota, and concurrent-tab tests plus a browser recovery walkthrough.

### OM-009 — Longer history and target adherence

**Outcome:** users can inspect consistency across a longer period without interpreting missing logs as zero intake.

**Decision before implementation:** select a small first scope, such as a 28-day view, and define calorie adherence (range versus exact target), partial-day meaning, and any export format. Do not add weight trends, coaching, or automatic macro changes to this task.

**Proposed acceptance criteria:**

- [ ] Selected date anchors the range; every day uses its historical effective target, including multiple target edits on one date.
- [ ] Logged-day averages, missing days, and protein/calorie adherence have explicit denominators. No data is presented as observed zero intake.
- [ ] Edits and Undo update the range immediately; leap days, month/year boundaries, and daylight-saving transitions preserve local dates.
- [ ] Keyboard/mobile navigation and large-diary behavior meet the M1 smoke checks. If export is included, its totals match the displayed range.

**Touchpoints:** [WeeklyProgress](frontend/src/components/WeeklyProgress.vue), [local diary](frontend/src/services/localDiary.ts), [diary store](frontend/src/stores/diaryStore.ts). **Evidence:** selected scope/metric definitions, date/target tests, and browser checks. Split additional reporting requests into new IDs.

## M4 — Optional cross-device use

**Entry gate:** a product decision to pursue accounts and sync. No provider, auth system, or sync protocol has been selected. A server database alone is insufficient to implement this safely.

### OM-010 — Account and sync architecture decision

**Outcome:** an accepted architecture decision record (proposed path: `docs/decisions/001-optional-sync.md`) that is concrete enough to implement and test.

**Questions to resolve:** account/session provider and operating cost; per-user ownership for every diary/target/library record; stable IDs across devices; conflict detection and resolution; deletion/tombstone rules; local-to-account import; offline queues and retries; backup/export/deletion guarantees; logout and shared-browser behavior; migration of legacy private server data; schema rollout and rollback.

**Acceptance criteria:**

- [ ] Compare at least two feasible approaches, including keeping manual backups, against cost, complexity, offline behavior, and account recovery.
- [ ] Specify API/data contracts, ownership boundaries, same-record conflict examples, idempotency, and the treatment of historical targets and favorite snapshots.
- [ ] Define an explicit opt-in migration that previews local data and can recover from interrupted uploads without duplicate meals or data loss.
- [ ] Define a staged rollout, failure/recovery matrix, integration tests, and what remains usable when the service is unavailable.
- [ ] Record the selected approach, unresolved decisions, and decision owner/date. If sync is declined, mark OM-011 Deferred and preserve local mode.

**Touchpoints to inspect:** [server controllers](backend/Controllers), [entities](backend/Data/Entities), [AppDbContext](backend/Data/AppDbContext.cs), [PasscodeAccess](backend/Services/PasscodeAccess.cs), [local diary](frontend/src/services/localDiary.ts). **Evidence:** accepted decision record. This task delivers a design, not an account system.

### OM-011 — Implement opt-in sync in independently verifiable slices

**Outcome:** a signed-in user can continue their diary across two devices while anonymous/local use remains available.

**Scope after OM-010:** split into linked implementation tasks for (1) auth and ownership enforcement, (2) versioned storage/API migrations, (3) opt-in local import, (4) sync queue/conflicts, and (5) rollout and recovery. Each slice needs its own acceptance evidence; do not mark this parent Done until the complete flow passes.

**Proposed acceptance criteria:**

- [ ] Two test accounts cannot read or mutate one another's records; anonymous reference access and local logging keep their intended behavior.
- [ ] Two device profiles converge after offline edits, retries, and interrupted imports without duplicate IDs, silent conflict loss, or resurrected deletions.
- [ ] Targets, favorites, remembered foods, optional times, original dates/order, and source metadata survive import/sync/export.
- [ ] Logout and switching accounts cannot expose the previous account's private data; local-data ownership follows the OM-010 decision.
- [ ] Backup recovery, account export/deletion, and rollout/rollback work against the selected deployed storage, using isolated test data.

**Evidence:** the accepted OM-010 design, links to implementation slices, ownership integration tests, two-device browser checks, and a rollout/recovery report. Select provider-specific implementation details only after the design decision.

## Verification and completion

Run commands from the repository root unless shown otherwise:

```sh
# Backend changes
dotnet test tests/Omnom.Tests

# Frontend changes: regressions plus type checking and production bundle
npm --prefix frontend test
npm --prefix frontend run build
```

Frontend `.vue` or `.ts` edits require the build so `backend/wwwroot/` stays synchronized. Run both suites for cross-layer behavior. Add focused tests for new parser rules, persistence failure cases, or access boundaries; do not claim mocked tests establish live model quality. Browser-visible changes also need the relevant OM-002 scenarios. Docs-only updates need link/path/command consistency checks, without claiming application tests were rerun.

Use isolated databases, disposable browser profiles, synthetic meal descriptions, and mocked providers for routine tests. Live evaluations explicitly enable the network, state the request cap and dataset, and preserve the free-only constraints. Record unavailable live checks as unavailable. A local build or test does not establish production deployment; deployment status needs its own revision and verification evidence.

A task is **Done** when its acceptance criteria are met, required checks pass, and docs/contracts/backups remain consistent. If anything is deferred, list it with a linked task rather than hiding it in a completion claim.

Use this handoff record below the relevant task or link to an issue/report containing it:

```text
Task ID / status:
Owner / started / last updated:
Revision or PR / dirty working-tree notes:
Scope delivered / remaining acceptance criteria:
Checks: exact commands, environment, date, results, skipped checks and reasons:
Evaluation: dataset version, case deltas, latency/fallback/cost when applicable:
Data/API/backup compatibility and rollout notes:
Blockers or decisions / next concrete step:
Evidence links:
```

## Maintenance and scope boundaries

- Update this file when a task starts, completes, changes scope, or is deferred. Preserve IDs and link replacement tasks; keep completed entries as history.
- Add an ID, outcome, dependencies, code touchpoints, acceptance criteria, and verification method before turning a new idea into Ready work.
- Treat model availability, prices, provider parameters, hosting limits, and integration contracts as dated facts to verify during implementation. Do not copy old benchmark claims into current release evidence.
- Keep detailed investigations in `docs/` and link them here; README remains a concise entry point. Reconcile architecture guidance when implementation changes.
- Barcode scanning, meal-photo recognition, automatic coaching/target adjustment, workout tracking, and social features are outside this roadmap. Reconsider them only with an explicit product need and their own scoped tasks.

## Change history

| Date | Change |
|---|---|
| 2026-10-20 | Created the canonical roadmap from the intelligence review and inspected working tree. Separated implemented baseline from future tasks, added milestones/dependencies/acceptance criteria, and left optional reporting/sync behind explicit design decisions. Documentation checks only; application verification remains tracked in OM-002/OM-004. |
| 2026-10-20 | Completed OM-001: reconciled contributor/setup docs, aligned Vite with backend HTTP port 5027, and recorded fresh local test/build/browser evidence. OM-002 is the next task. |
| 2026-10-20 | Completed OM-002: 18 deterministic desktop/mobile browser checks, isolated .NET host, reports/artifacts, and a repeatable release gate; existing backend/Node suites passed. No deployment. OM-003 is next. |
| 2026-10-20 | Completed OM-003: 66-case v1 corpus, offline baselines, scorer/request-guard tests, and explicit capped live mode. Prepared beta 0.0.2 for the authorized main-branch push. OM-004 is next. |
