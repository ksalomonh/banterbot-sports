# BanterBot Sports — Coding Standards

## Stack
- .NET 10 LTS
- ASP.NET Core MVC / Razor Pages
- Entity Framework Core 10 + PostgreSQL (Npgsql)
- ASP.NET Core Identity
- SignalR
- Claude API (Banter Engine)

## Architecture
Layered architecture inherited from legacy app:
- `*.Web` — Controllers, Views, wwwroot, SignalR hubs
- `*.BL` — Business Logic, Services, calculation engine
- `*.DAL` — EF Core DbContext, Repositories, Migrations
- `*.Entities` — Domain entities, ViewModels, DTOs
- `*.BanterAI` — Claude API integration, banter generation

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

## Commits
- Use conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`
- No AI attribution in commits
- Present tense, imperative mood: "add feature" not "added feature"

## Tests
- Unit tests for all scoring calculation logic
- Integration tests for prize distribution with tie scenarios
- Use xUnit + FluentAssertions
