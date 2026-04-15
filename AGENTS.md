# BanterBot Sports — Project Context

## Project
Football pools web app with AI banter engine. Modernization of a system used since 2016.
Stack: .NET 10 LTS + ASP.NET Core MVC + EF Core 10 + PostgreSQL + Telegram Bot + Claude API.

## Key Docs
- `PRD.md` — full product requirements
- `AGENTS.md` — coding standards enforced by GGA on every commit
- `DESIGN.md` — design coding standards enforced by GGA on every commit
- `.agent/skills/` — SDD skill definitions for this project

## Skill Registry
Load `.agent/skills/skill-registry/SKILL.md` to discover all available skills and their paths.

## Git Workflow (MANDATORY)
- Every task → its own branch: `feat/`, `fix/`, `docs/`, `refactor/`, `chore/`
- **Sub-agents ALWAYS use `isolation: "worktree"`** — never execute directly on main
- Branch naming:
  - Standard: `type/kebab-case-description` (e.g. `feat/telegram-bot-setup`)
  - Issue-linked: `type/issue/{number}/kebab-description` (e.g. `feat/issue/35/login-development`)
- Every PR must link an approved GitHub issue

## Architecture Layers
- `BanterBotSports.Web/` — ASP.NET Core MVC, SignalR hubs
- `BanterBotSports.BL/` — Business Logic, scoring engine, prize calculator
- `BanterBotSports.DAL/` — EF Core + PostgreSQL, Migrations
- `BanterBotSports.Entities/` — Domain entities (Spanish names), ViewModels, DTOs
- `BanterBotSports.BanterAI/` — Claude API: banter generation + prediction extraction
- `BanterBotSports.Integrations/` — API-Football, Telegram Bot, Whisper transcription

## Critical Business Rules
- Points are CONFIGURABLE per tournament — never hardcode
- Prize distribution is CONFIGURABLE — percentages defined by organizer
- Prediction deadline = first kick-off of the jornada (per jornada, not tournament)
- After deadline: only organizer can modify results
- Organizer can also be a player
- Banter messages max 280 characters

## Legacy Reference
Original app (.NET Core 2.0 + PostgreSQL): `/home/kevinsalomon/salomonai/customers/quinielas-legacy/`

# BanterBot Sports — Coding Standards

## Stack
- .NET 10 LTS
- ASP.NET Core MVC / Razor Pages
- Entity Framework Core 10 + PostgreSQL (Npgsql)
- ASP.NET Core Identity
- SignalR
- Claude API (claude-haiku-4-5-20251001) — Banter Engine + prediction extraction
- Telegram.Bot — Telegram Bot API SDK
- OpenAI Whisper API — voice message transcription
- API-Football — match catalog and live results

## Architecture
Layered architecture inherited from legacy app:
- `*.Web` — Controllers, Views, wwwroot, SignalR hubs
- `*.BL` — Business Logic, Services, calculation engine
- `*.DAL` — EF Core DbContext, Repositories, Migrations
- `*.Entities` — Domain entities, ViewModels, DTOs
- `*.BanterAI` — Claude API integration, banter generation, prediction extraction from free text
- `*.Integrations` — External service clients: API-Football, Telegram Bot (Telegram.Bot), Whisper transcription

## C# Conventions
- Use `async/await` for all I/O operations — no sync-over-async
- Use `record` types for DTOs and ViewModels where applicable
- Prefer `IReadOnlyList<T>` over `List<T>` for return types in services
- Null safety: enable `<Nullable>enable</Nullable>` in all projects
- Use `ArgumentNullException.ThrowIfNull()` for guard clauses
- No magic strings — use constants or enums for status values
- Entity names in Spanish (match legacy): `Torneo`, `Jornada`, `Partido`, `Participante`

## EF Core
- Never call `SaveChangesAsync()` from repositories — call it from services/unit of work
- Use explicit loading over lazy loading
- All migrations must be reversible (`Down()` method required)
- Never hardcode connection strings — use `IConfiguration`

## Business Rules to Enforce
- Points are CONFIGURABLE — never hardcode point values
- Prize distribution is CONFIGURABLE — never assume "winner takes all"
- Prediction deadlines are per-jornada (first match kick-off), not per-tournament
- Only the organizer can enter official results after lock
- Organizer CAN also be a player

## Banter Engine
- Never call Claude API synchronously
- Always validate AI output before storing or displaying
- System prompt must include guardrails: friendly tone, no offensive content
- Keep banter messages under 280 characters

## Integrations Layer Rules
- API-Football responses MUST be cached in PostgreSQL — never hit API on every request
- Telegram webhook handler must respond within 5 seconds (Telegram timeout) — offload heavy work to background jobs
- Voice messages: always delete temp audio file after transcription
- Whisper transcription result must pass through Claude for prediction extraction — never parse raw transcription directly
- All external HTTP clients registered via `IHttpClientFactory` with named clients
- Never expose API keys in code — always via `IConfiguration` / environment variables

## Commits
- Use conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`
- No AI attribution in commits
- Present tense, imperative mood: "add feature" not "added feature"

## Tests
- Unit tests for all scoring calculation logic
- Integration tests for prize distribution with tie scenarios
- Use xUnit + FluentAssertions

## SDD Skills Registry

| Skill | Description | Path |
|-------|-------------|------|
| `sdd-certifier` | QA Certification coordinator — receives app-type from orchestrator, loads the matching cert-* skill, certifies live behavior against Spec scenarios | [SKILL.md](~/.claude/skills/sdd-certifier/SKILL.md) |
| `cert-web` | Web app testing toolbox — Playwright headless browser, form interaction, screenshots | [SKILL.md](~/.claude/skills/cert-web/SKILL.md) |
| `cert-mobile` | Mobile app testing toolbox — Appium + UIAutomator2/XCUITest, emulator, element interaction | [SKILL.md](~/.claude/skills/cert-mobile/SKILL.md) |
| `cert-service` | Service/API testing toolbox — curl, httpie, grpcurl, response validation | [SKILL.md](~/.claude/skills/cert-service/SKILL.md) |
| `cert-console` | Console/CLI testing toolbox — stdin/stdout/exit codes, workers, log validation | [SKILL.md](~/.claude/skills/cert-console/SKILL.md) |
| `ss-continue` | Session-safe continue — audits git worktrees + engram after a session cut, reconciles both sources, identifies continuation point without duplicating work | [SKILL.md](~/.claude/skills/ss-continue/SKILL.md) |
