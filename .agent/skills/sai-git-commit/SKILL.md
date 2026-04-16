---
name: sai-git-commit
description: >
  Automates git workflow: stage files, review changes, generate conventional commit message, commit, and push.
  Trigger: When user asks to commit, stage files, or push changes.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "1.0"
  agent-linked: true
allowed-tools: bash, read, write
---

## SDD Execution Mode

This skill runs as a **visible sub-agent** — user sees real-time output.

## When to Use

- User says "commit it", "stage and commit", "push changes"
- Multiple files need staging and atomic commit
- Conventional commit format needed (feat:, fix:, refactor:, etc.)
- Changes need to be pushed to remote branch

## Critical Patterns

### 1. Always Review Before Commit
- Use `git diff --cached` to review staged changes
- Analyze file changes to understand what changed
- Generate appropriate conventional commit type based on changes

### 2. Atomic Commits
- Stage only files mentioned/related to the change
- Don't mix unrelated changes in one commit
- If user didn't specify files, ask orchestrator which ones to stage

### 3. Conventional Commits
Format: `type(scope): description`
Types: feat, fix, refactor, test, docs, chore, style, perf
Use present tense, imperative mood: "add feature" not "added feature"

### 4. No AI Attribution
Never add "Co-Authored-By" or AI signatures to commits.

### 5. Push After Commit
After successful commit, push to remote branch using `git push`

## Workflow

1. **Check status** → `git status`
2. **Stage files** → `git add <files>` (or all if user didn't specify)
3. **Review changes** → `git diff --cached` 
4. **Analyze changes** → Determine commit type (feat/fix/refactor/etc.)
5. **Write commit message** → Conventional format based on changes
6. **Execute commit** → `git commit -m "type(scope): description"`
7. **Push to remote** → `git push`
8. **Confirm** → Show commit hash, branch, and push status

## Commands

```bash
# Check repository status
git status

# Stage specific files
git add <files>

# Review staged changes
git diff --cached

# Commit with conventional format
git commit -m "type(scope): description"

# Push to remote
git push

# Verify commit
git log -1 --oneline
```

## Conventional Commit Types

| Type | Use For |
|------|---------|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | Code restructuring without behavior change |
| `test` | Adding or updating tests |
| `docs` | Documentation changes |
| `chore` | Maintenance, build, CI |
| `style` | Formatting, whitespace |
| `perf` | Performance improvements |

## Commit Message Format

```
type(scope): imperative-description

Optional body with context.
```

Rules:
- Subject line max 72 characters
- Present tense, imperative mood
- No period at end of subject
- Scope is optional but recommended
- Body separated by blank line from subject

## Output Format

```yaml
status: committed | failed
commit_hash: "{short-hash}"
commit_message: "type(scope): description"
files_staged:
  - path/to/file1
  - path/to/file2
files_count: {number}
pushed: true | false
error: null | "description of error"
```

## Error Handling (Fail Fast)

| Condition | Status | Action |
|-----------|--------|--------|
| No changes staged | failed | Error: "No changes staged for commit" |
| No changes to commit | failed | Error: "Nothing to commit, working tree clean" |
| Merge conflict | failed | Error: "Resolve merge conflicts first" |
| Pre-commit hook fails | failed | Report hook output, do NOT use --no-verify |
| Push rejected | failed | Error: "Push rejected — pull latest changes first" |
| Empty repository | failed | Error: "Repository has no commits yet — initialize first" |

**No workarounds. No bypassing hooks. No force pushes. No force push.**