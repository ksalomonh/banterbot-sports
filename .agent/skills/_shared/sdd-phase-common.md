# SDD Phase — Common Protocol (BanterBot Sports)

## Project Context
- **Project**: banterbot-sports
- **Stack**: .NET 10 LTS + ASP.NET Core MVC + EF Core 10 + PostgreSQL + Telegram.Bot + Claude API
- **Engram project key**: `banterbot-sports`
- **Legacy reference**: `quinielas-legacy/` (.NET Core 2.0, same PostgreSQL schema)

## Git Workflow (ALWAYS APPLY)
- Sub-agents ALWAYS run with `isolation: "worktree"`
- Branch before any code: `type/description` format
- Every commit follows conventional commits

## Engram Upsert Note
Set `topic_key` on every `mem_save`. This enables upserts — saving again updates, not duplicates.

## Return Envelope
Every phase MUST return this structure to the orchestrator:

| Field | Description |
|-------|-------------|
| `status` | `success`, `partial`, or `blocked` |
| `executive_summary` | 1-3 sentence summary |
| `artifacts` | Engram keys written |
| `next_recommended` | Next SDD phase |
| `risks` | Risks found, or "None" |

Example:
```
**Status**: success
**Summary**: Spec created for `{change}`. N requirements, M scenarios.
**Artifacts**: Engram `sdd/{change}/spec`
**Next**: sdd-design or sdd-tasks
**Risks**: None
```

## Retrieving Engram Artifacts (MANDATORY — always 2 steps)
1. `mem_search(query: "sdd/{change}/spec", project: "banterbot-sports")` → get ID
2. `mem_get_observation(id: {id})` → full content

**Never use search preview as source material — it's truncated to 300 chars.**
**Run all mem_search calls in parallel, then all mem_get_observation calls in parallel.**
