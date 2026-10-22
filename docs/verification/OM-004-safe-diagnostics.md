# OM-004 — Safe parsing diagnostics

Date: **October 20, 2026 (America/Chicago)**. Owner: Codex. Status: **Instrumentation complete; live baseline explicitly deferred after the free-availability preflight. OM-004 remains partial.**

Revision: `ef9fee37af5304f654ccab1faad848afaffefc49` plus this uncommitted OM-004 change. The starting working tree was clean. Machine-readable [offline evidence](OM-004-results.json), [browser results](OM-004-smoke-results.json), and [public endpoint snapshot](OM-004-provider-preflight.json) record execution context. There was no commit, push, deployment, schema change, or version increment.

## Delivered

- A correlation ID follows each application parse through extraction, model attempts, matching and remote USDA lookups. HTTP calls inherit the ASP.NET activity trace ID; direct calls generate an ID. Concurrent parses have separate contexts.
- Structured `ParseDiagnostic` events (ID 4004) record stage duration, requested and actual model, HTTP status, extracted-item count, final reference/estimate/unresolved counts, fallback category, and prompt/completion tokens and cost when supplied. Missing metadata is null. Unrecognized actual-model IDs become `other` rather than logging arbitrary provider text.
- Fixed categories distinguish provider HTTP/authentication/rate-limit failures, privacy/credit/parameter/endpoint rejections, invalid or truncated/filtered output, missing configuration, timeout, caller cancellation, API request deadline and the application's shared hourly rate limit. Client disconnection and the internal AI deadline are distinguishable.
- Parser and USDA failures no longer log exception objects or meal/search text. USDA no longer emits the query in a warning. Default typed HttpClient loggers are removed so changing log verbosity cannot expose query/API-key URLs. Synthetic redaction tests inspect rendered messages and structured fields, including poisoned exception messages, provider bodies, reasoning and model metadata.
- The evaluation runner accepts comma-separated case IDs and 1–10 repetitions. It retains the same application diagnostics per observation and computes nearest-rank p50/p95 with sample counts. Completed/scored observations (including local fallback) and failed/unscored observations have separate latency denominators. Token/cost sums include coverage counts; no observed metadata means unknown, not zero. Transport failures also count as attempts missing cost.

Model ordering, prompts, zero-price provider filters, semantic validation, JSON fence/substring extraction, primary/shared/request/USDA deadlines and review payloads are preserved. Application diagnostics contain no diary text, food identity, search terms, credentials, response bodies or reasoning. They do not create a new diary store or external analytics dependency.

## Live baseline: preflight stopped before completions

The user explicitly authorized a fixed synthetic subset with **at most 12 HTTP completion attempts**, zero-price routing only. On October 20, the official [provider-routing documentation](https://openrouter.ai/docs/guides/routing/provider-selection) confirmed price-based filtering through `provider.max_price` and parameter filtering through `require_parameters`. The [limits documentation](https://openrouter.ai/docs/api_reference/limits) was also checked. Credentials were not read or needed for this preflight.

The public [Ling endpoint API](https://openrouter.ai/api/v1/models/inclusionai/ling-3.1-flash/endpoints) listed DeepInfra and Novita, each with prompt price `0.000000075` USD/token and completion price `0.000000225` USD/token. Both are above zero. The [Apodex free endpoint API](https://openrouter.ai/api/v1/models/apodex/apodex-1.1-mini:free/endpoints) returned HTTP 200 with an empty endpoint list. The dated public responses are preserved in [the snapshot](OM-004-provider-preflight.json).

Neither configured model therefore had a listed zero-price endpoint. Following OM-004's fail-closed rule, **no live completion was sent** and no paid fallback or replacement model was enabled. This is an availability limitation, not a failed accuracy experiment.

| Planned live measurement | Recorded result |
|---|---|
| Dataset | OM-003 v1 / 1.0.0; hashes retained in offline evidence |
| Fixed subset | MASS-001, PREPARATION-001, QUALIFIERS-001, BRANDS-001, CORRECTIONS-001, FULL-DAY-002 |
| Planned repetitions | 2 per fixture, 12 observations; one shared cap of 12 actual attempts including backups |
| Requested / actual completion models | None / none; configured model IDs listed above |
| Sent / successful / failed / rate-limited completions | 0 / 0 / 0 / 0 |
| Attempted / unattempted observations | 0 / 12 |
| Live accuracy, fallback rate, p50/p95 | Unknown; n=0 |
| Observed tokens / cost | Unknown; no completion metadata |

The repeated subset was verified **offline with canned extraction**: 12/12 observations completed, 8 fully correct, no provider/network attempts. This proves the selection/repetition/report path, not live model quality.

Before resuming, recheck the configured endpoints. If free availability has not returned, select and verify a new server-owned free model in a separately documented routing change before measurement. Then use the application runner with `--mode live-ai --allow-live --max-requests 12`, the fixed `--case` list above and `--repetitions 2`, with the key exported privately. Stop on the first quota/auth/cost/cap signal. Backups consume the same cap and can leave observations unattempted; report those explicitly. Do not substitute a generic model prompt or weaken zero-price routing.

## Offline baseline and targets

Both full corpus modes used version 1.0.0 with unchanged dataset/reference hashes, frozen local references, isolated in-memory SQLite, no `.env` load, no remote USDA and no provider networking. No fixture expectation changed. Every case score and aggregate metric matches [OM-003's baseline](OM-003-meal-evaluation.md), including its known failures.

| Mode | Completed | Fully correct | Fallbacks | End-to-end p50 / p95 | Final reference / estimate / unresolved items |
|---|---|---|---|---|---|
| Heuristic | 66/66 | 41/66 | 66/66, deliberately forced | 0.616 / 3.940 ms (n=66) | 76 / 2 / 11 |
| Canned AI pipeline | 66/66 | 52/66 | 1/66, deliberately invalid fixture | 0.393 / 4.564 ms (n=66) | 76 / 2 / 8 |

Extraction diagnostics report 66 forced fallbacks in heuristic mode; canned mode reports 65 valid extractions and one `invalid-output` fallback. Matching counts are separate from extraction success. Final correctness still comes from the existing independent fixture scorer. The JSON contains per-stage durations, correlation IDs, each observation and all metric denominators. Synthetic forced heuristic errors classify as `internal-failure`; this is deliberate offline injection.

Percentiles use sorted samples at `ceil(p × n) − 1`, include the first invocation without warmup, and exclude database/server startup, browser/API transport and remote USDA. These sub-millisecond local figures cannot stand in for live or production latency; they are a single diagnostic run, not a statistically controlled performance benchmark. Usage/cost fields are unknown in both offline modes.

Targets for OM-005–OM-007: preserve all existing passing hard-rule cases; identify improvements and regressions by stable case ID; retain at least the current 41/66 heuristic and 52/66 canned fully-correct baselines without changing expectations to hide failures. Use the pinned offline p95 <50 ms on this environment as an initial investigation guardrail, not a timing assertion in CI. Establish live extraction correctness, fallback and latency targets **after an actual measured free-only baseline**; they remain an open OM-004 criterion. Keep the existing 15-second primary cap, 40-second shared AI budget, 50-second request deadline and 2-second USDA lookup deadline in the meantime.

## Verification

Environment: macOS Darwin arm64; .NET SDK 10.0.301 with .NET/ASP.NET Core 8.0.19; Node v24.4.0; Playwright 1.64.1 / Chromium 156.0.8078.4. Dates in machine-readable reports are UTC and correspond to October 20 locally.

```sh
dotnet test tests/Omnom.Tests --no-restore -m:1 -p:UseSharedCompilation=false
npm --prefix frontend test
npm --prefix frontend run test:smoke

dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode heuristic --output .evaluation/OM-004-heuristic.json
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode stubbed-ai --output .evaluation/OM-004-stubbed-ai.json
dotnet run --project tools/Omnom.Evaluation --no-build -- \
  --mode stubbed-ai --case MASS-001,PREPARATION-001,QUALIFIERS-001,BRANDS-001,CORRECTIONS-001,FULL-DAY-002 \
  --repetitions 2 --output .evaluation/OM-004-fixed-subset.json
```

- **132 backend tests passed**, zero skipped, including 24 diagnostic/redaction/API cases and report-summary coverage. Existing routing/price/cancellation tests remain green.
- **25 frontend regressions passed**, zero skipped.
- **18 browser scenarios passed**, zero retries/skips, across disposable desktop/mobile contexts and an isolated .NET/SQLite host. The smoke command also passed the frontend type check/production build and backend build; generated assets stayed unchanged. It verifies the actual Exact/public API boundary and stubbed review/storage flows.
- Both full offline evaluations completed all 66 cases, with **zero per-case score deltas** and unchanged aggregate metrics. The repeated subset completed all 12 observations. Unknown subset IDs and repetitions=0 each exited 2 before provider setup.
- `git diff --check` and local documentation path/anchor checks passed.

The first sandboxed .NET invocation could not open an MSBuild pipe; the rerun with local socket/pipe permission passed. Cached restore metadata emits NU1900 because the NuGet vulnerability service was unreachable; builds/tests themselves succeeded. Existing dependencies were used. The smoke run produced only the existing `NO_COLOR`/`FORCE_COLOR` environment warning. Hosted PostgreSQL, real USDA, real mobile hardware/Safari and production deployment were not exercised. Live completion evaluation was explicitly deferred for the measured availability reason above.

Remaining OM-004 criteria: run the fixed subset through current free models when available; publish model/request/usage accounting, live extraction-versus-matching results and live latency/fallback baselines; set evidence-based live targets. The next independent Ready implementation is **OM-005**.
