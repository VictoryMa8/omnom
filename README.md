# omnom AI

> **AI-assisted food and macro tracker geared toward weightlifters.**
> Describe what you ate in plain English. omnom AI helps review portions, shows assumptions, and tracks your daily nutrition in a calm, light interface.

Built with **C# (.NET 8 Web API)**, **Vue 3**, **Tailwind CSS**, **Pinia**, **SQLite (EF Core)**, and **OpenRouter Free LLMs**.

> 📖 **Onboarding as an engineer or junior developer? Read the comprehensive [Developer Architecture & Codebase Guide (DEV_README.md)](DEV_README.md) for deep dives into every layer of backend, frontend, and UI telemetry.**

---

## ✨ Features

- **Review assumptions**: Tap estimated portions, nutrition estimates, or default food choices to edit details before saving. Food names and all nutrition values are editable.
- **Nutrition review prompts**: Unusual portions, energy density, and calorie/macro mismatches are highlighted without blocking valid entries.
- **Undo meal changes**: Saving or deleting offers Undo for 12 seconds. Hover or focus the notification to keep it available. Restored meals keep their original date and position.

- **⚡ Natural Language Meal Parsing**: Type or speak (e.g. *"8oz chicken breast, 1.5 cups jasmine rice, and 2 eggs"*) and Omnom extracts individual items, quantities, and weights.
- **🍗 Interactive Clarification Chips**: Non-blocking chips right on the preview card (e.g. `[Cooked / Raw]`, `[+1 tbsp Olive Oil]`, `[93/7 vs 80/20 Beef]`) with smart defaults. Tap once to adjust or confirm right away.
- **🛡️ USDA-Verified Bodybuilding Staples**: Pre-seeded with 100+ authoritative staple foods (chicken, beef cuts, eggs, whey, oats, rice, peanut butter, olive oil) for instant offline lookups, backed by live USDA FoodData Central API.
- **🎯 Lifter Quick Macro Adjustments**: Adjust daily calorie and macro targets in 5 seconds with one tap (Lean Bulk, Cut, Maintenance, Refeed / High-Carb day).
- **⏱️ Chronological Timeline Feed**: Track meals with timestamps and **running cumulative macros** after every meal.
- **🎙️ Built-in Voice-to-Text**: Fast logging with microphone support via the Web Speech API.
- **💾 Browser-local diaries**: Each visitor gets their own diary in this browser, with backup export/import. No shared PIN.
- **📦 Single-Container Free Deployment**: Single Dockerfile bundling both C# backend and Vue SPA into one image, deployable for free on Render, Fly.io, or Vercel.

---

## 🚀 Quickstart (Local Development)

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)

### 1. Run the Backend (.NET 8 Web API)
```bash
cd backend
dotnet run
```
The backend will start at `http://localhost:5000` (or `http://localhost:5200`), auto-initialize `omnom.db` in SQLite, and seed the bodybuilding staple foods.

### 2. Run the Frontend (Vue 3 + Vite)
In a separate terminal:
```bash
cd frontend
npm install
npm run dev
```
Open `http://localhost:5173` in your browser. Vite will automatically proxy API calls to the backend.

---

## 🐳 Docker Deployment (1-Command)

To run both backend and frontend together in a single container:

```bash
docker compose up -d
```
Open `http://localhost:8080`. Your data is persisted in the `omnom_data` SQLite volume.

---

## ☁️ Free Cloud Deployment

### Deploying to Render (Free Web Service)
1. Push your repository to GitHub.
2. In Render, click **New +** -> **Web Service**.
3. Select your repository and choose **Docker** runtime.
4. Set the following Environment Variables (optional):
   - `OPENROUTER_API_KEY`: The server OpenRouter key (required for AI parsing).
   - `PUBLIC_DIARY=true`: Allow visitors to parse meals without a PIN; diaries stay in their browser.
   - `USDA_API_KEY`: `DEMO_KEY` (or your free USDA key).
5. Click **Deploy Web Service**!

---

## 🧪 Running Tests

To run the backend test suite:
```bash
cd tests/Omnom.Tests
dotnet test
```

---

## 🔑 Configuration Options

| Environment Variable | Description | Default |
|---|---|---|
| `OPENROUTER_API_KEY` | OpenRouter API Key for free LLMs | Set in the root `.env` or server environment |
| `PUBLIC_DIARY` | Public hosted mode: AI parsing only; diaries stay in each browser | `false` locally; `true` on Vercel |
| `APP_PASSCODE` | Optional master PIN for a private self-hosted diary | None (open access) |
| `USDA_API_KEY` | USDA FoodData Central API key | `DEMO_KEY` |
| `DATA_DIR` | Directory for SQLite `omnom.db` | Local working directory |

### Server-managed AI

Create a repository-root `.env` using `.env.example` and set `OPENROUTER_API_KEY`.
The backend loads it when launched from the repository root or `backend/`;
Docker Compose reads it automatically. Existing environment variables take precedence.
Restart the backend after changing the key. The `.env` file is excluded from version control and Docker builds.

AI parsing uses `z-ai/glm-5.2`. The key and model cannot
be changed in the app; previous database settings are ignored. If the model is
unavailable, the existing local meal-parser fallback still applies.

### Vercel deployment

The project is linked to `victorys-projects-c1cb4594/omnom`. `Dockerfile.vercel`
builds the existing Vue and .NET app as a Vercel container service.

Vercel must have `DATABASE_URL` and `DATABASE_URL_UNPOOLED` from a Neon database,
plus `OPENROUTER_API_KEY`, `PUBLIC_DIARY=true`, and `PORT=8080`.
Hosted public mode does not ask visitors for a PIN. Diaries stay in each
browser, with backup export/import; they do not sync between devices.
The unpooled database URL is used to serialize schema creation during cold starts.
Local development continues to use SQLite unless `DATABASE_URL` is configured.

After accepting Neon's marketplace terms, provision its free plan:

```bash
vercel integration add neon --name omnom-db --plan free_v3 --metadata region=iad1 --metadata auth=false --no-env-pull --scope victorys-projects-c1cb4594
vercel deploy --no-wait --scope victorys-projects-c1cb4594
```

For a production release, use `vercel deploy --prod --no-wait`.
No Git repository is currently configured, so deployments use the CLI.

Public parsing is limited by shared database counters (30 AI requests per hour)
and a 4,000-character description cap. If AI takes longer than 18 seconds, the
server falls back to the local parser. The whole request has a 35-second deadline.
Vercel Bot Protection challenges non-browser clients; humans pass a one-time
browser check and then use the app without a PIN.

On your phone, open the deployed HTTPS URL and optionally choose **Add to Home Screen**
from the browser's share/menu options. Export a diary backup before switching browsers or devices.
