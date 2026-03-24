---
name: branch-pr
description: >
  Branch creation and PR workflow for BanterBot Sports.
  Trigger: When creating a pull request or preparing changes for review.
---

## Critical Rules
1. Every PR MUST link an approved GitHub issue (`status:approved` label)
2. Every PR MUST have exactly one `type:*` label
3. Sub-agents always work in worktrees — the orchestrator creates the PR from the worktree branch
4. GGA pre-commit hook runs automatically — fix any violations before pushing

## Branch Naming
```
^(feat|fix|chore|docs|refactor|perf|test|ci)\/[a-z0-9._-]+$
```

Examples:
- `feat/telegram-bot-setup`
- `feat/scoring-engine`
- `fix/deadline-timezone-bug`
- `refactor/migrate-netcore2-to-net10`
- `test/scoring-edge-cases`

## Workflow
```
1. gh issue list → confirm issue exists with status:approved
2. git checkout -b type/description main
3. Implement with conventional commits (GGA reviews each commit)
4. git push -u origin type/description
5. gh pr create with template
6. gh pr edit --add-label "type:feature" (or appropriate type)
```

## PR Body Template
```markdown
Closes #N

## PR Type
- [ ] type:bug
- [ ] type:feature
- [ ] type:docs
- [ ] type:refactor
- [ ] type:chore
- [ ] type:breaking-change

## Summary
-

## Changes
| File | Change |
|------|--------|
| `path/to/file` | What changed |

## Test Plan
- [ ] .NET build passes: `dotnet build`
- [ ] Tests pass: `dotnet test`
- [ ] GGA pre-commit passed on all commits
- [ ] Manually tested the affected functionality

## Notes for Reviewers

```

## Commands
```bash
# Create branch
git checkout -b feat/my-feature main

# Push and open PR
git push -u origin feat/my-feature
gh pr create --title "feat(scope): description" --body "Closes #N ..."

# Add type label
gh pr edit <number> --add-label "type:feature"
```

## Commit Format
```
type(scope): description

feat(telegram): add prediction parsing from voice messages
fix(scoring): correct tie-breaking logic for prize distribution
refactor(dal): migrate EF Core 2.0 entities to EF Core 10
test(bl): add edge cases for jornada deadline enforcement
```
