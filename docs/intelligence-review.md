# Omnom intelligence review

Reviewed October 11, 2026, then updated for the user's free-only requirement. This includes a small live synthetic extraction check, not a broad model-quality benchmark. No paid inference requests were made. Historical code findings describe the starting implementation; the current free-only configuration is described below.

**Planning update (October 20, 2026):** [ROADMAP.md](../ROADMAP.md) is the canonical plan for future work, including stable task IDs, dependencies, acceptance criteria, and verification. This review preserves dated research and implementation evidence; its original priority table is historical, and several entries have since been implemented.

## Current free-only configuration

The server prefers [Ling 3.1 Flash](https://openrouter.ai/inclusionai/ling-3.1-flash), with [Apodex 1.1 Mini free](https://openrouter.ai/apodex/apodex-1.1-mini:free) as its sole AI backup. The current public [catalog](https://openrouter.ai/api/v1/models) lists both with zero prompt/completion prices and reasoning support. Ling's model page reports an Artificial Analysis Intelligence Index of 41.1; that general benchmark informed selection but does not prove meal-parsing accuracy.

Every request sets `provider.max_price` to zero for prompt, completion, request, and image charges. OpenRouter documents this as a hard provider filter, so a price change prevents routing instead of authorizing paid inference. There are no paid fallback model IDs, plugins, or tools. Client model overrides remain ignored. [Price-filter documentation](https://openrouter.ai/docs/guides/routing/provider-selection#max-price)

Ling lacks `response_format` support, so it receives the JSON instruction and passes the app's local source/field validation. A production investigation on October 12 found that Apodex's provider rejects `json_schema` with HTTP 400 despite the catalog advertising structured outputs. The backup now uses `json_object`, verified with a generic live request at zero cost, and the same local source/field validation. Invalid output, unavailable providers, and rate limits can trigger the backup; exhaustion triggers the visible local parser fallback, with a specific message when rate limits were encountered. The primary has a 15-second limit; the backup uses the remaining shared 40-second AI budget, within a 50-second request deadline. Production testing demonstrated that the old 15-second backup cap cancelled otherwise accepted requests.

### Live check

Using the configured key, each candidate received an independent generic JSON-extraction instruction and this synthetic description: `Lunch: 150g raw chicken breast, 100g dry rice, 1 can diet Coke, 2 eggs, and 2 slices sourdough. No butter.` No application source or internal prompt was transmitted in this check.

| Model | Result | Observed latency | Reported cost |
|---|---|---|---|
| Ling 3.1 Flash | Returned all five foods with the correct amounts and raw/dry/diet distinctions; omitted butter | 9.4 s | $0 |
| Apodex 1.1 Mini free | Returned all five foods with the correct amounts and raw/dry/diet distinctions; omitted butter | 8.1 s | $0 |
| Inkling Small free | HTTP 403; not selected for the app | 0.8 s | No successful completion |

This verifies access and one basic extraction case with reasoning enabled and zero-price limits. It does not test the complete application prompt or nutrition pipeline live, and cannot guarantee accuracy, quota, or future availability. Automated tests separately cover routing, invalid-result fallback, zero-price limits, cancellation, and the nutrition pipeline.

## Why logging can feel unintelligent

- `backend/Services/OpenRouterService.cs` pins `z-ai/glm-5.2` and explicitly disables reasoning. It requests free-form text, despite the parser expecting JSON, and discards the response's finish reason, actual model, and usage. `README.md` calls this a free LLM setup, although the pinned model's public catalog has nonzero pricing.
- `backend/Services/MealParserService.cs` lets a model's positive `estimatedGrams` override an explicitly supplied weight. Its prompt removes brands, and canonicalization can discard nutritionally meaningful qualifiers. The original JSON parser accepts empty or semantically incomplete objects. An unmatched item can receive a generic 1.5 kcal per gram estimate.
- `backend/Services/UsdaFoodService.cs` favors local staples with a permissive token score before searching remotely. It does not search branded USDA data. An exact brand, preparation state, or mixed dish may therefore become a generic food even when extraction was correct.
- Clarification chips use approximate raw/cooked multipliers and fixed ground-beef nutrition. More model reasoning alone cannot correct these downstream choices.
- `ParseLimits` gives the model 18 seconds. Provider delays or excessive thinking can turn AI logging into heuristic logging; measure how often this happens before concluding that the model itself is weak.

## Verified OpenRouter capabilities

**Previously pinned model:** the live [models catalog](https://openrouter.ai/api/v1/models) reports GLM-5.2 reasoning as optional, enabled by default, with supported efforts `high` and `xhigh`, default `high`. It advertises `reasoning`, `response_format`, and `structured_outputs`. The [model page](https://openrouter.ai/z-ai/glm-5.2) corroborates schema support and these effort levels. It is no longer configured in the app.

**Endpoint compatibility:** public [GLM-5.2 endpoint metadata](https://openrouter.ai/api/v1/models/z-ai/glm-5.2/endpoints) lists multiple compatible providers, but their parameter sets differ: some lack `structured_outputs`, and one Wafer endpoint lacks `temperature`. Set `provider.require_parameters: true` and request only the controls the application needs. Unsupported parameters can otherwise be ignored; provider fallback is enabled by default. [Provider routing documentation](https://openrouter.ai/docs/guides/routing/provider-selection)

**Structured data:** request `response_format.type: "json_schema"`, a strict meal schema, and explicitly require structured-output support. Still validate the returned object and preserve the existing substring JSON extractor and heuristic fallback. Schema enforcement varies across providers, and format correctness does not prove food or portion correctness. [Structured-output documentation](https://openrouter.ai/docs/guides/features/structured-outputs)

**Reasoning and latency:** `reasoning.exclude: true` hides reasoning text without disabling computation. Reasoning tokens remain billable and generally share `max_tokens` with visible output. Too little budget can return empty content with `finish_reason: "length"`. Enable a supported reasoning setting, reserve output space, reject truncated replies, and retain the existing timeout. Whether this improves meal extraction needs a measured comparison. [Reasoning documentation](https://openrouter.ai/docs/guides/best-practices/reasoning-tokens)

**Free versus paid:** `openrouter/free` randomly selects a compatible available free model, so it is a convenient zero-cost option but not a consistent model-quality guarantee. Specific `:free` model IDs must exist in the current catalog; do not append the suffix to an arbitrary paid model. `openrouter/auto` is not a zero-cost substitute. [Free-router documentation](https://openrouter.ai/docs/guides/routing/routers/free-router)

Free usage is quota-limited and affected by credit-purchase tier and provider capacity. Monitor `GET /api/v1/key` and honor `Retry-After`; the application's own request counter does not reveal upstream quota. Avoid baking a stale numerical ceiling into product copy. [Limits documentation](https://openrouter.ai/docs/api_reference/limits)

An ordered `models` list can recover from rate limits or downtime, but the final selected model determines pricing. Every fallback must therefore fit the intended budget and support the same schema and reasoning settings. It does not automatically repair a semantically wrong successful response. [Model-fallback documentation](https://openrouter.ai/docs/guides/routing/model-fallbacks)

## Historical priorities

The original recommendations below explain the earlier work; they are not an active backlog. Parser safeguards, review replacement, meal reuse/defaults, reference-backed chips, editing, and seven-day progress are present in the current working tree. Broad accuracy measurement, branded search, and optional sync remain open; use the [roadmap execution order](../ROADMAP.md#execution-order) for their current scope and status.

| Priority | Improvement | Grounding and success criterion |
|---|---|---|
| 1 | Preserve explicit amounts and meaningful food qualifiers; validate AI output; stop generic unknown-food calories | Parser and canonicalization issues above. Regression cases must distinguish diet cola, raw/cooked meat, nonfat yogurt, and explicit grams/ounces. Unknowns stay visibly unresolved or honestly estimated. |
| 2 | Enable supported reasoning and schema-constrained output, with observable failures | OpenRouter issues above. Measure complete-item extraction, portion accuracy, fallback rate, latency, and spend on the same representative prompts. Keep arithmetic deterministic and nutrition tied to reference records. |
| 3 | Add food replacement search inside the meal review | `FoodsController` and local search already exist; `MealConfirmCard.vue` currently permits renaming but does not re-match nutrition. A corrected name should offer candidate reference foods and update macros together. |
| 4 | Add repeat meals, favorite foods, and user-confirmed defaults | `QuickLogInput.vue` has four fixed shortcuts; `MealCard.vue` exposes deletion but no repeat/edit action. Reuse the user's confirmed portion and brand locally, with an explicit reset, instead of asking the model to guess every time. |
| 5 | Ground preparation and brand choices in real records | Replace raw/cooked scaling chips with matched reference records, preserve lean percentage, and add branded-food or label workflows. Show the actual selected source and uncertainty, rather than treating every fuzzy reference match as equally verified. |
| 6 | Expose saved-meal editing and recent history | `localDiary.updateMeal` already exists but the saved-meal card has no edit control. Correcting yesterday's meal should not require deleting and rebuilding it. |
| 7 | Add weekly target/adherence views and optional device sync | The current diary shows one date; public mode persists in this browser with manual backup. Weekly views can use existing diary data. Sync requires an explicit account and ownership design, while preserving the current quick local workflow. |

Start a representative evaluation set with branded foods, raw versus cooked weights, fractional units, vague portions, mixed dishes, corrections/negations, zero-calorie drinks, and intentionally unknown foods. Test heuristic and AI paths separately. Unit tests can prove transformations and safeguards; a live, budgeted comparison is still needed to prove the user-visible intelligence improvement.

## Implemented in this review

- Replaced the paid configured model with the free-only list described above. Enabled reasoning, excluded reasoning text from the answer, and reserved a 6,000-token maximum shared by reasoning/output. Rejected truncated, empty, or invalid completions. Production testing led to the supported JSON-object mode for Apodex, a shared 40-second AI budget, and a 50-second request deadline. The primary retains its 15-second cap; the backup receives the remaining AI budget.
- Required an excerpt from the meal description for each AI item and rejected empty, invalid, negative, or ungrounded extractions. Preserved fenced-JSON extraction and local fallback. Converted single explicit mass quantities locally instead of trusting model arithmetic. Multiple-weight corrections and drink ounces still depend on the model's interpretation.
- Preserved raw/dry and diet-cola distinctions and rejected incompatible food identities during local and USDA matching. Reused explicit local parsing so rice cups use the reference serving weight.
- Removed the arbitrary calorie density for unknown foods. Unmatched foods without usable nutrition stay visibly unresolved until reviewed or removed. Model estimates remain estimates; excerpt validation does not prove semantic accuracy or complete coverage.
- Added automated regressions for these cases and kept the production frontend bundle synchronized. The small generic live check above reported zero cost. No deployment or broad application benchmark was performed. Reasoning may increase latency; deadlines and free quotas can still cause fallback.

## Follow-up implementation (October 12, 2026)

The five product improvements requested by the user are now implemented: in-review reference replacement, favorite/repeat meals, explicitly remembered food aliases and portions, reference-backed clarification choices, and saved-meal editing with a seven-day progress view. Favorites and defaults are optional fields in the existing browser backup format; older backups remain readable. Favorites keep a snapshot, and remembered defaults require an exact alias match. Explicit weights override remembered portions. Brands can be stored from checked label values; brand-specific USDA search remains a future enhancement.

Public mode now allows the read-only food-reference search while keeping legacy diaries and settings private. Raw/cooked and lean options only appear when actual local references exist. Unsupported preparation or lean ratios do not substitute another record. Frontend replacements also update fiber and source IDs and clear incompatible chips.

Verification: 91 backend tests and 25 frontend regressions passed. Browser checks confirmed raw chicken at 150g uses 180 kcal, replacing it with cooked thigh uses 313.5 kcal, a remembered alias reuses the 150g portion, favorites survive reload and repeat onto the selected date, saved-meal edits and Undo update daily/weekly totals, and local data persists after reload. This used an isolated local database with AI disabled; no paid requests or production deployment occurred. Optional device sync is still a proposal.
