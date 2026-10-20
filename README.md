# omnom AI

AI-assisted food and macro tracking for weightlifters. Describe a meal, review its portions and nutrition, and save it to a diary in your browser. Meal time is optional.

Built with .NET 8 Web API, Vue 3, TypeScript, Vite, Tailwind CSS, Pinia, EF Core, and OpenRouter.

- [Roadmap](ROADMAP.md): current baseline, prioritized tasks, acceptance criteria, and handoffs.
- [Developer guide](DEV_README.md): architecture, contracts, storage, and verification.
- [Agent instructions](AGENTS.md): contributor rules and commands.
- [Meal evaluation](tests/fixtures/meal-evaluation/README.md): versioned synthetic corpus, offline baselines, metrics, and opt-in capped live mode.
- [Releases](RELEASES.md): numbered beta deployments and release procedure; the current build's version appears in the app footer.

## Features

- Text or browser voice input, with AI-assisted and deterministic Exact logging modes.
- Editable review: change amounts, replace a food from local/cached references, enter checked label values, and inspect assumptions before saving.
- Reference-backed preparation and cooking-fat clarification chips where suitable records exist.
- Repeat/favorite meals and optional remembered food aliases and portions. Explicit weights override remembered portions; favorites remain snapshots.
- Meal editing, deletion, and Undo; insertion-order running totals with optional time badges.
- Effective-date macro targets and a seven-day view using logged-day averages and historical targets.
- Browser-local diaries with JSON backup export/import. No account or device sync.

The active UI is light, with green accents. Earlier dark-theme tokens and unused PIN/settings components remain in the repository; they do not describe the active application shell.

## Local quickstart

Run these commands from the repository root. The examples use a POSIX shell (macOS/Linux).

Prerequisites:

- .NET 8 SDK, or a newer compatible SDK with the .NET 8 ASP.NET Core runtime installed. Check `dotnet --info` and `dotnet --list-runtimes`.
- Node.js 20.19+ in the 20.x line, or 22.12+ and newer supported releases, with npm. These minimums come from the installed Vite/plugin engine requirements inspected October 20, 2026; Node 24 was used for local verification.
- Available ports **5027** (backend HTTP) and **5173** (frontend). Port 5000 is deliberately avoided because macOS AirPlay can occupy it.

Install dependencies:

```sh
dotnet restore backend/Omnom.Api.csproj
npm --prefix frontend ci
```

Start the backend in one terminal:

```sh
dotnet run --project backend/Omnom.Api.csproj --launch-profile http
```

Start the frontend in another terminal:

```sh
npm --prefix frontend run dev -- --strictPort
```

Open [http://localhost:5173](http://localhost:5173). Vite proxies `/api` to [http://localhost:5027](http://localhost:5027). The backend's development Swagger UI is at [http://localhost:5027/swagger](http://localhost:5027/swagger). The optional `https` launch profile uses HTTPS port 7252 and the same HTTP port 5027; the documented development path uses HTTP and needs no development certificate.

### No-key mode

A fresh checkout needs no `.env` file or API key for a basic local meal. Choose **Exact**, enter `cooked chicken breast 100g, jasmine rice 150g`, click **Review meal**, and save after checking the result. Exact mode still calls the backend; it uses deterministic parsing and local references without contacting AI or USDA. It is not a fully offline web app.

In **AI** mode, a missing OpenRouter key triggers the local heuristic fallback and a visible review notice. Nutrition lookup can still contact USDA for foods without local matches, so use Exact for deterministic no-provider checks. Foods without usable nutrition need values entered in review, a replacement reference, or removal before saving.

If you already have a root `.env` or exported provider/database settings, see the [isolated verification command](docs/verification/OM-001-local-startup.md#isolated-startup) for explicit overrides that preserve existing data and disable AI calls.

### Live AI mode

Copy `.env.example` to `.env`, set your own `OPENROUTER_API_KEY`, and restart the backend. Never commit the key. The example also enables `PUBLIC_DIARY=true`. The loader reads `.env` in the process working directory, or the repository root when launched from `backend/`; existing environment variables take precedence, including explicitly empty values.

The key and model list are server-managed. The active UI has no model selector or API-key editor. The code inspected October 20, 2026 configures `inclusionai/ling-3.1-flash`, then `apodex/apodex-1.1-mini:free`. Every attempt applies zero maximum provider prices. Reasoning is requested with a shared 6,000-token output/reasoning budget; the backup requests JSON-object mode, and both outputs are validated locally. These are code settings, not a fresh guarantee of provider availability or capabilities. Verify current provider documentation before changing them.

The primary has a 15-second cap inside a shared 40-second AI budget. The API deadline is 50 seconds; the browser timeout is 55 seconds. Invalid output, provider failures, and exhausted free quotas can trigger visible local fallback. See the dated [intelligence review](docs/intelligence-review.md) for earlier provider checks and limitations; broader live evaluation is tracked in OM-004.

## Where data lives

Meals, targets, favorites, and remembered foods live in `localStorage` under `omnom_diary_v1`, scoped to the browser profile and origin (scheme, hostname, and port). Switching between `localhost` and `127.0.0.1`, the development and production ports, or devices gives a different diary. People sharing a browser profile/origin share the same diary.

Use the settings button to **Export your diary** before clearing browser data or moving to another origin/device. **Restore a backup** replaces the current browser diary after confirmation; it does not merge diaries. Older version-1 backups without favorites/defaults remain readable. Backup recovery and cross-tab conflict hardening are future work under OM-008.

The server database stores food references/cache, settings, request counters, and legacy private diary data. A Docker volume or hosted PostgreSQL database does **not** back up or sync the active browser diary. Meal descriptions in AI mode are sent to the backend and may be sent to the AI provider; browser-local storage does not mean all parsing stays on the device.

## Configuration

| Variable | Behavior |
|---|---|
| `OPENROUTER_API_KEY` | Server-only AI credential. Absent/empty: heuristic fallback in AI mode. |
| `PUBLIC_DIARY` | Default false; true exposes parse and food-reference search while returning 404 for legacy diary, target, settings, and auth endpoints. It does not change browser persistence. |
| `USDA_API_KEY` | USDA lookup key; defaults to `DEMO_KEY`. A legacy stored `UsdaApiKey` takes precedence. Exact mode does not use remote lookup. |
| `DATA_DIR` | SQLite directory when `DATABASE_URL` is empty; otherwise SQLite defaults to the backend process working directory. |
| `DATABASE_URL` | Nonempty: select PostgreSQL instead of SQLite for server data. |
| `DATABASE_URL_UNPOOLED` | Optional direct PostgreSQL connection for serialized startup schema creation/seeding. |
| `APP_PASSCODE` | Legacy server endpoint PIN; the active UI does not mount the unlock screen. Leave unset for ordinary local browser-diary use. |
| `REQUIRE_PASSCODE` | Forced private hosted PIN mode requires `APP_PASSCODE`, `Jwt__Secret`, and `DATABASE_URL` when public mode is off. |
| `Jwt__Secret` | JWT signing secret for legacy PIN access; without it, signing material is process-local. |
| `ASPNETCORE_URLS` | Explicit binding for runs without a launch profile; containers bind HTTP port 8080. |

The backend initializes its schema and reference records at startup. This uses `EnsureCreated` and targeted compatibility SQL, not a general EF migration pipeline. See [the developer guide](DEV_README.md#server-storage-and-access) before schema changes.

## Build and test

```sh
dotnet test tests/Omnom.Tests
npm --prefix frontend test
npm --prefix frontend run build
```

The frontend build includes TypeScript checking and replaces `backend/wwwroot/` with production assets. Run it after frontend `.vue` or `.ts` edits and include the generated assets. For a single-process local preview, stop the Vite server and open [http://localhost:5027](http://localhost:5027) while the backend runs; this is a separate browser origin from port 5173.

For the reproducible desktop/mobile browser gate, run `npm --prefix frontend run test:smoke:install` once, then `npm --prefix frontend run test:smoke`. See the [smoke suite instructions](frontend/tests/smoke/README.md) for isolation, reports, and the release checklist.

The current [OM-001 verification report](docs/verification/OM-001-local-startup.md) records the setup and browser checks. The repeatable browser release suite is documented under OM-002; live accuracy evaluation remains OM-004.

## Containers and hosting

```sh
docker compose up --build -d
```

Open [http://localhost:8080](http://localhost:8080). [docker-compose.yml](docker-compose.yml) maps that port, mounts server SQLite data at `/data`, and forwards the listed OpenRouter/USDA/PIN variables. Compose reads root `.env` for interpolation; only variables listed in the service environment are forwarded. In particular, the current Compose file does not forward `PUBLIC_DIARY`; pass/configure it explicitly if you need public endpoint restrictions.

[Dockerfile](Dockerfile) builds the Vue bundle and .NET API into one image. [Dockerfile.vercel](Dockerfile.vercel) and [vercel.json](vercel.json) describe the repository's Vercel container-service deployment; its image defaults to public mode on port 8080. Hosted PostgreSQL uses `DATABASE_URL` and optionally `DATABASE_URL_UNPOOLED`; provision deployment credentials in the hosting environment, never the frontend.

Hosting plan availability, pricing, project linkage, protection settings, and live deployment state are not established by these checked-in files. Inspect the intended hosting project before provisioning or deploying. A successful local build does not mean a production release occurred.
