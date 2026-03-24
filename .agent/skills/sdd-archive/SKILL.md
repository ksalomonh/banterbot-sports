---
name: sdd-archive
description: >
  BanterBot Sports wrapper for the global sdd-archive skill.
  Loads project context then delegates to the global skill.
---

## Setup — Load Before Starting
1. Read `.agent/skills/_shared/sdd-phase-common.md` — project context and common protocol
2. Read `~/.claude/skills/sdd-archive/SKILL.md` — full skill instructions
3. Apply ALL rules from both files

## Project: banterbot-sports
- Engram project key: `banterbot-sports`
- Stack: .NET 10 + ASP.NET Core + EF Core 10 + PostgreSQL
- Coding standards: `AGENTS.md` (enforced by GGA on every commit)
- Git: sub-agents run in worktrees, branch per feature
