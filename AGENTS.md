# AGENTS.md — Omnom Developer & Architecture Guide

Welcome to **Omnom**! This document provides future AI agents and developers with complete context on the architecture, technical decisions, data models, workflows, and design philosophies of the codebase.

---

## 1. Project Overview & Philosophy

**Omnom** is an AI-assisted fitness and nutrition tracking app tailored specifically for **weightlifters and athletes**.

### Core Principles
1. **Frictionless Natural Language Logging**: Users simply type or voice-dictate what they ate (e.g., *"a medium plate of chicken breast, 2 slices aldi boule bread, 150g ground lamb, 120g rice, and 2 tbsp hummus"*).
2. **Time is Optional**: Weightlifters frequently log all food once at night after a tiring day. **Never** require clock time, and **never** auto-assign meal names based on system clock time (e.g., don't call lunch "Late Snack" just because it was logged at 11:30 PM).
3. **USDA-Verified Nutritional Accuracy**: Macros and calories are matched against USDA FoodData Central and pre-seeded high-protein staple databases. Unmatched items must never arbitrarily fallback to irrelevant defaults.
4. **Non-Blocking Clarification Chips**: Instead of multi-turn chat dialogues, the AI offers optional 1-tap clarification chips (e.g., `[Cooked (Default)] [Raw]` or `[0-Cal Spray / None] [+1 tbsp Olive Oil]`).
5. **Athletic, Clean, Lighthearted Tone**: Confident, modern dark-mode aesthetic. **Avoid kid-like emoji clutter** (no `🤤`, `🍪`, `🦖`, `🥣` on every card). Keep it athletic, crisp, and functional with subtle lifter touches.
6. **Immediate User Feedback**: Every user interaction (saving a meal, adjusting macro targets, deleting an item) provides visual confirmation via loading states and toast notifications.

---

## 2. Tech Stack

- **Backend**: C# (.NET 8 Web API)
  - **Database**: SQLite via Entity Framework Core (`omnom.db`)
  - **AI Provider**: OpenRouter Free Tier (rotates across `google/gemini-2.0-flash-lite:free`, `meta-llama/llama-3.3-70b-instruct:free`, `deepseek/deepseek-r1:free`, `qwen/qwen-2.5-72b-instruct:free`, etc.)
  - **Nutrition Data**: USDA FoodData Central API + local verified staple table
  - **Auth**: Passcode protection via BCrypt and JWT
- **Frontend**: Vue 3 (Composition API `<script setup>`), TypeScript
  - **Bundler**: Vite (build target configured to `backend/wwwroot` for single-service deployment)
  - **Styling**: Tailwind CSS (custom theme: `omnom-dark`, `omnom-matcha`, `omnom-strawberry`, `omnom-blueberry`, `omnom-yellow`, `omnom-caramel`)
  - **State Management**: Pinia (`diaryStore`, `settingsStore`, `authStore`, `toastStore`)
  - **Icons**: Lucide Icons (`lucide-vue-next`)
  - **Voice**: Web Speech API (`webkitSpeechRecognition`)
- **Testing**: xUnit with .NET EF In-Memory testing in `tests/Omnom.Tests`

---

## 3. Directory Layout

```
omnom/
├── AGENTS.md                  # This guide
├── README.md                  # Human-facing setup & usage documentation
├── Dockerfile                 # Multi-stage production container build
├── docker-compose.yml         # Container runner
├── backend/
│   ├── Omnom.Api.csproj       # .NET 8 Web API project
│   ├── Program.cs             # Web application bootstrap, DI, SPA fallback
│   ├── Controllers/
│   │   ├── AiController.cs        # /api/ai/parse natural language endpoint
│   │   ├── DiaryController.cs     # /api/diary timeline, meal CRUD
│   │   ├── TargetsController.cs   # /api/targets daily macro targets
│   │   ├── SettingsController.cs  # /api/settings API keys & PIN
│   │   ├── AuthController.cs      # /api/auth passcode validation
│   │   └── FoodSearchController.cs# /api/foods local & USDA search
│   ├── Data/
│   │   ├── AppDbContext.cs        # EF Core SQLite DbContext
│   │   ├── DbInitializer.cs       # Database migrations & staple seed data
│   │   └── Entities/              # MealEntry, MealItem, DailyTarget, FoodStaple, AppSettings
│   ├── Models/
│   │   └── DTOs.cs                # Request/response records
│   ├── Services/
│   │   ├── MealParserService.cs   # OpenRouter LLM call, regex fallback, clarification generator
│   │   ├── UsdaFoodService.cs     # USDA API integration + search algorithm
│   │   └── AuthService.cs         # Passcode hashing & JWT tokens
│   └── wwwroot/                   # Compiled Vue 3 production bundle
├── frontend/
│   ├── src/
│   │   ├── App.vue                # Root shell, toast notifications, auth check
│   │   ├── components/
│   │   │   ├── DailySummaryHeader.vue # Calorie ring, macro progress bars, targets modal trigger
│   │   │   ├── QuickLogInput.vue      # Natural language textarea + voice input
│   │   │   ├── MealConfirmCard.vue    # Editable ingredients, grams stepper, clarification chips
│   │   │   ├── TimelineFeed.vue       # Chronological meal feed
│   │   │   ├── MealCard.vue           # Meal breakdown card with running totals
│   │   │   ├── DateNavigator.vue      # Date switcher (Today / Prev / Next / Picker)
│   │   │   ├── QuickTargetModal.vue   # Preset switcher (Bulk, Cut, Maintenance, Refeed)
│   │   │   ├── SettingsModal.vue      # OpenRouter & USDA API keys, model selector
│   │   │   └── PasscodeModal.vue      # Full-screen lock if passcode enabled
│   │   ├── services/
│   │   │   └── api.ts                 # Axios client, types, and API methods
│   │   └── stores/
│   │       ├── diaryStore.ts          # Active timeline, date navigation, meal/target mutations
│   │       ├── toastStore.ts          # Global banner notifications
│   │       ├── authStore.ts           # Authentication and lock state
│   │       └── settingsStore.ts       # OpenRouter & USDA configuration
│   ├── vite.config.ts                 # Builds directly into backend/wwwroot
│   └── tailwind.config.js             # Custom color palette & fonts
└── tests/
    └── Omnom.Tests/                   # Backend unit and integration tests
```

---

## 4. Key Workflows & Implementation Details

### A. Meal Parsing Pipeline (`MealParserService.cs`)
1. **Prompt Sanitization**: Normalizes user text and extracts meal names (e.g. "Breakfast", "Lunch", "Dinner", "Post-Workout" if explicitly mentioned; otherwise clean `"Meal"`).
2. **OpenRouter LLM Request**: Calls OpenRouter using `openrouter/auto` or user-selected free model with temperature 0.1 for deterministic JSON output.
3. **Resilient JSON Extraction**: Uses regex to slice from the first `{` to the last `}`. Strips markdown fences (` ```json `).
4. **Fallback Heuristic Extractor**: If the LLM produces invalid JSON or is unreachable, an in-memory regex parser extracts common units (e.g., `150g ground lamb`, `8oz chicken breast`, `2 slices bread`, `medium plate chicken`, `handful of grapes`).
5. **Nutrition & Staple Matching**:
   - Queries `UsdaFoodService.FindBestMatchAsync()`.
   - Strips filler words (*"plate of", "medium light smoothie from", "handful of"*).
   - Searches local staples first (chicken breast, 93/7 beef, eggs, rice, whey, lamb, bread, feta, hummus, olive oil, etc.).
   - If not found locally, calls USDA FoodData Central API.
   - If no USDA match, applies sensible estimations. **Never fall back to Chicken Breast for unrelated foods.**
6. **Clarification Chips Generator**:
   - Generates 1–2 actionable chips:
     - `Cooking Fat`: `[0-Cal Spray / None]`, `[+1 tbsp Olive Oil (+124 kcal)]`, `[+1 tbsp Butter (+102 kcal)]`.
     - `Meat State`: `[Cooked (Default)]`, `[Raw]`.
     - `Ground Meat Lean %`: `[93/7 Lean]`, `[85/15]`, `[80/20]`.

### B. Time Handling
- `MealEntry.Time` is an optional `string?` (e.g. `"13:30"` or `null`).
- HTML5 `<input type="time" />` outputs `"HH:mm"`.
- In `DiaryController.cs`, meals for a day are ordered by sequential ID: `.OrderBy(m => m.Id)`.
- In `MealConfirmCard.vue`, the time input is clearable and optional.
- In `MealCard.vue`, the time badge only renders if a non-empty `meal.time` exists.

### C. Daily Targets & Timeline
- Targets have an `EffectiveDate` (`DateOnly`).
- When fetching `/api/diary?date=YYYY-MM-DD`, EF queries `DailyTargets.Where(t => t.EffectiveDate <= queryDate).OrderByDescending(t => t.EffectiveDate).ThenByDescending(t => t.Id).FirstOrDefaultAsync()`.
- When updating targets via `TargetsController.UpdateTarget`, the client passes `EffectiveDate` matching the current diary date (`selectedDate`), avoiding UTC timezone drift.

---

## 5. Running & Testing

### Run Backend
```bash
dotnet run --project backend/Omnom.Api.csproj
```
Runs at `http://localhost:5000` (or `https://localhost:5001`).

### Run Frontend in Dev Mode (with Hot Reload)
```bash
cd frontend
npm install
npm run dev
```
Vite runs at `http://localhost:5173` and proxies `/api` to `http://localhost:5000`.

### Build Frontend for Production
```bash
cd frontend
npm run build
```
This compiles the SPA and writes production bundles directly into `backend/wwwroot/`. The .NET Web API serves this directory as static files and falls back to `index.html` for client-side routing.

### Run Automated Tests
```bash
dotnet test tests/Omnom.Tests
```
Ensure all tests pass before making or committing changes.

---

## 6. Guidelines for Future AI Agents

1. **Keep Tone Athletic & Focused**: Do not add cartoonish emojis or infantile copy. The target demographic is weightlifters and athletes tracking macros. Lighthearted and encouraging is good; silly or childish is not.
2. **Never Break Time-Freedom**: Keep meal time optional. Do not enforce chronological sorting by clock time or require timestamps.
3. **Preserve JSON Parsing Resilience**: OpenRouter free models occasionally output chat intros or imperfect JSON. Always maintain the robust substring extractor (`firstOpen` to `lastClose`) and the regex heuristic fallback.
4. **Always Build Frontend After Edits**: Whenever editing `.vue` or `.ts` files in `frontend/`, always run `npm run build` in `frontend/` so that `backend/wwwroot/` is kept in sync.
5. **Run Backend Tests**: Always run `dotnet test tests/Omnom.Tests` after backend edits.
