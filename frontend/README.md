# Omnom frontend

Vue 3, TypeScript, Pinia, Tailwind CSS, and Vite. See the root [README](../README.md) for prerequisites/configuration and the [developer guide](../DEV_README.md) for current architecture.

From the repository root:

```sh
npm --prefix frontend ci
npm --prefix frontend run dev -- --strictPort
```

Run the backend separately with `dotnet run --project backend/Omnom.Api.csproj --launch-profile http`. Open `http://localhost:5173`; `/api` proxies to `http://localhost:5027`. Changing origin creates a different browser diary.

```sh
npm --prefix frontend test
npm --prefix frontend run build
```

The build type-checks and replaces `backend/wwwroot/` with production assets. The active diary/targets are browser-local; Exact logging still calls the backend. Planned work is in [ROADMAP.md](../ROADMAP.md).

For isolated desktop/mobile browser checks, install Chromium with `npm --prefix frontend run test:smoke:install`, then run `npm --prefix frontend run test:smoke`. See [the smoke suite guide](tests/smoke/README.md) for reports and release gates.
