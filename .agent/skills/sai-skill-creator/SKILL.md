---
name: sai-skill-creator
description: >
  Creates new AI skills or agents following Agent Teams spec with orchestrator-driven user interaction.
  Trigger: When user asks to create a skill, agent, or document AI patterns.
license: Apache-2.0
metadata:
  author: salomon-ai
  version: "3.0"
allowed-tools: Read, Edit, Write, Bash
---

## Storage Locations

| Type | Location | Purpose |
|------|----------|---------|
| Skill | `.agent/skills/{name}/SKILL.md` | Skill definition, instructions |
| Agent | `.agent/agents/{name}.json` | Agent configuration, model, tools |
| Global | `~/.config/opencode/opencode.json` | System agent registry |

## Execution Architecture

This skill requires **TWO-PHASE execution** because sub-agents cannot interact with users:

| Phase | Who | Can Ask User? | Responsibility |
|-------|-----|---------------|----------------|
| **Phase 1: Discovery & Selection** | Orchestrator | ✅ Yes | Query providers, present models, collect user input |
| **Phase 2: Creation** | Sub-agent | ❌ No | Create files based on orchestrator-provided parameters |

**CRITICAL**: Never run the full flow as a single sub-agent delegation. User interaction MUST happen in orchestrator context.

---

## Phase 1: Orchestrator-Driven Discovery

### Step 1: Determine Type

**ORCHESTRATOR PROMPTS USER:**
```
Create:
[1] Normal skill (guidance only)
[2] Agent-linked skill (skill + dedicated AI model)

Selection:
```

**IF [1] Normal Skill** → Jump to [Normal Skill Creation](#step-3a-normal-skill-creation)

**IF [2] Agent-Linked Skill** → Continue to Step 2

### Step 2: Dynamic Model Discovery

**ORCHESTRATOR queries runtime providers:**

1. Query available models from connected providers:
   - OpenCode: Query `/v1/models` endpoint
   - OpenAI: Query `/v1/models` endpoint (if configured)
   - Anthropic: Query `/v1/models` endpoint (if configured)
   - Google: Query `/v1/models` endpoint (if configured)

2. Filter results:
   - Remove deprecated models
   - Remove models user lacks permission for
   - Remove models over quota limit
   - Include only: id, context_window, capabilities, cost_tier, provider

3. Cache for 5 minutes to avoid rate limits

**Fallback:**
- If provider query fails → show error and retry once
- If all providers fail → use local cache (last known good)

### Step 3: Present Options to User

**ORCHESTRATOR displays:**
```
Connected Providers: {count} ({provider names})

Available Models:
┌─ OpenCode ──────────────────────────────────────┐
│ 1. opencode/claude-sonnet-4-6                    │
│    Complex reasoning, orchestration (200k ctx)   │
│ 2. opencode-go/glm-5.1                           │
│    Coding, implementation (128k ctx)             │
│ 3. opencode-go/kimi-k2.5                         │
│    Fast analysis, reading (200k ctx)             │
├─ OpenAI ────────────────────────────────────────┤
│ 4. gpt-4-turbo                                   │
│    Coding, reasoning (128k ctx)                  │
│ 5. gpt-3.5-turbo                                 │
│    Simple tasks (16k ctx)                        │
└──────────────────────────────────────────────────┘

Select (1-{n}) or filter by capability [coding/fast/reasoning]:
```

**ORCHESTRATOR waits for user selection**

### Step 4: Collect Remaining Details

**ORCHESTRATOR asks user:**
```
Skill name: [auto-convert to kebab-case]
Description: [one line]
Mode: [1] subagent (default) [2] primary (orchestrator)
Tools needed: 
  [ ] bash
  [ ] read
  [ ] write
  [ ] edit
```

### Step 5: Delegate Creation to Sub-Agent

**ORCHESTRATOR delegates to sub-agent with ALL parameters:**

```yaml
task: create-agent-linked-skill
parameters:
  name: "{user-provided-name}"
  description: "{user-provided-description}"
  mode: "{subagent|primary}"
  model: "{user-selected-model-id}"
  provider: "{user-selected-provider}"
  tools: ["bash", "read", "write", "edit"]
  project_path: "{absolute-path}"
```

---

## Phase 2: Sub-Agent File Creation

**Sub-agent receives complete parameters from orchestrator**

### Step 3A: Normal Skill Creation

**EXECUTE (sub-agent creates files):**

```bash
mkdir -p .agent/skills/{name}/assets .agent/skills/{name}/references
```

**Create: `.agent/skills/{name}/SKILL.md`**
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
---

## When to Use

{context}

## Critical Patterns

{rules}

## Commands

{bash commands}
```

**RETURN to orchestrator:**
```yaml
status: created
type: skill-only
skill_path: ".agent/skills/{name}/SKILL.md"
```

### Step 3B: Agent-Linked Skill Creation

**EXECUTE (sub-agent creates files):**

**Create: `.agent/skills/{name}/SKILL.md`**
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

This skill runs as a **visible sub-agent** — user sees real-time output.

## When to Use

{context}

## Critical Patterns

{rules}

## Commands

{bash commands}
```

**Create: `.agent/agents/{name}.json`**
```json
{
  "name": "{name}",
  "description": "{description}",
  "mode": "{subagent|primary}",
  "model": "{model}",
  "provider": "{provider}",
  "prompt": "file:{project_path}/.agent/skills/{name}/SKILL.md",
  "tools": {
    "bash": {true|false},
    "read": {true|false},
    "write": {true|false},
    "edit": {true|false}
  }
}
```

**VALIDATE agent.json:**
- [ ] All required fields present
- [ ] Model format is valid (provider/model-id)
- [ ] Mode is valid (subagent/primary)
- [ ] Tools list valid
- [ ] Prompt path is absolute

**If invalid → RETURN error:**
```yaml
status: validation-failed
errors:
  - "Field 'model' missing"
  - "Invalid mode 'invalid-mode'"
```

**If valid → RETURN success:**
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
    model: "{model}"
    provider: "{provider}"
    discovered_at: "{timestamp}"
    prompt: "file:{project_path}/.agent/skills/{name}/SKILL.md"
    tools: {tools}
validation:
  status: passed
  checks: 5/5
auto_register: true
```

---

## Phase 3: Orchestrator Post-Processing

When orchestrator receives `status: agent-created` with `auto_register: true`:

### Auto-Edit Flow
1. **Read** `~/.config/opencode/opencode.json`
2. **Validate** JSON syntax
3. **Insert** agent entry under `"agent"` section
4. **CHECK** if permissions already exist:
   - Check for exact match: `"{name}": "allow"` → skip
   - Check for wildcard match: `"{prefix}-*": "allow"` (if {name} starts with {prefix}-) → skip
   - If no match → add `"{name}": "allow"`
5. **Update permissions** only if needed
6. **Validate** resulting JSON
7. **Write** file
8. **Confirm** to user

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

**Check existing permissions first:**

1. If wildcard pattern already covers this agent (e.g., `{prefix}-*` exists and agent name starts with `{prefix}-`), **no action needed**
2. If specific permission `"{name}": "allow"` already exists, **no action needed**
3. Otherwise, add specific permission:

```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow",
    "{name}": "allow"
  }
}
```

**Wildcard option** (recommended if creating multiple custom agents with same prefix):
```json
"permission": {
  "task": {
    "*": "deny",
    "sdd-*": "allow",
    "{prefix}-*": "allow"
  }
}
```

**Orchestrator MUST check** `~/.config/opencode/opencode.json` for existing patterns before showing permission instructions.

### Error Handling
- File locked → Retry 3x with delay
- Permission denied → Inform user to run with elevated permissions
- JSON parse error → Rollback and error
- Validation fail → Show details, abort

---

## Output Format

### Success — Normal Skill
```
[SUCCESS] Skill created: {name}
Location: .agent/skills/{name}/SKILL.md
Type: Standalone skill
```

### Success — Agent-Linked

**IF permissions already configured (wildcard covers agent):**
```
[SUCCESS] Agent created: {name}

Files:
  ✓ .agent/skills/{name}/SKILL.md
  ✓ .agent/agents/{name}.json
  ✓ ~/.config/opencode/opencode.json (auto-registered)

Configuration:
  Model: {selected-model}
  Provider: {selected-provider}
  Mode: {mode}
  Tools: {tools}
  Discovered: {timestamp}

✅ Permissions already configured (wildcard pattern detected)

Ready to use: task(subagent_type: "{name}", ...)
```

**IF permissions NOT configured:**
```
[SUCCESS] Agent created: {name}

Files:
  ✓ .agent/skills/{name}/SKILL.md
  ✓ .agent/agents/{name}.json
  ✓ ~/.config/opencode/opencode.json (auto-registered)

Configuration:
  Model: {selected-model}
  Provider: {selected-provider}
  Mode: {mode}
  Tools: {tools}
  Discovered: {timestamp}

⚠️  PERMISSION SETUP REQUIRED:
Add to ~/.config/opencode/opencode.json in ALL orchestrator profiles:

  "{name}": "allow"

Or use wildcard if you have multiple custom agents with same prefix:
  "{prefix}-*": "allow"

Ready to use: task(subagent_type: "{name}", ...)
```

### Error
```
[ERROR] Failed to create agent: {name}

Reason: {error details}
Suggestion: {how to fix}
```

---

## Critical Rules

1. **TWO-PHASE ARCHITECTURE** — Orchestrator handles user interaction, sub-agent handles file creation
2. **NEVER delegate user interaction** — Sub-agents cannot prompt users; orchestrator MUST collect all inputs before delegating
3. **SDD-style visibility** — All output visible to user in real-time
4. **Separation of concerns** — Skills in `skills/`, agents in `agents/`
5. **Validation mandatory** — JSON validated before and after edits
6. **Auto-registration** — No confirmation, orchestrator edits opencode.json immediately after sub-agent returns
7. **Atomic operations** — Either all files created + registered, or nothing
8. **Backup before edit** — Orchestrator backs up opencode.json before modification
9. **Conflict detection** — Check if agent name already exists before creating
10. **Dynamic discovery** — Models discovered from runtime providers, not static config
11. **Multi-provider support** — OpenCode, OpenAI, Anthropic, Google, etc.
12. **Live metadata** — Context window, capabilities, cost tier from provider APIs
13. **Permission configuration REQUIRED** — Agent WON'T WORK until permissions added to ALL orchestrator profiles in opencode.json

---

## Anti-Patterns (NEVER DO)

❌ **NEVER** run full sai-skill-creator flow as a single sub-agent delegation
❌ **NEVER** let sub-agent prompt user for model selection
❌ **NEVER** hardcode model defaults in sub-agent (user MUST choose)
❌ **NEVER** skip provider discovery and use cached/static model lists

---

## Example: Correct Orchestrator Implementation

```python
# PHASE 1: ORCHESTRATOR (user interaction)
user_choice = ask_user("Create:\n[1] Normal skill\n[2] Agent-linked skill")

if user_choice == "2":
    # Orchestrator queries providers
    models = query_available_models()  # Runtime discovery
    
    # Orchestrator presents to user
    display_model_menu(models)
    
    # Orchestrator collects selection
    selected_model = get_user_selection()
    name = ask("Skill name: ")
    description = ask("Description: ")
    mode = ask("Mode [1] subagent [2] primary: ")
    tools = ask_multi_select("Tools: ", ["bash", "read", "write", "edit"])
    
    # PHASE 2: DELEGATE to sub-agent (no user interaction)
    result = delegate(
        agent="sdd-apply-opencode-go",
        prompt=f"""
        Create agent-linked skill with these EXACT parameters:
        - name: {name}
        - description: {description}
        - mode: {mode}
        - model: {selected_model.id}
        - provider: {selected_model.provider}
        - tools: {tools}
        
        Do NOT ask user for input. Use provided parameters.
        """
    )
    
    # PHASE 3: ORCHESTRATOR registers agent
    if result.status == "agent-created":
        register_in_opencode_json(result.registration.entry)
```

(End of file)
