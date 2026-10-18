# AI Agent Desktop Automation Guide (Token-Efficient)

This guide documents how AI Coding Agents (such as Claude, Cursor, Antigravity, Gemini, or custom LLM test runners) can automate desktop testing with **minimal token footprint and high reliability**.

---

## 🎯 The Token Dilemma in Desktop Automation

Traditional desktop automation strategies waste massive amounts of tokens:
1. **Dumping full accessibility trees**: Exposes thousands of non-interactive layout containers, borders, and margins (often 10,000+ tokens per screen).
2. **Vision-only (Full screenshots)**: Sending raw screenshots on every step costs thousands of vision tokens per interaction.
3. **Step-by-step chat round trips**: Clicking a button, waiting for chat reply, typing text, waiting for reply creates 5–10 LLM turns for a simple form.
4. **Trial-and-error retry loops**: Guessing locator names fails repeatedly when an ID has slight variations (e.g., `submit_btn` vs `SubmitButton`).
5. **Session drop on reconnect**: Losing the connection restarts or loses track of the open application.

---

## 🚀 The Solution: `AutoNex` & `AgentAutomationEngine`

The framework includes a built-in agent automation engine designed specifically to solve these problems:

```
┌────────────────────────────────────────────────────────────────────────┐
│                         AgentAutomationEngine                          │
├────────────────────┬────────────────────┬────────────────┬─────────────┤
│   CompactUiTree    │  AgentBatchRunner  │  FuzzyLocator  │ State Cache │
│ (Semantic + List   │ (1 call / batch +  │ (Self-healing  │ (diff_only  │
│   Summarization)   │  dry-run + assert) │   locators)    │ deduplicate)│
└────────────────────┴────────────────────┴────────────────┴─────────────┘
                               ▲
                               │ UI Delta & Diff
                     ┌───────────────────┐
                     │ AgentStateTracker │
                     └───────────────────┘
```

---

## 🤖 Recommended System Prompt for AI Agents

Add this system prompt to your agent configuration in Claude Desktop, Cursor rules (`.cursorrules`), or Antigravity custom instructions:

```markdown
You are a Desktop Automation Specialist using the AutoNex MCP server.
Follow these token-efficient best practices:

1. WORKFLOW ORDER:
   - Launch or attach: Use `launch_application` or `attach_to_process`.
   - Initial Inspect: Call `inspect_screen(format="text")` to discover active controls.
   - Subsequent Inspects: ALWAYS pass `diff_only=true` (`inspect_screen(format="text", diff_only=true)`). This returns "[No UI changes]" if nothing changed, saving thousands of tokens.
   - Batch Execution: ALWAYS group multiple actions into a single `execute_plan` call instead of calling single actions one-by-one.
   - Verification: Include assertions (`"action": "assert"`) directly inside `execute_plan` steps.

2. PLANNING & EXECUTION:
   - Format actions as JSON array in `execute_plan`:
     [
       { "action": "type", "id": "UsernameBox", "value": "admin", "clear": true },
       { "action": "type", "id": "PasswordBox", "value": "secret" },
       { "action": "click", "id": "LoginButton" },
       { "action": "wait", "id": "WelcomeBanner", "timeout": 5 },
       { "action": "assert", "id": "WelcomeBanner", "contains": "Welcome" }
     ]
   - Supported actions: 'click', 'type', 'paste', 'check', 'uncheck', 'select', 'double_click', 'right_click', 'hover', 'press_key', 'send_keys', 'wait', 'assert', 'scroll', 'scroll_into_view', 'clear'.
   - Dry-Run: When unsure of locators, set `dry_run: true` in `execute_plan` to validate element presence without executing destructive actions.

3. SCROLLING & LARGE LISTS:
   - Large lists are automatically summarized (e.g. "... and 45 more items omitted").
   - To target offscreen items in long lists, use `scroll_into_view(id="targetId")` or `scroll(direction="down")`.

4. MULTI-WINDOW & DIALOGS:
   - If an action opens a dialog or secondary window, use `list_windows` to inspect open windows, then `switch_window(title="...")` to switch context before interacting.

5. SELF-HEALING:
   - AutoNex fuzzy-matches IDs. If you make a minor spelling typo (e.g. `submit_btn` instead of `SubmitButton`), AutoNex will self-heal automatically.
```

---

## 🔌 MCP Configuration (Claude / Cursor / Antigravity)

### 1. Claude Desktop (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "autonex": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/DesktopAutomationTest.McpServer/DesktopAutomationTest.McpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

### 2. Cursor (`.cursor/mcp.json`)
```json
{
  "mcpServers": {
    "autonex": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/DesktopAutomationTest.McpServer/DesktopAutomationTest.McpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

### 3. Antigravity MCP Setup
Add `autonex` as an stdio MCP server in your workspace configuration or start the server with optional `--verbose` flag for timestamped debug diagnostics:
```bash
dotnet run --project DesktopAutomationTest.McpServer/DesktopAutomationTest.McpServer.csproj --verbose
```

---

## 🛠️ Complete MCP Tool Reference (v1.6.0)

| Tool Name | Key Parameters | Description |
|---|---|---|
| `launch_application` | `path`, `arguments`, `driver` ("flaui" \| "avalonia") | Launches target app and initializes automation session. Auto-persists session PID. |
| `attach_to_process` | `process_id`, `process_name` | Attaches to an already running app without relaunching. |
| `inspect_screen` | `format` ("text" \| "json"), `diff_only` (bool), `max_depth` (int), `container_id` (string) | Returns compact interactive UI elements (~90% fewer tokens). Supports caching, depth limits, and list summarization. |
| `execute_plan` | `plan_json` (string), `dry_run` (bool) | Executes a multi-action plan in a single call with auto-healing, multi-assert, and UI state delta. |
| `wait_for_element` | `id`, `timeout_seconds` | Polls until an element appears. Auto-heals misspelled IDs. |
| `click_element` | `id` | Clicks an element with fuzzy self-healing. |
| `type_text` | `id`, `text`, `clear` (bool) | Fast text entry with optional pre-clear. |
| `get_element_text` | `id` | Reads the current text/value of an element. |
| `take_screenshot` | `output_path`, `include_image` (bool) | Takes a screenshot. Returns base64 PNG inline when `include_image=true`. |
| `scroll` | `direction` ("down" \| "up" \| "left" \| "right"), `id` | Scrolls active window or container element. |
| `scroll_into_view` | `id` | Scrolls container until the target element is visible. |
| `list_windows` | *(none)* | Lists all application windows and dialogs with their titles and PIDs. |
| `switch_window` | `title`, `timeout_seconds` | Switches automation context to a window or modal dialog matching title. |
| `record_session` | `plan_json`, `session_path`, `label` | Executes plan and saves complete session JSON for zero-token future replays. |
| `replay_session` | `session_path` | Replays a previously recorded session with **ZERO LLM tokens**. |
| `export_session_as_test` | `session_path`, `format` ("csharp" \| "gherkin") | Converts a recorded session into xUnit C# or Reqnroll Gherkin scenarios. |
| `close_application` | *(none)* | Closes application, clears session file, and releases COM/driver resources. |

---

## ⚡ Intelligent Token Optimization Highlights

### 1. Compact UI Hierarchy & Intelligent List Summarization
Instead of 10,000+ tokens of layout elements and repeated items:
```text
[Edit #FirstNameTextBox]
[Edit #LastNameTextBox]
[ComboBox #CountryComboBox val="United States"]
[ListItem #Item_1 "Row 1"]
[ListItem #Item_2 "Row 2"]
[ListItem #Item_3 "Row 3"]
[ListItem "... and 97 more ListItem items omitted (100 total)"]
[Button #SubmitButton "Submit"]
```

### 2. State Caching with `diff_only=true`
Calling `inspect_screen(diff_only=true)` returns:
```text
[No UI changes since last inspect]
```
Saves 100% of the token cost on redundant inspection steps.

### 3. Dry-Run Validation (`dry_run=true`)
Validate complex multi-step plans and ensure all locators exist on screen before making live modifications:
```json
{
  "plan_json": "[{\"action\": \"click\", \"id\": \"NonExistentButton\"}]",
  "dry_run": true
}
```
Returns:
```text
Step 1 failed: [Dry-run] Element 'NonExistentButton' not found on screen.
```

### 4. Automatic Session Persistence & Reconnect
If the MCP client disconnects or restarts:
- AutoNex stores active session info in `.autonex_mcp_session.json`.
- On reconnect, AutoNex automatically verifies the PID is still alive and re-attaches seamlessly.

---

## 💻 AutoNex CLI (`autonex`)

Run automation directly from bash/terminal or CI pipelines:

```bash
# Dump compact UI tree
autonex inspect --app "C:\MyApp\MyApp.exe"

# Execute batch plan with dry-run
autonex run --app "C:\MyApp\MyApp.exe" --plan ./plan.json

# Record flow for zero-token regression testing
autonex record --app "C:\MyApp\MyApp.exe" --plan ./plan.json --out ./sessions/flow.json

# Replay flow at zero LLM cost
autonex replay --session ./sessions/flow.json
```
