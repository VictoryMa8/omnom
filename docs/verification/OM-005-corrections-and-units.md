# OM-005 — Corrections, negations and beverage units

**Done — October 21, 2026 (America/Chicago).** Owner: Codex. Implemented against revision `ef9fee37af5304f654ccab1faad848afaffefc49` plus the uncommitted OM-004 diagnostics and this OM-005 change. The starting dirty tree contained the preceding OM-004 work, which was preserved. No commit, push, deployment or version increment was performed.

## Behavior and bounded grammar

[Shared amount rules](../../backend/Services/MealParserService.Amounts.cs) now apply to Exact, heuristic fallback and validated AI extractions. The original source stays visible in review and is preserved when saving.

| Input | Result |
|---|---|
| `200g cooked chicken breast, actually 150g` | One 150g portion. A model returning the old amount or an excerpt omitting the correction is overridden from the complete literal context. |
| `200g cooked chicken breast; make that 0.1kg` | One 100g portion; quantity/unit remain 0.1 kg. Chained numeric corrections replace the immediately preceding amount. |
| `150g cooked rice without oil` | Rice only; no oil is added. Leading `no`, `not`, `without`, `skipped`, and `ignore the earlier …` clauses are excluded, as are unquantified continuations of exclusion lists. |
| `100g raw chicken breast, not cooked` | Raw chicken remains raw; the negated preparation cannot become a reference constraint. Negated source excerpts cannot ground an AI food. |
| `2 slices sourdough and 2 more slices sourdough` | Two separate 100g portions using the existing 50g/slice reference. No replacement or merging. |
| `2 slices sourdough, 2 more slices` | The second count inherits the immediately preceding food with a matching slice unit. A bare extra count without such context stays unresolved. |
| `8 fl oz whole milk` | Quantity 8, unit `fl oz`, approximately 236.6g with a visible density assumption; it is never converted with the mass-ounce multiplier. |
| `8oz whole milk` | The same explicitly disclosed US fluid-ounce interpretation, including an ambiguity notice asking the user to confirm volume or enter weighed grams. |
| `250ml whole milk` / `0.5 l whole milk` | 250g / 500g with the same approximate density disclosure. |
| `8oz cooked chicken breast` / `8oz whole milk by weight` | 226.8g as mass ounces. Explicit mass, including grams alongside a volume, wins over model arithmetic. |
| `100 ml olive oil`, unknown juice, or `1 bottle whole milk` | Unsupported density, reference identity or container size remains unresolved with zero inferred mass/nutrients and a review instruction. |
| `100g chicken 150g`, food-changing corrections, or `2 bottles 250ml …` | Ambiguous arithmetic stays unresolved; it cannot silently become two portions or a guessed total. |

The grammar intentionally handles an immediately following numeric correction (`actually`, `make that`, `rather`), not arbitrary conversational rewriting. Explicitly counted additions remain separate items. Unsupported ambiguity requires a checked correction in review. Meal names and time/date/insertion-order behavior are unchanged.

Volume and weight are separate internally. The implementation uses **29.5735295625 ml per US customary fluid ounce**, consistent with the rounded 29.57353 conversion in [NIST SP 811 Appendix B.9](https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b9). Recognized reference beverages, and the existing zero-calorie-cola semantic case, disclose **“assumed 1 g/ml”**; that density is an editable approximation, not source-verified milk/latte density. Other foods receive no generic volume-to-mass multiplier. Matching slice/scoop/container reference servings use their existing checked gram weight; no new serving table or database metadata was added.

[Remembered food defaults](../../frontend/src/utils/foodDefaults.ts) recognize corrected masses and named additional portions. Drink ounces cannot be reinterpreted as mass ounces during reuse. An unresolved volume/container needs a checked remembered serving in the same unit; an unrelated usual portion cannot resolve it. Existing portion assumptions remain visible unless a checked compatible serving supersedes them. All nutrients, including fiber, scale together.

## Before/after evaluation

Both modes ran all 66 OM-003 v1 / dataset 1.0.0 cases before and after, with identical dataset/reference hashes, frozen local references, isolated in-memory SQLite and no provider/network attempts. No fixture, expected value or canned model response was changed. [Machine-readable evidence](OM-005-results.json) includes both run contexts, aggregate metrics, actual items and every food verdict by case ID.

| Metric | Heuristic before → after | Canned AI pipeline before → after |
|---|---|---|
| Fully correct meals | **41/66 → 48/66** | **52/66 → 52/66** |
| Explicit-mass checks correct | **52/61 → 55/61** | 56/61 → 56/61 |
| Item precision | 76/89 → 76/86 | 77/86 → 77/86 |
| Item recall | 76/86 → 76/86 | 77/86 → 77/86 |
| Excluded-food violations | **1 → 0** | 0 → 0 |
| Qualifier errors | 8 → 8 | 6 → 6 |
| Forbidden substitutions | 11 → 11 | 11 → 11 |

New fully correct heuristic cases: **MIXED-005, MIXED-007, CORRECTIONS-001, CORRECTIONS-005, CORRECTIONS-006, CORRECTIONS-008, FULL-DAY-001**. CORRECTIONS-007 remains correct; its item source excerpt now omits the explicitly discarded estimate. The correction set now passes 8/8 heuristic and 8/8 canned cases.

**Zero newly failing meals and zero previously passing food checks regressed**, including identity, preparation, explicit mass, serving/count, uncertainty and nutrition checks. Existing failures remain measured: branded/unknown identity and matching issues still account for forbidden substitutions, including the complex full-day case's unresolved brand distinction problems. This task does not claim the remaining 18 heuristic or 14 canned failures are solved. Canned results exercise downstream contracts and do not measure live model quality.

The runner retains latency and fallback/cost diagnostics. The local full-run p95 remained below OM-004's 50ms investigation guardrail (n=66 each). These are local measurements excluding provider requests and browser transport, not a production/live latency claim. Usage and cost remain unknown in offline modes. Prompts, model routing, price limits and deadlines were not changed, so no changed AI instruction required live validation. OM-004's live baseline remains explicitly deferred.

## Verification

Environment: macOS Darwin arm64; .NET SDK 10.0.301 / .NET 8.0.19; Node v24.4.0; Playwright 1.64.1 with managed Chromium. Browser contexts are disposable, fixed to October 20 in America/Chicago for reproducibility, with isolated SQLite and provider keys disabled.

```sh
dotnet test tests/Omnom.Tests --no-restore -m:1 -p:UseSharedCompilation=false
npm --prefix frontend test
npm --prefix frontend run test:smoke

dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode heuristic --output .evaluation/OM-005-after-heuristic.json
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode stubbed-ai --output .evaluation/OM-005-after-stubbed-ai.json
```

Before commands used the corresponding `OM-005-before-…` filenames before editing parser behavior. Successful evaluation exit means completed measurement; known correctness failures remain in the report.

- **167 backend tests passed**, zero skipped. This adds 35 correction/unit/negation cases to the 132-test OM-004 baseline, covering all parser modes, poisoned AI excerpts/arithmetic, qualifier preservation, unresolved ambiguities and network-forbidden references.
- **29 frontend regressions passed**, zero skipped. Four new cases cover corrected masses, drink-ounce assumptions, unknown-volume/container reuse guards and additional named checked portions.
- **20 desktop/mobile browser scenarios passed**, zero retries/skips. The new real-API scenario verifies correction → review → save/reload, visible volume/density assumptions, no horizontal overflow and blocked saving for an unknown bottle size. [Browser results](OM-005-smoke-results.json); [inspected mobile review](om-005-units-mobile.png).
- The smoke command passed the required frontend type check/production build and backend build. `backend/wwwroot/` is synchronized; package/footer version remains beta 0.0.2 because no deployment was requested.
- Both full offline corpus runs completed 66/66 before/after; sample IDs, hashes, metrics and per-food regression checks are in the JSON evidence.
- Local documentation targets/anchors and `git diff --check` passed.

Cached package metadata still produces NU1900 for the unreachable NuGet vulnerability service; tests/builds succeed. Browser output includes the existing color-environment warning. No live OpenRouter/USDA evaluation, hosted PostgreSQL, real mobile hardware or production deployment was performed.

API response and backup schemas are unchanged; internal amount metadata is ignored during JSON deserialization/serialization. Existing saved meals/favorites are snapshots and are not rewritten. No database or reference seed change was needed. Next Ready implementation: **OM-006 — Branded reference search and replacement**, retaining its OM-004 comparison gate and known failure evidence.
