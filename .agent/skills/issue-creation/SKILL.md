---
name: issue-creation
description: >
  Issue creation workflow for BanterBot Sports.
  Trigger: When creating a GitHub issue (bug or feature request).
---

## Critical Rules
1. Blank issues are disabled — use a template
2. Every issue gets `status:needs-review` automatically on creation
3. A maintainer adds `status:approved` before any PR can open
4. Questions → Discussions, NOT issues

## Workflow
```
1. gh issue list --search "keyword" → check for duplicates
2. Choose template: bug_report or feature_request
3. Fill ALL required fields
4. Submit → gets status:needs-review
5. Wait for status:approved before opening a PR
```

## Commands
```bash
# Search existing issues
gh issue list --search "telegram bot"

# Create bug report
gh issue create \
  --title "fix(scope): description" \
  --label "bug" \
  --body "$(cat <<'EOF'
## Pre-flight Checks
- [x] Searched existing issues — not a duplicate
- [x] I understand this needs status:approved before a PR

## Bug Description
[clear description]

## Steps to Reproduce
1.
2.
3.

## Expected Behavior


## Actual Behavior


## Context
- .NET version:
- Environment:
EOF
)"

# Create feature request
gh issue create \
  --title "feat(scope): description" \
  --label "enhancement" \
  --body "$(cat <<'EOF'
## Pre-flight Checks
- [x] Searched existing issues — not a duplicate
- [x] I understand this needs status:approved before a PR

## Problem Description


## Proposed Solution


## Affected Area
[ ] BL  [ ] DAL  [ ] Web  [ ] BanterAI  [ ] Integrations  [ ] Entities

## Alternatives Considered

EOF
)"

# Maintainer: approve issue
gh issue edit <number> --add-label "status:approved"
```
