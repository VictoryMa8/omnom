# Meal evaluation corpus and runner (OM-003)

`v1/cases.json` is dataset **1.0.0**: 66 authored synthetic meal descriptions, 86 expected foods, eight cases in each required category (`mass`, `preparation`, `qualifiers`, `brands`, `vague`, `mixed`, `corrections`, `unknown`) plus two full-day cases. The final case preserves the original complex guide example. These are intended semantics, including failures the current parser still makes; they are not golden copies of its current output.

`v1/references.json` freezes the 59 local seed records inspected October 20, 2026. Known nutrient expectations are scaled from those **code reference contracts**, not a new independent USDA/clinical accuracy claim. Zero-calorie drink expectations separately cite the app's zero-calorie semantic contract; unknown container mass is deliberately not graded. Synthetic unknown brands/foods have no exact macro truth. Vague portions and dishes without recipes leave grams/nutrients unspecified and require visible uncertainty.

## Commands (repository root)

Prerequisite: the README's compatible .NET SDK/.NET 8 runtime. Initial restore/build may require package-registry access; evaluation itself is offline in the first two modes.

```sh
dotnet build tools/Omnom.Evaluation

dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode heuristic --output .evaluation/heuristic.json

dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode stubbed-ai --output .evaluation/stubbed-ai.json
```

Each command writes paired JSON and Markdown reports. `.evaluation/` is excluded from Git, Docker, and Vercel uploads. Curated dated baselines belong in `docs/verification/`; never publish real secrets, private diary data, or provider response bodies. The runner only processes synthetic fixture prompts and stores the resulting application foods, not raw provider responses or hidden reasoning.

- **heuristic** deliberately makes the AI interface fail, then exercises the application's real heuristic parser and local reference matcher. No provider key is read or required.
- **stubbed-ai** supplies the fixture's explicitly authored extraction JSON (including fenced JSON and an intentionally invalid case) to the real application parser, quantity normalization, reference matching, and clarification pipeline. The canned extraction is an input, not generated at runtime from expected outputs. Its results measure those downstream contracts **given this extraction**, not model intelligence. One invalid response intentionally exercises fallback.
- Both use a fresh in-memory SQLite database, seeded with the real initializer; all USDA lookups are forced local. An HTTP handler rejects/records any unexpected offline network attempt. No `.env`, server diary, or browser storage is loaded. The runner refuses a reference snapshot that differs from the current code, rather than silently moving the evaluation target.

Filter one stable case or make any correctness failure exit nonzero:

```sh
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode heuristic --case MASS-001 --require-perfect
```

For a fixed subset with repeated observations, use comma-separated stable IDs and `--repetitions 1..10` (default 1). The request cap still applies across the entire live run, including all repetitions and backups:

```sh
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode stubbed-ai --case MASS-001,PREPARATION-001,QUALIFIERS-001,BRANDS-001,CORRECTIONS-001,FULL-DAY-002 \
  --repetitions 2 --output .evaluation/fixed-subset.json
```

Unknown IDs and invalid repetition counts fail before provider setup. Reports distinguish distinct fixtures from selected observations, identify each repetition, and count partial/unattempted observations explicitly.

Default exit 0 means **evaluation completed**, not every meal was correct. Exit 1 with `--require-perfect` means a scored case failed a constraint. Exit 2 means invalid arguments/fixtures, blocked offline networking, or runner errors. Exit 3 means incomplete/cancelled/deadline/budget/quota/auth/cost-stopped execution. Unscored and unattempted cases are explicit and never presented as correct. Use the same dataset/reference hashes and mode when comparing reports; changing canned inputs or fixture expectations changes the measurement.

## Explicitly enabled live mode

OM-003 does **not** require or perform a live run. Verify current provider documentation/free availability and the intended request budget before using this mode (the live benchmark is OM-004). Export the key privately in your own environment; do not put it in an argument, report, shell history example, or Git file. The runner intentionally does not load `.env`.

```sh
# Requires an already exported OPENROUTER_API_KEY; sends only synthetic fixture prompts.
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode live-ai --allow-live --max-requests 3 --case MASS-001 \
  --output .evaluation/live-subset.json
```

All three are required: `--mode live-ai`, `--allow-live`, and `--max-requests 1..100`, plus a nonempty exported key. The cap counts **actual HTTP attempts**, including backup models and failed requests; it is not a case-count allowance. Current server-owned model IDs and every zero-price prompt/completion/request/image filter are preserved by the real `OpenRouterService` and checked again before send. No paid fallback, tools, or plugins are introduced. The only permitted live endpoint is OpenRouter completion over HTTPS; redirects are disabled. Reference matching remains pinned/local even in live mode, so remote USDA variability is excluded.

The first provider 429, 401/403, nonzero reported cost, or exhausted request cap stops further requests and records a reason. A partially processed quota/cap case is not scored as if its heuristic fallback were a live result. Ctrl+C cancels the current case and writes a partial report when cancellation completes. Per-case deadlines use the existing request/AI limits. Requests without cost metadata stay **unknown**; `reportedCostSum` is null when none supplied cost, and missing-cost counts are separate. No cost field is inferred from an absent response or a model's name.

## Fixture contract and authoring

Every case has a stable ID/category, prompt, intended semantics, expected foods, excluded foods, and an independently readable canned extraction. For each food:

- `key`: unique within the case; `aliases`: acceptable identity phrases (`|` separates alternatives).
- `sourceText`: literal excerpt of the prompt containing the intended food/amount. Validation checks it exists.
- `qualifiers` / `forbiddenQualifiers`: actual food-name preparation, brand, lean, or diet constraints. Original source text cannot conceal a nutritionally incompatible chosen reference.
- `grams`: exact expected amount only when supported, otherwise null. `quantityKind` distinguishes explicit mass from a reference serving or an unspecified portion. `quantity` and `unit` are checked only when known.
- `resolution`: `reference`, `unresolved`, or `uncertain`. An unknown food cannot gain macros from an unrelated record. `uncertainFields` requires an estimate/unresolved source label or explicit assumptions; it is not an invitation to invent exact calories.
- `reference`, `nutrition`, `nutritionTolerance`, `evidence`: exact cited snapshot row and per-nutrient expected values/tolerances where defensible. Zero-calorie drinks are the sole zero-valued semantic exception to the rule against exact nutrition for unknown portions.
- `stubResponse`: explicit canned model extraction with verbatim excerpts. `stubFormat` is `json`, `fenced`, or `invalid`. Do not regenerate this from expected foods in the runner; that would hide extraction/matching differences.

Review fixture changes before accepting them. Never edit expectations just to make the current parser pass. Corpus validation and tests check required category coverage, IDs, excerpts, uncertainty, reference arithmetic/servings, and nutrient tolerances. For a new code reference table or changed expectations, create a new dataset version/directory and document the change. The `--export-references <path>` maintenance command can produce a candidate snapshot, but it does not review or update case expectations and should not overwrite the baseline during a measurement. Update the version validation/test contract deliberately when adding a new version.

## Metrics and denominators

Food identity comparison tokenizes words, normalizes simple plurals, and matches declared alias alternatives. It uses maximum-cardinality one-to-one matching, preferring qualifier/gram agreement when identities repeat. A prediction cannot satisfy two expected items. Identity recall is separate from preparation/nutrition correctness: a raw-versus-cooked chicken mismatch can count as an identity match and still fail the meal.

| Metric | Definition |
|---|---|
| Item precision | Identity matches / predicted items in scored cases. Extras are false positives. |
| Item recall | Identity matches / expected items in scored cases. Missing foods are false negatives. |
| Fully correct meals | Cases with every expected food/constraint correct, no extra foods, and no excluded ingredients / scored cases. |
| Explicit-mass accuracy | Correct grams for `explicit-mass` expectations / all such expectations in scored cases, **including missing foods as failures**. Absolute tolerance 0.1 g plus floating-point epsilon. |
| Portion accuracy | Correct grams / all expectations with known grams, including reference-serving expectations. Missing foods fail. |
| Quantity accuracy | Correct count and declared unit / expectations with known quantities. |
| Qualifier errors | Matched foods whose actual chosen name misses a required qualifier or includes a forbidden qualifier. Missing identities are already recall failures. |
| Forbidden substitutions | Resolved matched foods violating qualifier constraints or resolving a food required to stay unknown; also resolved unmatched predictions whose source identifies an intentionally unknown expected food. |
| Exclusion violations | Predicted foods matching any explicitly excluded ingredient. |
| Unresolved/estimated outcomes | Counts by actual `Unparsed` / `Estimated` status in scored predictions. |
| Fallback rate | Scored cases returning the application's local-fallback notice / scored cases. Heuristic mode forces this intentionally. |

Zero-denominator rates are null (Markdown `n/a`), not 100%. Runner/network/deadline failures and incomplete live cases are listed separately and excluded from accuracy ratios; the report includes selected, attempted, scored, and unattempted counts/IDs so a partial run cannot resemble full coverage. All rates are also available by category. Nutrition comparisons use the per-field fixture tolerance (typically 0.2 kcal and 0.15 g macros/fiber) rather than a blanket percentage. Unknown recipes have no nutrition score.

JSON includes dataset/reference SHA-256 hashes, mode, Git revision and dirty paths, timestamps, runtime/platform, every expected-food verdict, actual parsed foods, model/request/cost accounting when relevant, and metric numerators/denominators. Markdown exposes per-case violations for debugging. Unit tests cover scorer matching, missing/duplicate foods, qualifier masking, tolerances, unknowns/negations, uncertainty, report rendering, actual-attempt caps, price filtering, and quota/cost stops.

## Safe diagnostics and latency (OM-004)

The runner collects the same structured diagnostics as the application: correlation ID, extraction and matching durations, extracted item count, final reference/estimate/unresolved counts, each model attempt's requested/actual model and failure category, nullable token counts and cost, and total request duration. It retains no provider body or reasoning. Actual model metadata is limited to server-owned IDs; absent metadata is null and an unrecognized ID is `other`. Application logging also suppresses typed HttpClient URL logs, since USDA requests carry private queries and credentials.

JSON `diagnostics` separates extraction outcomes/fallback categories from matching totals, lists attempt outcomes, and reports nullable usage sums with coverage counts. Failed transports count as attempts with missing cost. End-to-end percentiles use the runner's elapsed time around the application parser, including logging and local fallback. The separate stage timings identify where time went. The method is **nearest rank**, with p50 = sorted[ceil(0.50 × n) − 1] and p95 = sorted[ceil(0.95 × n) − 1]. Scored/completed and failed/unscored observations have separate sample counts and percentiles; no samples means null. These measurements exclude server startup, network between browser/API, and remote USDA, and include the first parser invocation without warmup.

The [October 20 OM-004 evidence](../../../docs/verification/OM-004-safe-diagnostics.md) records unchanged offline correctness and a live-run preflight deferral: the configured primary has paid endpoints and its backup lists none. Reverify the official endpoints before a live run; an existing key or model name is not evidence of free availability. Do not remove the zero-price filters to complete a benchmark.
