# BanterBot Sports — Project Context

## Project
Football pools web app with AI banter engine. Modernization of a system used since 2016.
Stack: .NET 10 LTS + ASP.NET Core MVC + EF Core 10 + PostgreSQL + Telegram Bot + Claude API.

## Key Docs
- `PRD.md` — full product requirements
- `AGENTS.md` — coding standards enforced by GGA on every commit
- `.agent/skills/` — SDD skill definitions for this project

## Skill Registry
Load `.agent/skills/skill-registry/SKILL.md` to discover all available skills and their paths.

## Git Workflow (MANDATORY)
- Every task → its own branch: `feat/`, `fix/`, `docs/`, `refactor/`, `chore/`
- **Sub-agents ALWAYS use `isolation: "worktree"`** — never execute directly on main
- Branch naming: `type/kebab-case-description` (e.g. `feat/telegram-bot-setup`)
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
