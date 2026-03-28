# BanterBot Sports — PRD (Active Summary)

**Updated**: 2026-03-27
**Full PRD archived at**: `requirements/archive/PRD_2026-03-27_1746.md`

---

## Product

Football pools web app with AI banter engine. Modernization of a group quiniela system used since Eurocopa 2016 (Excel) → Russia 2018 (.NET Core 2.0) → now .NET 10 LTS.

**Stack**: .NET 10 + ASP.NET Core MVC + EF Core 10 + PostgreSQL + Telegram Bot + Claude API + Whisper + API-Football + SignalR + Tailwind CSS

**Roles**: Organizer (creates/manages tournaments, can also play) · Player (predicts, views rankings)

---

## What Is Built (Cycle History)

### Phase 1–6 — Initial Build (PRs #1–#13)

Full .NET 10 solution across 7 projects. All layers implemented from scratch.

| Module | Status |
|--------|--------|
| Auth (register / login / logout) | Working |
| Tournament creation + invite links (7-day expiry) | Working |
| Jornada lifecycle (create, open, close, deadline auto-assign) | Working |
| Prediction form (web) with countdown + status badges | Working |
| Scoring: result correct + exact score + total goals (all configurable) | Working |
| Prize distribution with tie-breaking (percentages, organizer-defined) | Working |
| SignalR real-time ranking on jornada close | Working |
| Leaderboard + post-matchday summary views | Working |
| Error handling (no stack traces in prod, UseExceptionHandler unconditional) | Working |

---

### cycle2-fixes — QA Cycle 1 (PR #20, 2026-03-26)

7 blocking gaps fixed after QA pass. Full SDD cycle: explore → archive. Engram archive: obs #113.

- Error handling hardened: TorneoController try/catch, no stack trace exposure
- `CalcularPuntosGolesJornadaAsync` implemented (was a stub)
- `DeadlineUtc` auto-assigned from earliest `KickOffUtc` when jornada opens
- `JornadaSinPartidosException` typed exception — jornada cannot open with 0 partidos
- BanterAI uses `NombreDisplay` instead of raw `UserId` GUIDs
- Confidence threshold fixed: 0.95 → **0.75** (per spec)
- Telegram match list notification on jornada open (`JornadaAbiertaNotifier`)

**Warnings carried forward**: `JornadaAbiertaNotifier` wired in controller instead of `Program.cs` composition root; BanterDispatchService fallback can still send raw `UserId` if lookup misses.

---

### cycle2-ux — Midnight Stadium Design System (merged 2026-03-27)

Full visual overhaul. Bootstrap 5 → Tailwind CSS. Engram archive: obs #130.

- All 13 views + `_Layout` rewritten — desktop + mobile in the same Razor view (mobile-first breakpoints)
- `DESIGN.md` is the single source of truth for all styling decisions
- Auth views (`Login`, `Register`): standalone `Layout = null`, hero panels
- Torneo views (`Index`, `Dashboard`, `Leaderboard`): full UX
- Jornada views (`Detalle`, `Resumen`, `AsistenteCalificacion`): full UX
- `Prediccion/Form`: countdown, match cards, dual-layout sync (mobile + desktop)
- `Home/Index`: branded landing + redirect for authenticated users
- `Account/Profile`: user profile page

---

### cycle3-images — Image Asset Integration (PR #21, 2026-03-27)

Static image layer on top of Midnight Stadium. Engram archive: latest obs.

- 5 assets in `wwwroot/images/`: `stadium-hero.png`, `logo-icon.png`, `avatar-default.png`, `tournament-banner-default.png`, `stadium-pitch-inset.png`
- `Partido` entity: `LogoUrlLocal` + `LogoUrlVisitante` nullable `string?` (migration `20260327195702` applied)
- `_TeamLogo.cshtml` reusable partial: renders team logo (`object-contain`) or `shield` icon fallback
- All views updated with `onerror` graceful degradation — no broken UI if files absent

---

## What Remains (Next Cycles)

### Blocking / High Priority

- **Telegram Bot**: `TelegramUpdateWorker` is a stub — no message parsing, no prediction ingestion
- **Prediction via Telegram**: text + audio (Whisper → Claude extraction at ≥0.75 confidence → user confirmation loop)
- **API-Football sync**: `ResultSyncService` incomplete — fetch + auto-sync every 5 min during active jornada
- **`Program.cs` composition root**: move `JornadaAbiertaNotifier` subscription from controller to startup
- **404 custom page**: currently returns empty body

### Medium Priority

- **Invite link + inline registration**: non-registered user receives link → creates account → auto-joins tournament
- **Tournament close screen**: final standings + prize distribution display
- **`Torneo/Historial.cshtml`**: history view (referenced in cycle3-images REQ-4 but not created)
- **Arena Chat**: SignalR peer-to-peer + BanterBot participation (glassmorphic, mobile FAB)
- **Banter Rail**: real-time read-only feed component, right panel

### Technical Debt

- `[Trait("Category","Unit")]` missing from all test classes — workaround: `--filter "FullyQualifiedName~Unit"`
- `_TeamLogo` fallback uses `shield` icon; original spec said initials pill — design decision to revisit
- Testcontainers `PostgreSqlBuilder()` obsolete constructor in 5 integration tests

---

## Critical Business Rules

- Points are **CONFIGURABLE** per tournament — never hardcode
- Prize distribution is **CONFIGURABLE** — percentages defined by organizer, must sum to 100%
- Prediction deadline = **first kick-off of the jornada** (per jornada, not tournament-wide)
- After deadline: **only organizer** can modify results
- Organizer **can also be a player** in their own tournament
- Banter messages: **max 280 characters**
- Claude confidence threshold: **0.75** (predictions below this ask user to reformulate)

---

## Architecture

```
BanterBotSports.Web/          → ASP.NET Core MVC, SignalR hubs, Razor views (Tailwind)
BanterBotSports.BL/           → Business logic, scoring engine, prize calculator, domain events
BanterBotSports.DAL/          → EF Core + PostgreSQL, Migrations
BanterBotSports.Entities/     → Domain entities (Spanish names), ViewModels, DTOs
BanterBotSports.BanterAI/     → Claude API: banter generation + prediction extraction
BanterBotSports.Integrations/ → API-Football, Telegram Bot, Whisper transcription
BanterBotSports.Tests/        → Unit + Integration (Testcontainers PostgreSQL)
```

**Key reference files:**
- `AGENTS.md` — coding standards enforced by GGA pre-commit hook
- `DESIGN.md` — Midnight Stadium design system (single source of truth for all styling)
- `requirements/archive/PRD_2026-03-27_1746.md` — full original PRD v2 with all flows, non-functionals, and UI specs

---

## SDD Artifact Index (Engram)

| Change | Engram Archive Obs |
|--------|--------------------|
| cycle2-fixes | #113 |
| cycle2-ux | #130 |
| cycle3-images | search `sdd/cycle3-images/archive-report` |
