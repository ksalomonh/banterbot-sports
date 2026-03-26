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
