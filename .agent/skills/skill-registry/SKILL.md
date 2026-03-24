---
name: skill-registry
description: >
  Index of all skills available in BanterBot Sports.
  Load this first to discover which skill to use for your task.
---

# BanterBot Sports — Skill Registry

## SDD Phases

| Skill | Trigger | Path |
|-------|---------|------|
| `sdd-explore` | Investigating a feature, clarifying requirements, exploring the codebase | `.agent/skills/sdd-explore/SKILL.md` |
| `sdd-spec` | Writing specifications with requirements and scenarios | `.agent/skills/sdd-spec/SKILL.md` |
| `sdd-design` | Technical design — architecture decisions, DB schema, API contracts | `.agent/skills/sdd-design/SKILL.md` |
| `sdd-tasks` | Breaking down a change into an implementation task checklist | `.agent/skills/sdd-tasks/SKILL.md` |
| `sdd-apply` | Implementing tasks, writing actual .NET code | `.agent/skills/sdd-apply/SKILL.md` |
| `sdd-verify` | Validating implementation matches specs and tasks | `.agent/skills/sdd-verify/SKILL.md` |
| `sdd-archive` | Archiving a completed change after implementation and verification | `.agent/skills/sdd-archive/SKILL.md` |

## Git & GitHub Workflow

| Skill | Trigger | Path |
|-------|---------|------|
| `issue-creation` | Creating a GitHub issue (bug report or feature request) | `.agent/skills/issue-creation/SKILL.md` |
| `branch-pr` | Creating a branch and opening a pull request | `.agent/skills/branch-pr/SKILL.md` |

## Shared Protocols

| File | Purpose |
|------|---------|
| `.agent/skills/_shared/sdd-phase-common.md` | Return envelope format + engram upsert rules |

## SDD Dependency Graph

```
proposal (PRD.md) → spec → tasks → apply → verify → archive
                     ↑
                   design
```

## Engram Topic Keys

| Artifact | Key |
|----------|-----|
| Project context | `sdd-init/banterbot-sports` |
| Exploration | `sdd/{change}/explore` |
| Spec | `sdd/{change}/spec` |
| Design | `sdd/{change}/design` |
| Tasks | `sdd/{change}/tasks` |
| Apply progress | `sdd/{change}/apply-progress` |
| Verify report | `sdd/{change}/verify-report` |
| Archive report | `sdd/{change}/archive-report` |
