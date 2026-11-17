# Weekly News Recap — Implementation Plan

> **Status:** Proposed / Ready for Development  
> **Scope:** `NewsApi`, `NigerianNewsGrid.Client`, `NigerianNewsGrid.Web`, `NigerianNewGrid` (.NET MAUI)  
> **Future Extension:** Scheduled AI Video & Audio Pipeline (`TtsWorker` / Media Worker)

---

## 1. Overview & Goals

Add a **News Recap** section to both the Website and Mobile App that delivers a **weekly recap** of prevalent stories across **Politics**, **Business**, **Entertainment**, **International**, and other active categories (**Technology**, **Sports**, **Crime**, **General**, etc.).

Key intelligence requirements:
1. **Story-Arc Continuity (Up to 2-Week Lookback)**: Trace stories from the current week back up to **14 days** to identify if a thread started earlier, capturing *How It Started → What Happened This Week → Where It Stands*.
2. **Prominent Personalities Analysis**: Identify people who came up prominently during the week, explaining *what happened* and *where the story went*.
3. **Future-Ready AI Video & Audio Pipeline**: Persist narration-ready scripts and media fields (`AudioUrl`, `VideoUrl`, `MediaStatus`) on each weekly edition so a scheduled AI pipeline can later synthesize audio and video without API or UI breaking changes.

---

## 2. Existing Codebase Building Blocks

| Layer | Existing Components to Leverage |
| :--- | :--- |
| **Backend (`src/NewsApi`)** | `StoryClusteringService` (`StoryCluster`, `SourceCount`, `ArticleCount`), `Topic` (`TopicType.Person`, `Organization`, `Place`, `Theme`) + `ArticleTopic`, `ArticleEmbedding`, `ArticleEnrichmentService` + `ArticleEnrichment` (`FullSummary`, `TimelineFacts`, `KeyFiguresAndEntities`), `AiAuditLogger`, `BriefingService`, `AudioController` |
| **Client (`src/NigerianNewsGrid.Client`)** | `NewsApiClient`, shared DTO records |
| **Website (`src/NigerianNewsGrid.Web`)** | React Router v7 SSR (`app/routes.js`, `app/routes/home.jsx`), `src/App.jsx`, `Navbar.jsx`, `MobileBottomNav.jsx`, `TrendingStoriesSection.jsx`, `CategoryTabs.jsx` |
| **Mobile (`NigerianNewGrid`)** | `AppShell.xaml`, `MainPage.xaml`, `DiscoverPage.xaml`, `StoryClusterPage.xaml`, `ArticleWebPage.xaml`, `AudioBriefingCoordinator`, `NewsPersistenceService` |
| **Workers (`src/TtsWorker`)** | `Service.cs` (scheduled WAT UTC+1 neural TTS synthesis & `/api/v1/audio/register` pattern) |

---

## 3. Core AI Recap Pipeline Design

### Stage 1: Retrieval & 14-Day Thread Matching
1. **Current Week Window (Days 0–7)**: Query `StoryCluster`s and deduplicated `Article`s grouped by category (`Politics`, `Business`, `Entertainment`, `International`, etc.). Rank by `SourceCount`, `ArticleCount`, `UserEvents` engagement, and recency.
2. **2-Week Lookback Window (Days 7–14)**: For each top story—especially those breaking early in the week—search up to **14 days back** using `StoryCluster` continuity, `ArticleTopic` overlap, and `ArticleEmbedding` cosine similarity to detect multi-week threads.

### Stage 2: Prominent Personalities Extraction
1. Aggregate `ArticleTopic` (`TopicType.Person`, plus key `Organization`s) and `ArticleEnrichment.FullSummary.KeyFiguresAndEntities` across the 7-day window.
2. Rank personalities by unique `StoryCluster` appearances, publisher diversity, and engagement.
3. Select the top **5–8 prominent personalities** per weekly edition.

### Stage 3: AI Narrative Synthesis (with Extractive Fallback)
1. Call the LLM (matching `ArticleEnrichmentService`, logged via `AiAuditLogger`) with bounded, source-attributed context per category and per top personality to generate strict JSON output:
   - **Editorial Intro** for the week.
   - **Per-Category Story Arcs** (3–5 stories per category): `RecapSummary`, `IsMultiWeekThread`, `ThreadStartedAtUtc`, `OriginContext`, `WeeklyDevelopment`, `CurrentOutcome`, `TimelineSteps`, and `ScriptSegment`.
   - **Personality Spotlights**: `RoleOrTitle`, `WhatHappened`, `WhereItWent`, and `ScriptSegment`.
   - **Full Narration Script (`ScriptText`)** for future audio/video generation.
2. **Fallback**: If the LLM is unavailable or quota-limited, deterministically synthesize from `StoryCluster.Summary` and `ArticleEnrichment` (`ExecutiveOverview`, `TimelineFacts`, `OutlookOrNextSteps`).

---

## 4. Phased Implementation Plan

### Phase 1: Backend Data Model, Migration & API (`src/NewsApi`)
1. **Entities** (`src/NewsApi/Models/` & `NewsDbContext.cs`):
   - `WeeklyRecap`: `Id`, `Slug` (unique, e.g. `2026-w47`), `Title`, `WeekStartUtc`, `WeekEndUtc`, `EditorialIntro`, `ScriptText`, `AudioUrl`, `AudioDurationSeconds`, `VideoUrl`, `ThumbnailUrl`, `MediaStatus` (`None` | `Queued` | `Processing` | `Ready` | `Failed`), `Status` (`Draft` | `Published`), `GeneratedAt`, `PublishedAt`.
   - `WeeklyRecapItem`: `Id`, `WeeklyRecapId` (FK), `Category`, `Rank`, `Headline`, `RecapSummary`, `IsMultiWeekThread`, `ThreadStartedAtUtc`, `OriginContext`, `WeeklyDevelopment`, `CurrentOutcome`, `TimelineStepsJson`, `ScriptSegment`, `SourceCount`, `ArticleCount`, `PrevalenceScore`, `StoryClusterId`, `StoryClusterSlug`, `LeadArticleId`, `LeadArticleSlug`, `ImageUrl`, `PublishedAt`.
   - `WeeklyRecapPersonality`: `Id`, `WeeklyRecapId` (FK), `TopicId`, `TopicSlug`, `Name`, `RoleOrTitle`, `PrimaryCategory`, `Rank`, `MentionCount`, `ClusterCount`, `WhatHappened`, `WhereItWent`, `ScriptSegment`, `ImageUrl`, `RelatedClusterIdsJson`.
   - Add EF Core migration for `WeeklyRecaps`, `WeeklyRecapItems`, and `WeeklyRecapPersonalities`.
2. **Services**:
   - `IRecapService` / `RecapService.cs`: Implements the 3-stage pipeline, cached queries (`GetLatestRecapAsync`, `GetRecapBySlugAsync`, `GetRecapEditionsAsync`), and `UpdateRecapMediaAsync`.
   - `RecapBackgroundService.cs`: Runs weekly on schedule (WAT) and ensures the current week's edition exists on startup.
3. **Controller** (`src/NewsApi/Controllers/RecapController.cs`):
   - `GET /api/v1/recap/latest` (supports `?category=Politics`, OutputCache + ETag).
   - `GET /api/v1/recap/editions` (paginated archive of past weeks).
   - `GET /api/v1/recap/{slug}` (specific week's edition).
   - `POST /api/v1/recap/generate` (admin/internal API-key protected manual trigger).
   - `PATCH /api/v1/recap/{id}/media` (internal API-key protected hook for future AI Audio/Video pipeline).

### Phase 2: Shared C# Client (`src/NigerianNewsGrid.Client`)
1. Add `src/NigerianNewsGrid.Client/Models/RecapModels.cs`:
   - `WeeklyRecapDto`, `RecapCategoryGroupDto`, `RecapItemDto`, `RecapPersonalityDto`, `RecapTimelineStepDto`, `RecapEditionSummaryDto`.
2. Add `GetLatestRecapAsync`, `GetRecapBySlugAsync`, and `GetRecapEditionsAsync` to `NewsApiClient.cs`.

### Phase 3: Website (`src/NigerianNewsGrid.Web`)
1. **API Client** (`src/services/api.js`): Add `getLatestRecap`, `getRecapBySlug`, `getRecapEditions`.
2. **Homepage Section** (`src/components/NewsRecapSection.jsx` + `app/routes/home.jsx` + `src/App.jsx`):
   - SSR-loaded `initialRecap` previewing top stories across **Politics**, **Business**, **Entertainment**, **International**, and **Other Categories**, plus a **"Personalities in the News"** strip and a `"Full Weekly Recap →"` CTA.
3. **Dedicated Recap Pages** (`app/routes/recap.jsx`, `app/routes/recap.$slug.jsx`, registered in `app/routes.js`):
   - Week selector (archive), category filter pills, Personality Spotlights (*What Happened & Where It Went*), and Story Arc cards (*2-Week Thread* badge + *How It Started → This Week → Where It Stands*).
   - Future-ready Media Player bar (renders automatically when `audioUrl` / `videoUrl` is populated).
   - SEO meta tags, `CollectionPage` JSON-LD, and `sitemap.xml` inclusion.
4. **Navigation**: Add `"News Recap"` to `Navbar.jsx` and `MobileBottomNav.jsx`.

### Phase 4: Mobile App (`NigerianNewGrid` - .NET MAUI)
1. **Recap Page & ViewModel** (`RecapPage.xaml`, `RecapPage.xaml.cs`, `ViewModels/RecapViewModel.cs`):
   - Register `"recap"` route in `AppShell.xaml.cs` and DI in `MauiProgram.cs`.
   - Week switcher, horizontal category filter chips, Personalities horizontal carousel, and Story Arc cards tapping through to `StoryClusterPage` or `ArticleWebPage`.
   - Future-ready Media Bar hooking into `AudioBriefingCoordinator` when `AudioUrl` is present.
2. **Entry Point**: Featured **"Weekly News Recap"** banner/card on `MainPage.xaml` and `DiscoverPage.xaml`.
3. **Offline Caching**: Persist latest `WeeklyRecapDto` in `NewsPersistenceService`.

### Phase 5: Verification & Tests
- **Backend Tests** (`tests/NewsApi.Tests/`): 14-day thread matching, personality ranking, AI synthesis + fallback, and `RecapController` endpoints.
- **Client Tests** (`tests/NewsApiClient.Tests/`): `NewsApiClient` deserialization for recap DTOs.
- **Build Checks**: `npm run build` (`NigerianNewsGrid.Web`) and `dotnet build` / `dotnet test` (`NewsApi`, `NigerianNewGrid`).

---

## 5. Future Phase: Scheduled AI Video & Audio Pipeline
1. Extend `TtsWorker` (or add a `RecapMediaWorker`) to poll `WeeklyRecap` where `MediaStatus == None`.
2. Synthesize weekly audio briefings from `ScriptText` / `ScriptSegment`s using the existing Tri-Tier Neural TTS (`en-NG-EzinneNeural` / `en-NG-Neural2-A`).
3. Render video recaps (headline cards + article images + captions + voiceover) via FFmpeg or AI video service.
4. Register generated assets via `PATCH /api/v1/recap/{id}/media` (`MediaStatus = Ready`).

---

## 6. Open Decisions to Confirm Before Implementation
- [ ] **Weekly Schedule**: Day and time (WAT) for auto-generation (e.g., Sunday 8:00 PM WAT vs. Monday 6:00 AM WAT).
- [ ] **Mobile Navigation**: Entry banner on `MainPage`/`DiscoverPage` opening `RecapPage`, or a dedicated tab in `AppShell.xaml`.
- [ ] **Edition Depth**: Confirm **3–5 stories per category** and **5–8 prominent personalities** per weekly edition.
