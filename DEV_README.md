# Omnom Developer Guide & Codebase Architecture (DEV_README.md)

Welcome to the **Omnom Developer Guide**! This guide is crafted to teach you the architecture, technical decisions, data models, workflows, and frontend/UI philosophy behind Omnom. Whether you are a junior developer, a new contributor, or an experienced engineer onboarding onto the project, this document will walk you through every layer of the system.

---

## Table of Contents

1. [Executive Summary & Philosophy](#1-executive-summary--philosophy)
2. [High-Level Architecture & Data Flow](#2-high-level-architecture--data-flow)
3. [Backend Deep-Dive (.NET 8 Web API)](#3-backend-deep-dive-net-8-web-api)
   - [Bootstrap & Dependency Injection (`Program.cs`)](#bootstrap--dependency-injection-programcs)
   - [Database & Entities (`backend/Data/`)](#database--entities-backenddata)
   - [Core Services & Parsing Pipeline (`backend/Services/`)](#core-services--parsing-pipeline-backendservices)
   - [API Controllers & DTOs (`backend/Controllers/` & `Models/`)](#api-controllers--dtos-backendcontrollers--models)
4. [Frontend Deep-Dive (Vue 3, TypeScript, Pinia)](#4-frontend-deep-dive-vue-3-typescript-pinia)
   - [Single Page Application Setup (`main.ts`, `App.vue`)](#single-page-application-setup-maints-appvue)
   - [Pinia Reactive Stores (`frontend/src/stores/`)](#pinia-reactive-stores-frontendsrcstores)
   - [Component Hierarchy & Responsibilities (`frontend/src/components/`)](#component-hierarchy--responsibilities-frontendsrccomponents)
   - [API Client Layer (`frontend/src/services/api.ts`)](#api-client-layer-frontendsrcservicesapits)
5. [UI, UX & Design System Choices](#5-ui-ux--design-system-choices)
   - [The "Apex Athletic + Obsidian Minimalist" Aesthetic](#the-apex-athletic--obsidian-minimalist-aesthetic)
   - [Macro Math & Real-Time Telemetry](#macro-math--real-time-telemetry)
   - [Design Anti-Patterns We Deliberately Avoid](#design-anti-patterns-we-deliberately-avoid)
6. [Testing & Quality Assurance](#6-testing--quality-assurance)
7. [Developer Workflow & Deployment](#7-developer-workflow--deployment)
8. [Junior Dev Golden Rules & Common Pitfalls](#8-junior-dev-golden-rules--common-pitfalls)

---

## 1. Executive Summary & Philosophy

### What is Omnom?
**Omnom** is an AI-assisted fitness and nutrition tracking web application tailored specifically for **weightlifters and athletes**. 

Most traditional nutrition apps (e.g., MyFitnessPal, Cronometer) suffer from extreme tracking friction:
- Forcing users to search through crowdsourced databases filled with duplicate or inaccurate entries.
- Requiring dozens of manual screen taps per meal to input grams and serving sizes.
- Mandating exact clock times and meal slots ("Breakfast", "Lunch", "Dinner", "Snack").
- Cluttering the interface with cartoon emojis, aggressive paywalls, or multi-turn conversational chat interfaces that slow down logging.

Omnom solves this by combining **zero-friction natural language meal parsing** with an **uncluttered athletic telemetry dashboard**.

### Core Tenets to Memorize

| Tenet | What It Means | Why It Matters for Athletes |
| :--- | :--- | :--- |
| **1. Frictionless Natural Language** | Users type or voice-dictate: *"8oz flank steak, 200g jasmine rice, 1 tbsp olive oil"*. | Lifters eat repetitive, staple meals. Logging must take under 5 seconds. |
| **2. Time is Optional** | Meals do not require clock times. Omnom never auto-labels a meal based on system clock time. | Lifters often eat on irregular schedules or log their entire day at night before sleep. |
| **3. Grounded Nutritional Accuracy** | Nutritional data matches USDA FoodData Central and pre-seeded local staple tables. | Macro accuracy matters for muscle protein synthesis, cutting, and bulking. Guesswork ruins physique goals. |
| **4. Non-Blocking Clarification Chips** | Instead of asking multi-turn chat questions (*"Did you cook it with oil?"*), the UI surfaces 1-tap chips (`[Cooked (Default)] [Raw]`, `[+1 tbsp Olive Oil]`). | Chatbots are conversational and slow. 1-tap chips provide instant, optional calibration without interrupting flow. |
| **5. Athletic Tone & Obsidian Aesthetic** | Crisp, dark obsidian interface with high-visibility neon accents (Electric Volt, Protein Coral, Carb Cyan, Fat Amber). **No cartoon emoji clutter.** | Athletes want clean telemetry, not a childish toy app. |
| **6. Immediate Visual Feedback** | Every action (saving meals, adjusting targets, deleting items) responds with live loading states and feedback. | Confidence that data is saved and persisted reliably. |

---

## 2. High-Level Architecture & Data Flow

Omnom uses a **Single-Service Architecture**. The Vue 3 SPA compiles directly into the .NET 8 Web API's static files directory (`backend/wwwroot/`). This allows the entire application—frontend, backend, database, and background seeding—to run as a single process or container without microservice overhead.

```
+-------------------------------------------------------------------------------+
|                            CLIENT BROWSER (VUE 3)                             |
|                                                                               |
|  [Voice / Text Input] ---> [Command-Dock Logger] ---> [1-Tap Confirmation]    |
|           ^                                                    |              |
|           |                                                    v              |
|  [Daily Telemetry HUD] <--- [Pinia Stores] <--- [Axios HTTP Client (/api)]    |
+-------------------------------------------------------------------------------+
                                        |  HTTP Requests
                                        v
+-------------------------------------------------------------------------------+
|                        BACKEND (.NET 8 WEB API HOST)                          |
|                                                                               |
|  [Kestrel Server] ---> [Program.cs (Routing, DI, CORS, Static Files Fallback)]|
|                                       |                                       |
|          +----------------------------+-----------------------------+         |
|          |                            |                             |         |
|   [AiController]              [DiaryController]             [TargetsController]
|          |                            |                             |         |
|  [MealParserService]          [EF Core SQLite]              [EF Core SQLite]  |
|   /              \                    |                             |         |
| [OpenRouter API] [UsdaFoodService]    v                             v         |
| (Free Tier LLMs)   /         \   [MealEntry / Items]         [DailyTarget]    |
|                   v           v                                               |
|             [Local Staples] [USDA API]                                        |
|             (SQLite Cached) (FoodData Central)                                |
+-------------------------------------------------------------------------------+
                                        |
                                        v
                          [SQLite File: omnom.db]
```

### The End-to-End Fuel Logging Pipeline

1. **User Input**: The lifter enters text or speaks: *"200g chicken breast and 150g rice"*.
2. **AI Request (`POST /api/ai/parse`)**:
   - `MealParserService` crafts a deterministic system prompt instructing an LLM on OpenRouter to return a structured JSON extraction.
   - If OpenRouter is unavailable or rate-limited, an internal deterministic regex parser extracts numbers, units, and food terms as a fallback.
3. **Staple & USDA Cross-Referencing**:
   - Each extracted food item is queried through `UsdaFoodService`.
   - Omnom checks its pre-seeded SQLite staples database first (sub-millisecond latency).
   - If not found locally, it queries the USDA FoodData Central REST API.
   - Gram weights are normalized ($1\text{ oz} \approx 28.35\text{g}$, $1\text{ cup rice} \approx 158\text{g}$).
4. **Clarification Generation**:
   - The engine generates non-blocking chips (`[Cooked (Default)] [Raw]`, `[0-Cal Spray]` vs `[+1 tbsp Olive Oil]`).
5. **Interactive Review**:
   - Vue receives the parsed meal and mounts `MealConfirmCard.vue`.
   - The user can adjust grams with `+10g`/`+25g` steppers, tap chips, or rename items.
6. **Commit to Diary (`POST /api/diary`)**:
   - The meal is persisted to SQLite.
   - Cumulative running macros for the day are recalculated.
   - Pinia updates the reactive timeline and daily telemetry rings instantly.

---

## 3. Backend Deep-Dive (.NET 8 Web API)

The backend is built in C# targeting .NET 8, organized under `backend/`.

### Bootstrap & Dependency Injection (`Program.cs`)

Located at [`backend/Program.cs`](file:///Users/victoryma/Repositories/omnom/backend/Program.cs).

```csharp
// Excerpt from Program.cs
var builder = WebApplication.CreateBuilder(args);

// 1. Storage Location Support (Local vs Containerized Volume)
var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
var dbPath = !string.IsNullOrWhiteSpace(dataDir)
    ? Path.Combine(dataDir, "omnom.db")
    : "omnom.db";

// 2. Entity Framework Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// 3. Typed HTTP Clients & Scoped Services
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
builder.Services.AddHttpClient<IUsdaFoodService, UsdaFoodService>();
builder.Services.AddScoped<IMealParserService, MealParserService>();
```

#### Key Concepts to Understand Here:
* **`DATA_DIR` Environment Variable**: When deployed via Docker or cloud hosting (Render/Fly.io), SQLite needs to write to a persistent mounted volume (e.g. `/data/omnom.db`). If `DATA_DIR` is set, the app creates that folder and stores the DB there. Otherwise, it defaults to the local working directory.
* **`AddHttpClient<TInterface, TImplementation>`**: Uses .NET's `IHttpClientFactory` pattern. This manages the socket lifecycle properly and avoids DNS exhaustion bugs common with manual `new HttpClient()` instantiations.
* **SPA Fallback Routing (`MapFallbackToFile("index.html")`)**: This is crucial. Because Vue Router or client-side navigation handles page state, any URL not matching a registered `/api/*` controller falls back to serving `wwwroot/index.html`.

---

### Database & Entities (`backend/Data/`)

The database layer uses **Entity Framework Core (EF Core)** with SQLite.

#### Entities Breakdown

1. **[`MealEntry.cs`](file:///Users/victoryma/Repositories/omnom/backend/Data/Entities/MealEntry.cs)**:
   - Represents a logged meal on a given `DateOnly Date`.
   - `Time` is an optional `string?` (`"13:30"` or `null`).
   - Stores pre-computed totals (`TotalCalories`, `TotalProtein`, `TotalCarbs`, `TotalFat`) so daily summary queries don't need expensive aggregations on every request.
2. **[`MealItem.cs`](file:///Users/victoryma/Repositories/omnom/backend/Data/Entities/MealItem.cs)**:
   - Represents an individual food item within a meal (e.g. "Chicken Breast", 200g).
   - Linked to `MealEntry` via a foreign key with `CascadeDelete`.
3. **[`DailyTarget.cs`](file:///Users/victoryma/Repositories/omnom/backend/Data/Entities/DailyTarget.cs)**:
   - Represents calorie and macronutrient targets.
   - Has an `EffectiveDate` (`DateOnly`).
4. **[`FoodReference.cs`](file:///Users/victoryma/Repositories/omnom/backend/Data/Entities/FoodReference.cs)**:
   - Represents a cached food item (either from USDA or local staple).
   - Indexed by `NormalizedQuery` and `FdcId` for fast search matching.

#### The "Effective Date" Temporal Inheritance Pattern

Pay close attention to how `DailyTargets` are resolved in [`DiaryController.cs`](file:///Users/victoryma/Repositories/omnom/backend/Controllers/DiaryController.cs):

```csharp
var targetEntity = await _dbContext.DailyTargets
    .Where(t => t.EffectiveDate <= queryDate)
    .OrderByDescending(t => t.EffectiveDate)
    .ThenByDescending(t => t.Id)
    .FirstOrDefaultAsync();
```

**Why do we do this?**
Users don't want to re-enter their target macros every single day. If a lifter begins a "Lean Bulk (2,800 kcal)" on September 1st, that target should automatically apply to September 2nd, 3rd, and onwards—until they change it to "Cutting" on October 1st.
By querying `EffectiveDate <= queryDate` ordered descending, the system provides **temporal target inheritance** with zero database bloat.

---

### Core Services & Parsing Pipeline (`backend/Services/`)

#### 1. [`MealParserService.cs`](file:///Users/victoryma/Repositories/omnom/backend/Services/MealParserService.cs)
This is the core intelligence of Omnom. It takes a raw string and outputs structured nutrition.

* **LLM Extraction**: Sends a strict system prompt to OpenRouter asking for JSON:
  ```json
  {
    "suggestedMealName": "Lunch",
    "items": [
      { "foodName": "chicken breast", "quantity": 8, "unit": "oz", "estimatedGrams": 226 }
    ]
  }
  ```
* **Resilient JSON Parser (`ParseLlmJson`)**:
  Free-tier LLMs frequently include introductory chatter like *"Sure, here is your JSON:"* or wrap their answer in markdown fences.
  To prevent parsing crashes, Omnom slices strictly from the first `{` to the last `}` using `IndexOf('{')` and `LastIndexOf('}')`.
* **Heuristic Fallback Engine (`FallbackHeuristicParse`)**:
  If the LLM is down, network drops, or the user hasn't configured an API key, the app does **not** fail. An internal regex heuristic extracts units (`g`, `oz`, `lbs`, `cup`, `tbsp`, `scoop`, `slice`) and maps items directly against the local staple database.
* **Clarification Chip Generator**:
  Inspects the parsed items. If chicken or meat is present, it generates a `[Cooked (Default)] [Raw]` chip. If meat is cooked in oil, it offers `[0-Cal Spray]` vs `[+1 tbsp Olive Oil]`.

#### 2. [`UsdaFoodService.cs`](file:///Users/victoryma/Repositories/omnom/backend/Services/UsdaFoodService.cs)
Integrates with USDA FoodData Central and maintains the local database.

* **Hierarchy of Truth**:
  1. Check `FoodReferences` where `IsStaple == true`. Matches staples like *"chicken breast"*, *"93/7 ground beef"*, *"jasmine rice"*, *"whey protein isolate"*.
  2. If unmatched, query USDA FoodData Central REST API (`/fdc/v1/foods/search`).
  3. If USDA API is unreachable, apply sensible baseline macro estimations.
  4. **Golden Rule**: Unmatched foods must *never* arbitrarily fallback to unrelated defaults (e.g. don't log a cookie as chicken breast).

---

## 4. Frontend Deep-Dive (Vue 3, TypeScript, Pinia)

The frontend is located in `frontend/` and built with **Vue 3 (Composition API)**, **TypeScript**, **Tailwind CSS**, and **Pinia**.

### Project Structure & Component Tree

```
frontend/src/
├── App.vue                        # App Shell, Toast container, Top Nav, Modals
├── main.ts                        # Vue app mount & Pinia installation
├── style.css                      # Tailwind base, dark obsidian gradients, tactile buttons
├── services/
│   └── api.ts                     # Axios client, TypeScript interfaces, API endpoints
├── stores/
│   ├── diaryStore.ts              # Current date, timeline data, meal CRUD, target mutation
│   ├── settingsStore.ts           # API keys, free model selection
│   ├── authStore.ts               # Passcode/PIN verification & token storage
│   └── toastStore.ts              # Global banner notification system
└── components/
    ├── DailySummaryHeader.vue     # Calorie circular ring, 3 macro cards, caloric split bar
    ├── QuickLogInput.vue          # Command-dock text/voice logger + fast staple chips
    ├── MealConfirmCard.vue        # AI review tray, +/- gram steppers, clarification chips
    ├── MealCard.vue               # Timeline meal card with running cumulative totals
    ├── TimelineFeed.vue           # Chronological feed container & empty state
    ├── DateNavigator.vue          # [Prev] [Date Picker] [Today] [Next] controls
    ├── QuickTargetModal.vue       # Preset switcher (Bulk, Cut, Maintenance, Refeed)
    ├── SettingsModal.vue          # OpenRouter/USDA API key configuration
    └── PasscodeModal.vue          # Master PIN lock screen
```

---

### Pinia Reactive Stores (`frontend/src/stores/`)

#### [`diaryStore.ts`](file:///Users/victoryma/Repositories/omnom/frontend/src/stores/diaryStore.ts)
The heart of the client-side state.
* `selectedDate`: ISO string (`YYYY-MM-DD`).
* `timeline`: Reactive `DayTimeline` object holding daily targets, consumed calories/macros, remaining metrics, and the list of meals.
* `shiftDate(days: number)`: Increments or decrements the date by $N$ days and triggers `fetchTimeline()`.
* `setDateToToday()`: Resets `selectedDate` to the current local date.
* `addMeal()`, `deleteMeal()`, `updateTarget()`: Mutates backend data and refreshes the timeline.

#### Date Handling & Timezone Safety
Notice how `diaryStore` calculates today's date:
```typescript
const getTodayString = () => {
  const d = new Date();
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};
```
**Why do this instead of `new Date().toISOString().split('T')[0]`?**
`toISOString()` converts the timestamp to UTC! If a lifter in California logs food at 9:00 PM PST, UTC is already the next day (5:00 AM UTC). Using UTC causes "timezone drift," logging food onto tomorrow's timeline. Omnom explicitly formats based on the client's local year, month, and day.

---

### Component Hierarchy & Responsibilities

#### 1. [`DailySummaryHeader.vue`](file:///Users/victoryma/Repositories/omnom/frontend/src/components/DailySummaryHeader.vue)
Renders the top athletic telemetry dashboard:
* **Circular Progress Ring**: SVG circle with stroke-dashoffset bound to `consumedCal / targetCal`.
* **Macro Delta Readouts**: Calculates `remaining = target - consumed`. Displays `-25g left` or `✓ Met`.
* **Caloric Macro Split Bar**: Computes energy ratios using standard macronutrient caloric densities:
  $$\text{Energy}_{\text{Protein}} = \text{grams} \times 4\text{ kcal/g}$$
  $$\text{Energy}_{\text{Carbs}} = \text{grams} \times 4\text{ kcal/g}$$
  $$\text{Energy}_{\text{Fat}} = \text{grams} \times 9\text{ kcal/g}$$
  Renders a tri-color segmented progress bar showing the percentage split (e.g. `38% Protein • 44% Carbs • 18% Fat`).

#### 2. [`QuickLogInput.vue`](file:///Users/victoryma/Repositories/omnom/frontend/src/components/QuickLogInput.vue)
A command-dock input interface:
* **1-Tap Fast Staples Tray**: Buttons for frequent foods (`+ 30g Whey`, `+ 4 Whole Eggs`, `+ 200g Chicken`, `+ 150g Rice`). Tapping a staple automatically appends or populates the input.
* **Voice-to-Text**: Wraps the browser's native Web Speech API (`SpeechRecognition` / `webkitSpeechRecognition`).
* **Keyboard Ergonomics**: Pressing `Enter` (without Shift) triggers analysis.

#### 3. [`MealConfirmCard.vue`](file:///Users/victoryma/Repositories/omnom/frontend/src/components/MealConfirmCard.vue)
Mounts as soon as the backend returns an `AiParsedMealResult`.
* **Live In-Memory Calibration**: When a user changes grams (via `-25`, `-10`, `+10`, `+25` buttons), the card recalculates macros dynamically in Vue without making extra server roundtrips.
* **Clarification Chips**: Clicking `[+1 tbsp Olive Oil]` immediately pushes a cooking oil item to the list with 14g fat and 124 kcal.

#### 4. [`MealCard.vue`](file:///Users/victoryma/Repositories/omnom/frontend/src/components/MealCard.vue)
Renders each meal in the chronological timeline:
* **Running Daily Cumulative Totals**: Displays the total nutrition consumed up to and including this meal (`Day Cumulative: 1,450 kcal | 112g P | 140g C | 38g F`).
* **Optional Time Badge**: If `meal.time` exists (`"12:30"`), it formats to `"12:30 PM"`. If null, no empty badges are shown.
* **Expandable Ingredient Breakdown**: Shows itemized rows with `#1`, `#2`, gram badges, and macronutrient breakdowns.

---

## 5. UI, UX & Design System Choices

### The "Apex Athletic + Obsidian Minimalist" Aesthetic

Omnom's interface is designed specifically for strength athletes. The design tokens in [`tailwind.config.js`](file:///Users/victoryma/Repositories/omnom/frontend/tailwind.config.js) reflect this:

```javascript
colors: {
  omnom: {
    dark: '#08090d',         // Deep obsidian background
    card: '#0f1118',         // Obsidian slate card surface
    border: 'rgba(255, 255, 255, 0.08)',
    volt: '#d4ff00',         // Electric Volt / Chartreuse (energy & primary action)
    coral: '#ff4d6d',        // High-vis Protein Coral (recovery & amino acids)
    cyan: '#38bdf8',         // Sky Cyan (glycogen & carbohydrates)
    amber: '#f59e0b',        // Warm Amber (hormonal health & dietary fats)
    emerald: '#10b981',      // Success / Target Met badges
  }
}
```

#### Why These Colors?
* **Volt (`#d4ff00`)**: Creates high visual contrast against dark obsidian, commonly seen in athletic performance gear (Nike Volt, Garmin).
* **Coral (`#ff4d6d`) for Protein**: Visually represents lean meat/protein, distinct and readable.
* **Cyan (`#38bdf8`) for Carbs**: Evokes clean energy and hydration.
* **Amber (`#f59e0b`) for Fat**: Evokes healthy oils and fats.

### Design Anti-Patterns We Deliberately Avoid

1. **Kid-Like Emoji Clutter**: Avoid putting cartoons (`🤤`, `🍪`, `🦖`, `🥣`) on every card. Athletic tools should look like high-precision instruments.
2. **Chatbot Conversations**: Do not build a conversational chatbot that asks *"What did you have for lunch?"* followed by *"Did you cook that with butter?"*. Lifters want to enter their food, check the chips, and confirm in under 5 seconds.
3. **Clock Time Enforcement**: Never reject a meal because it lacks a time. Never auto-label a 11:30 PM meal as "Late Night Snack" if the lifter considers it dinner.

---

## 6. Testing & Quality Assurance

### Backend Automated Tests (`tests/Omnom.Tests`)

Backend tests use **xUnit** and **Microsoft.EntityFrameworkCore.InMemory**.

Run tests via the CLI:
```bash
dotnet test tests/Omnom.Tests
```

#### What the Tests Cover:
* **MealParserServiceTests**:
  - Validates substring JSON extraction.
  - Validates heuristic regex fallback when LLM fails.
  - Validates clarification chip generation logic.
* **UsdaFoodServiceTests**:
  - Validates staple priority lookup over remote API.
  - Validates gram weight normalization.
* **DiaryControllerTests**:
  - Validates meal creation, deletion, and cascading delete of items.
  - Validates cumulative running macro calculations.
  - Validates `EffectiveDate` target inheritance.

---

## 7. Developer Workflow & Deployment

### Local Development Setup

#### 1. Start Backend API
```bash
cd backend
dotnet run
```
Runs at `http://localhost:5000`. Auto-creates `omnom.db` and seeds staples.

#### 2. Start Frontend Dev Server
```bash
cd frontend
npm install
npm run dev
```
Runs Vite at `http://localhost:5173`. Vite proxies `/api` requests to `http://localhost:5000`.

### Production Build & Single-Container Distribution

To build for production:
```bash
cd frontend
npm run build
```
Vite compiles the application and outputs bundles directly into [`backend/wwwroot/`](file:///Users/victoryma/Repositories/omnom/backend/wwwroot/).

When the .NET API starts in production (`dotnet run --project backend/Omnom.Api.csproj`), Kestrel serves the compiled Vue SPA as static files.

#### Multi-Stage Dockerfile (`Dockerfile`)
```dockerfile
# Stage 1: Build Vue SPA
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# Stage 2: Build .NET Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY backend/ ./backend/
COPY --from=frontend-build /app/backend/wwwroot ./backend/wwwroot
RUN dotnet publish backend/Omnom.Api.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=backend-build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Omnom.Api.dll"]
```

---

## 8. Junior Dev Golden Rules & Common Pitfalls

If you take away nothing else from this document, remember these rules:

1. **Always Rebuild Frontend After Edits**:
   Because the backend serves from `backend/wwwroot/`, whenever you edit Vue files or Tailwind styles, run:
   ```bash
   cd frontend && npm run build
   ```
2. **Never Break Time-Freedom**:
   `MealEntry.Time` must remain optional (`string?`). Never require timestamps or sort exclusively by clock time.
3. **Preserve JSON Parsing Resilience**:
   Free-tier models will occasionally output preamble text or unescaped characters. Always maintain the substring extractor (`IndexOf('{')` to `LastIndexOf('}')`) and the heuristic regex fallback in [`MealParserService.cs`](file:///Users/victoryma/Repositories/omnom/backend/Services/MealParserService.cs).
4. **Never Default to Chicken Breast for Random Foods**:
   If a food cannot be matched against the USDA database, use sensible macronutrient estimations. Never default an unmatched item to a staple meat.
5. **Beware UTC Date Drift**:
   Always handle diary dates as `DateOnly` strings (`YYYY-MM-DD`) derived from the user's local device clock, not UTC timestamps.
6. **Run Tests Before Committing**:
   ```bash
   dotnet test tests/Omnom.Tests
   ```
   Ensure all tests pass before submitting changes.

---

*Happy hacking! Fuel your training, hit your macros, and keep Omnom fast, accurate, and athletic.* 🏋️‍♂️⚡
