---
name: sai-git-branch-creator
description: >
  Creates git branches automatically from GitHub issues. Branch format: {type}/issue/{number}/{description}.
  Trigger: When user says "create branch from issue", "branch for issue", or provides an issue number.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "1.0"
allowed-tools: Read, Bash
---

## When to Use

- Creating a branch from existing GitHub issue
- Starting work on tracked issue

## Branch Format

```
{type}/issue/{number}/{kebab-description}
```

Examples:
- `feat/issue/35/login-development`
- `fix/issue/133/login-redirect-bug`
- `hotfix/issue/200/payment-gateway-error`
- `docs/issue/42/api-endpoint-guide`

## Type Mapping from Issue Labels

| Issue Label | Branch Type |
|-------------|-------------|
| `bug`, `bugfix`, `fix` | `fix` |
| `feature`, `enhancement` | `feat` |
| `hotfix`, `critical`, `urgent` | `hotfix` |
| `documentation`, `docs` | `docs` |
| `refactor`, `cleanup` | `refactor` |
| `test`, `testing` | `test` |
| `chore`, `ci`, `build` | `chore` |

**Default**: `feat` (if no matching label)

## Critical Patterns

- **ISSUE IS MANDATORY** → branch only from existing issue
- **NEVER ask user** → fail if issue not found
- **Format is strict**: `{type}/issue/{number}/{description}`
- **Push immediately**: `git push -u origin {branch}`
- **Use Bash tool** for all git operations

## Execution Flow

1. Extract issue number from context or user input
2. Verify issue exists: `gh issue view {number}`
3. Get issue title and labels
4. Determine type from labels (default: feat)
5. Generate branch name: `{type}/issue/{number}/{kebab-title}`
6. Create branch: `git checkout -b {branch}`
7. Push to remote: `git push -u origin {branch}`
8. Return result

## Commands

```bash
# Verify issue exists
gh issue view {number} --json title,labels,number

# Create branch
git checkout -b {type}/issue/{number}/{description}

# Push immediately
git push -u origin {type}/issue/{number}/{description}
```

## Output Format

```yaml
status: created | already-exists | failed
branch_name: "{type}/issue/{number}/{description}"
type: feat | fix | hotfix | docs | refactor | test | chore
issue_number: {number}
issue_title: "{original title}"
pushed: true | false
error: null | "Issue #{number} not found" | "Branch already exists" | ...
```

## Error Handling (Fail Fast)

| Condition | Status | Action |
|-----------|--------|--------|
| Issue not found | `failed` | Error: "Issue #{number} does not exist" |
| Branch exists | `failed` | Error: "Branch {name} already exists" |
| No git repo | `failed` | Error: "Not a git repository" |
| No remote | `failed` | Error: "No remote configured" |

**No fallbacks. No user prompts. Issue must exist.**
