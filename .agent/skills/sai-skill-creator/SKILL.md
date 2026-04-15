---
name: sai-skill-creator
description: >
  Creates new AI skills or agents following Agent Teams spec. SDD-style execution with real-time visibility.
  Trigger: When user asks to create a skill, agent, or document AI patterns.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "2.0"
allowed-tools: Read, Edit, Write, Bash
---

## SDD Execution Mode

This skill runs as a **visible sub-agent** — user sees real-time output via Ctrl+O.

## Storage Locations

| Type | Location | Purpose |
|------|----------|---------|
| Skill | `.agent/skills/{name}/SKILL.md` | Skill definition, instructions |
| Agent | `.agent/agents/{name}.json` | Agent configuration, model, tools |
| Global | `~/.config/opencode/opencode.json` | System agent registry |

## Creation Flow

### Step 1: Determine Type

**PROMPT USER:**
```
Create:
[1] Normal skill (guidance only)
[2] Agent-linked skill (skill + dedicated AI model)

Selection:
```

### Step 2A: Normal Skill

**EXECUTE (visible to user):**
```bash
mkdir -p .agent/skills/{name}/assets .agent/skills/{name}/references
# Create SKILL.md
```

**RETURN:**
```yaml
status: created
type: skill-only
skill_path: ".agent/skills/{name}/SKILL.md"
```

### Step 2B: Agent-Linked Skill

#### Step 2B.1: Get Models from Orchestrator

**REQUEST from orchestrator:**
> "Provide list of available AI models from ~/.config/opencode/opencode.json"

**Orchestrator returns:**
```json
{
  "models": [
    {"id": "opencode/claude-sonnet-4-6", "type": "orchestrator"},
    {"id": "opencode-go/glm-5.1", "type": "coding"},
    {"id": "opencode-go/kimi-k2.5", "type": "fast-analysis"},
    {"id": "opencode-go/minimax-m2.7", "type": "simple-ops"}
  ]
}
```

**PRESENT to user:**
```
Available Models:
1. opencode/claude-sonnet-4-6 (complex reasoning, orchestration)
2. opencode-go/glm-5.1 (coding, implementation)
3. opencode-go/kimi-k2.5 (fast analysis, reading)
4. opencode-go/minimax-m2.7 (simple operations)

Select (1-4):
```

#### Step 2B.2: Collect Details

**ASK:**
- Skill name: (auto-kebab-case)
- Description:
- Mode: [1] subagent (default) [2] primary (orchestrator)
- Tools needed: [list checkboxes]

#### Step 2B.3: Create Files (Visible Execution)

**OUTPUT to user:**
```
Creating skill files...
✓ .agent/skills/sai-db-migrator/SKILL.md
✓ .agent/skills/sai-db-migrator/assets/
✓ .agent/skills/sai-db-migrator/references/
✓ .agent/agents/sai-db-migrator.json
```

**File: `.agent/skills/{name}/SKILL.md`**
```yaml
---
name: {name}
description: >
  {description}.
  Trigger: {trigger keywords}.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "1.0"
  agent-linked: true
allowed-tools: {tools}
---

## When to Use

{context}

## Critical Patterns

{rules}

## Commands

{bash commands}
```

**File: `.agent/agents/{name}.json`**
```json
{
  "name": "{name}",
  "description": "{description}",
  "mode": "{subagent|primary}",
  "model": "{selected-model}",
  "prompt": "file:{project}/.agent/skills/{name}/SKILL.md",
  "tools": {
    "bash": true,
    "read": true,
    "write": true,
    "edit": true
  }
}
```

#### Step 2B.4: Validation

**VALIDATE agent.json:**
- [ ] All required fields present
- [ ] Model exists in system
- [ ] Mode is valid (subagent/primary)
- [ ] Tools list valid
- [ ] Prompt path is absolute

**If invalid → ERROR with details**

**If valid → CONTINUE**

#### Step 2B.5: Return Registration Payload

**RETURN to orchestrator:**
```yaml
status: agent-created
type: agent-linked
files_created:
  - .agent/skills/{name}/SKILL.md
  - .agent/agents/{name}.json
registration:
  target: "~/.config/opencode/opencode.json"
  entry:
    name: "{name}"
    description: "{description}"
    mode: "{mode}"
    model: "{selected-model}"
    prompt: "file:{absolute-path}/.agent/skills/{name}/SKILL.md"
    tools: {tools}
validation:
  status: passed
  checks: 5/5
auto_register: true
```

## Orchestrator Post-Processing

When orchestrator receives `status: agent-created` with `auto_register: true`:

### Auto-Edit Flow
1. **Read** `~/.config/opencode/opencode.json`
2. **Validate** JSON syntax
3. **Insert** agent entry under `"agent"` section
4. **Validate** resulting JSON
5. **Write** file
6. **Confirm** to user

### Error Handling
- File locked → Retry 3x with delay
- Permission denied → Inform user to run with elevated permissions
- JSON parse error → Rollback and error
- Validation fail → Show details, abort

## Output Format

### Success — Normal Skill
```
[SUCCESS] Skill created: sai-validator
Location: .agent/skills/sai-validator/SKILL.md
Type: Standalone skill
```

### Success — Agent-Linked
```
[SUCCESS] Agent created: sai-db-migrator

Files:
  ✓ .agent/skills/sai-db-migrator/SKILL.md
  ✓ .agent/agents/sai-db-migrator.json
  ✓ ~/.config/opencode/opencode.json (auto-registered)

Configuration:
  Model: opencode-go/kimi-k2.5
  Mode: subagent
  Tools: bash, read, write, edit

Ready to use: task(subagent_type: "sai-db-migrator", ...)
```

### Error
```
[ERROR] Failed to create agent: sai-db-migrator

Reason: {error details}
Suggestion: {how to fix}
```

## Critical Rules

1. **SDD-style visibility** — All output visible to user in real-time
2. **Separation of concerns** — Skills in `skills/`, agents in `agents/`
3. **Validation mandatory** — JSON validated before and after edits
4. **Auto-registration** — No confirmation, orchestrator edits opencode.json immediately
5. **Atomic operations** — Either all files created + registered, or nothing
6. **Backup before edit** — Orchestrator backs up opencode.json before modification
7. **Conflict detection** — Check if agent name already exists before creating
