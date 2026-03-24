<!--
  ⚠️  READ BEFORE SUBMITTING

  Every PR must:
  1. Link an approved issue (status:approved label)
  2. Have exactly one type:* label
  3. Pass all automated checks (issue reference, type label, dotnet build)

  See .agent/skills/branch-pr/SKILL.md for the full workflow.
-->

## Linked Issue

<!-- REQUIRED: Replace N with the issue number -->
Closes #

---

## PR Type

<!-- Check exactly ONE, then add the matching label to this PR -->

- [ ] `type:bug` — Bug fix
- [ ] `type:feature` — New feature
- [ ] `type:docs` — Documentation only
- [ ] `type:refactor` — Code refactoring (no behavior change)
- [ ] `type:chore` — Maintenance, dependencies, tooling
- [ ] `type:breaking-change` — Breaking change

---

## Summary

<!-- 1-3 bullet points of what this PR does -->

-

## Changes

| File | Change |
|------|--------|
| `path/to/file` | What changed |

## Test Plan

- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] GGA pre-commit hook passed on all commits
- [ ] Manually tested the affected functionality

---

## Automated Checks

| Check | What it verifies | Status |
|-------|-----------------|--------|
| **Issue Reference** | PR body contains `Closes #N` | ⏳ |
| **Issue Approved** | Linked issue has `status:approved` | ⏳ |
| **Type Label** | PR has exactly one `type:*` label | ⏳ |
| **Build** | `dotnet build` succeeds | ⏳ |

---

## Checklist

- [ ] Linked an approved issue (`Closes #N`)
- [ ] Added exactly one `type:*` label
- [ ] All commits follow conventional commits format
- [ ] No `Co-Authored-By` trailers in commits
- [ ] Entity names in Spanish (Torneo, Jornada, Partido, etc.)
- [ ] No hardcoded point values or prize percentages
- [ ] Async/await for all I/O operations

## Notes for Reviewers

<!-- Optional: context, tradeoffs, open questions -->
