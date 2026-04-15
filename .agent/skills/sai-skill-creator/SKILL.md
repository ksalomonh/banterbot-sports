---
name: sai-skill-creator
description: >
  Creates new AI skills or agents following Agent Teams spec.
  Trigger: When user asks to create a skill, agent, or document AI patterns.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "2.1"
allowed-tools: Read, Edit, Write, Bash
---

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

#### Step 2B.1: Dynamic Model Discovery

**REQUEST from orchestrator:**
> "Discover available AI models from all connected providers"

**Orchestrator queries runtime providers:**
- Checks OpenCode provider connection
- Checks OpenAI provider connection (if configured)
- Checks Anthropic provider connection (if configured)
- Checks Google provider connection (if configured)
- Queries each provider's `/models` endpoint
- Filters by user's permissions and quotas

**Orchestrator returns:**
```yaml
provider: opencode
  models:
    - id: opencode/claude-sonnet-4-6
      capabilities: [orchestration, complex-reasoning]
      context_window: 200000
      cost_tier: high
    - id: opencode-go/glm-5.1
      capabilities: [coding, implementation]
      context_window: 128000
      cost_tier: medium
    - id: opencode-go/kimi-k2.5
      capabilities: [fast-analysis, reading]
      context_window: 200000
      cost_tier: low

provider: openai
  models:
    - id: gpt-4-turbo
      capabilities: [coding, reasoning]
      context_window: 128000
      cost_tier: high
    - id: gpt-3.5-turbo
      capabilities: [simple-tasks]
      context_window: 16000
      cost_tier: low
```

**PRESENT to user:**
```
Connected Providers: 2 (OpenCode, OpenAI)

Available Models:
┌─ OpenCode ──────────────────────────────────────┐
│ 1. opencode/claude-sonnet-4-6                    │
│    Complex reasoning, orchestration (200k ctx)   │
│ 2. opencode-go/glm-5.1 ← RECOMMENDED             │
│    Coding, implementation (128k ctx)             │
│ 3. opencode-go/kimi-k2.5                         │
│    Fast analysis, reading (200k ctx)             │
├─ OpenAI ────────────────────────────────────────┤
│ 4. gpt-4-turbo                                   │
│    Coding, reasoning (128k ctx)                  │
│ 5. gpt-3.5-turbo                                 │
│    Simple tasks (16k ctx)                        │
└──────────────────────────────────────────────────┘

Select (1-5) or filter by capability [coding/fast/reasoning]:
```

#### Provider Discovery Rules

**Orchestrator MUST:**
1. Query runtime provider connections (not static config)
2. Call provider APIs to get live model lists:
   - OpenCode: `GET /v1/models`
   - OpenAI: `GET /v1/models`
   - Anthropic: `GET /v1/models`
3. Filter out:
   - Deprecated models
   - Models user lacks permission for
   - Models over quota limit
4. Cache results for 5 minutes to avoid rate limits
5. Include metadata:
   - Context window size
   - Capabilities/tags
   - Cost tier (low/medium/high)
   - Provider name

**Fallback:**
If provider query fails → show error and retry once
If all providers fail → fallback to local cache (last known good)

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

## SDD Execution Mode

This skill runs as a **visible sub-agent** — user sees real-time output via Ctrl+O for Claude and Ctrl+x down for OpenCode.

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
  "provider": "{selected-provider}",
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
    provider: "{selected-provider}"
    discovered_at: "{timestamp}"
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
4. **Update permissions** for all orchestrator profiles (see Permission Configuration below)
5. **Validate** resulting JSON
6. **Write** file
7. **Confirm** to user

### Permission Configuration

**CRITICAL:** For the new agent to be delegable, permissions must be updated in ALL orchestrator profiles.

**For each profile** (`sdd-orchestrator`, `sdd-orchestrator-opencode-go`, etc.):

Locate the profile's permission block:
```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow"
  }
}
```

Add the new agent pattern:
```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow",
    "{agent-name}": "allow"
  }
}
```

**Example for `{name}`:**
```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow",
    "{name}": "allow"
  }
}
```

**Wildcard option** (if you have multiple custom agents with same prefix):
```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow",
    "{prefix}-*": "allow"
  }
}
```

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
  Provider: opencode
  Mode: subagent
  Tools: bash, read, write, edit
  Discovered: 2026-04-14T20:45:00Z

⚠️  PERMISSION SETUP REQUIRED:
Add to ~/.config/opencode/opencode.json in ALL orchestrator profiles:

  "{name}": "allow"

Or use wildcard if you have multiple custom agents with same prefix:
  "{prefix}-*": "allow"

Ready to use: task(subagent_type: "{name}", ...)
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
8. **Dynamic discovery** — Models discovered from runtime providers, not static config
9. **Multi-provider support** — OpenCode, OpenAI, Anthropic, Google, etc.
10. **Live metadata** — Context window, capabilities, cost tier from provider APIs
11. **Permission configuration REQUIRED** — Agent WON'T WORK until permissions added to ALL orchestrator profiles in opencode.json
