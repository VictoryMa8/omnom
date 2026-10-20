# OM-003 verification: meal evaluation corpus and runner

Completed October 20, 2026. Owner: Codex. Dataset **1.0.0** contains **66 synthetic prompts / 86 expected foods**: eight in each of the eight required categories, plus two full-day descriptions including the original complex guide example. [Corpus/runner guide](../../tests/fixtures/meal-evaluation/README.md) defines contracts, units, metric denominators, commands, and limitations.

## Verification

- **103 backend tests passed**, no failures/skips, including 11 new corpus/scoring/report/request-guard tests.
- **25 frontend regressions passed** and **18 desktop/mobile browser checks passed**, no failures/skips. [Release smoke snapshot](OM-003-release-smoke.json).
- Frontend type-check/production build and .NET builds passed. Version **beta 0.0.2** was built for the authorized Git push; deployment verification is recorded in the release handoff/Vercel commit check.
- Both full-corpus offline modes completed all 66 cases with **zero provider HTTP requests and zero offline networking attempts**. No live mode, AI credential, or external USDA lookup was used.
- CLI controls verified: missing live key, live budget flags in offline mode, and an unknown case ID exit 2; one perfect canned mass case with `--require-perfect` exits 0; a known brand failure with that flag exits 1. Fake-handler tests prove actual-attempt caps, pre-send zero-price rejection, quota stops, and nonzero-cost stops without contacting a provider.
- Corpus reference arithmetic/serving checks and maximum one-to-one matching, missing/duplicate items, qualifier masking, explicit mass/nutrient tolerance boundaries, uncertainty, negations, unknown-food substitution, and Markdown report rendering all passed.

Commands from repository root:

```sh
dotnet test tests/Omnom.Tests --no-restore --verbosity quiet
npm --prefix frontend test
npm --prefix frontend run test:smoke
dotnet run --project tools/Omnom.Evaluation --no-build -- --mode heuristic --output .evaluation/heuristic.json
dotnet run --project tools/Omnom.Evaluation --no-build -- --mode stubbed-ai --output .evaluation/stubbed-ai.json
```

.NET runtime 8.0.19 on macOS arm64; .NET SDK 10.0.301, Node 24.4.0 and Playwright 1.64.1 for release checks. These reports identify base revision `11239373c2da77dea10da4ff170903f70244f70f` plus dirty paths; they were generated before the requested release commit. JSON contains timestamps, all case results, source/quantity/nutrition evidence, environment and hashes. NuGet printed an unavailable vulnerability-catalog warning during local builds; test/evaluation execution succeeded. This is not a vulnerability-audit pass.

## Measured baseline

| Metric | Forced heuristic | Canned AI extraction + real downstream pipeline |
|---|---|---|
| Identity precision | 76/89 (85.4%) | 77/86 (89.5%) |
| Identity recall | 76/86 (88.4%) | 77/86 (89.5%) |
| Fully correct meals | 41/66 (62.1%) | 52/66 (78.8%) |
| Explicit mass accuracy | 52/61 (85.2%) | 56/61 (91.8%) |
| Known portion accuracy | 53/64 (82.8%) | 59/64 (92.2%) |
| Quantity/unit accuracy | 52/64 (81.2%) | 59/64 (92.2%) |
| Qualifier errors | 8 | 6 |
| Forbidden substitutions | 11 | 11 |
| Excluded ingredient violations | 1 | 0 |
| Unresolved / estimated items | 11 / 2 | 8 / 2 |

These are completed evaluations with observed correctness failures, **not a claim that every parser case passed**. The 41/66 and 52/66 fully correct meal figures measure different controlled input paths, not two live model scores or an improvement experiment. The canned path intentionally includes one invalid completion to verify fallback. Strict qualifier checks use actual food names, so even an unresolved food can flag lost preparation/brand wording. Missing foods stay in recall and explicit-weight denominators.

Results expose the work planned under OM-005 through OM-007: corrections/negations and ambiguous beverage units, brand identity/generic substitution, vague portions/recipe uncertainty, and supported reference choices. Parser behavior was not changed to make this corpus pass. Any future baseline comparison must keep dataset/reference hashes and mode consistent.

- [Heuristic JSON](meal-evaluation-v1/heuristic.json) / [case violations](meal-evaluation-v1/heuristic.md)
- [Canned-AI JSON](meal-evaluation-v1/stubbed-ai.json) / [case violations](meal-evaluation-v1/stubbed-ai.md)

Dataset SHA-256: `7AAF0FDBB0744B84BBA2466B39EF3ABBB061213746519AE49D07C7600B147594`. Reference SHA-256: `30558462F409F1E754F6A153874F6DCA00DF014618F5191FEBDBD0B512901B3A`.

## Scope and next step

All six OM-003 acceptance criteria are complete. Live mode exists but remains unrun: it requires an explicit flag, an exported key and a real HTTP-attempt cap, retains the server's zero-price routing, stops cleanly on quota/auth/cost/budget/cancellation, and records partial coverage/unknown usage honestly. Live reference resolution deliberately stays local to isolate extraction from USDA variability. This task supplies the offline baseline; **OM-004** is next for safe diagnostics and a bounded live application benchmark.
