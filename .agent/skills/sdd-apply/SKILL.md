---
name: sdd-apply
description: >
  Implement tasks from the change, writing actual .NET 10 code following specs and design.
  Trigger: When the orchestrator launches you to implement one or more tasks.
---

## Purpose
You are a sub-agent responsible for IMPLEMENTATION. You write actual .NET code following the specs, design, and AGENTS.md coding standards. You run inside a Git worktree — your changes are isolated.

## Before Writing Any Code
1. Load `.agent/skills/_shared/sdd-phase-common.md`
2. Read `AGENTS.md` — all rules apply to every line you write
3. Read `CLAUDE.md` — architecture layers and business rules

## Retrieve All Artifacts (parallel — mandatory)
Run ALL searches in parallel, then ALL retrievals in parallel:

**Search (parallel):**
- `mem_search("sdd/{change}/spec", project: "banterbot-sports")`
- `mem_search("sdd/{change}/design", project: "banterbot-sports")`
- `mem_search("sdd/{change}/tasks", project: "banterbot-sports")`

**Retrieve full content (parallel):**
- `mem_get_observation(id)` for each

## Implementation Rules

### .NET Specific
- Every new project: enable `<Nullable>enable</Nullable>`
- All I/O: `async/await` — no blocking calls
- Guard clauses: `ArgumentNullException.ThrowIfNull()`
- No magic strings — use `const` or enums
- EF Core: never call `SaveChangesAsync()` from repositories
- External clients: register via `IHttpClientFactory`
- Config: always via `IConfiguration` — never hardcode secrets

### Architecture Placement
Before creating a file, ask: which layer does this belong to?
- Domain entity → `BanterBotSports.Entities/`
- DB access → `BanterBotSports.DAL/`
- Business logic → `BanterBotSports.BL/`
- Claude / banter → `BanterBotSports.BanterAI/`
- Telegram / API-Football / Whisper → `BanterBotSports.Integrations/`
- Controllers / Views / Hubs → `BanterBotSports.Web/`

### Legacy Reference
Check `quinielas-legacy/` for existing patterns before creating new ones. Prefer migration over reinvention.

## Git Workflow (MANDATORY)
You run in a worktree. Never push directly. The orchestrator handles PR creation.
- Mark completed tasks in engram with `mem_update`
- Save progress: `mem_save(topic_key: "sdd/{change}/apply-progress", project: "banterbot-sports")`

## Mark Tasks Complete
After implementing each task:
```
mem_update(id: {tasks-observation-id}, content: "{updated tasks with [x] marks}")
```

## Return Envelope
```
**Status**: success | partial | blocked
**Summary**: Implemented tasks X.X-X.X for `{change}`. Files created/modified: N.
**Artifacts**: Engram `sdd/{change}/apply-progress`
**Next**: sdd-verify
**Risks**: {any issues found}
```
