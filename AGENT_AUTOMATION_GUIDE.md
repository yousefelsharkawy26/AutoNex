# AI Agent Desktop Automation Guide (Token-Efficient)

This guide documents how AI Coding Agents (such as Claude, Gemini, ChatGPT, or custom LLM test runners) can automate desktop testing with **minimal token footprint**.

---

## 🎯 The Token Dilemma in Desktop Automation

Traditional desktop automation strategies waste massive amounts of tokens:
1. **Dumping full accessibility trees**: Exposes thousands of non-interactive layout containers, borders, and margins (often 10,000+ tokens per screen).
2. **Vision-only (Full 4K screenshots)**: Sending raw screenshots on every step costs thousands of vision tokens per interaction.
3. **Step-by-step chat round trips**: Clicking a button, waiting for chat reply, typing text, waiting for reply creates 5–10 LLM turns for a simple form.
4. **Trial-and-error retry loops**: Guessing locator names fails repeatedly when an ID has slight variations (e.g., `submit_btn` vs `SubmitButton`).

---

## 🚀 The Solution: `AgentAutomationEngine`

The framework includes a built-in agent automation engine designed specifically to solve these problems:

```
┌──────────────────────────────────────────────────────────────┐
│                    AgentAutomationEngine                     │
├─────────────────────┬──────────────────────┬─────────────────┤
│    CompactUiTree    │   AgentBatchRunner   │  FuzzyLocator   │
│   (Semantic dump)   │ (1 tool call/batch)  │  (Self-healing) │
└─────────────────────┴──────────────────────┴─────────────────┘
                                ▲
                                │ UI Delta
                      ┌───────────────────┐
                      │ AgentStateTracker │
                      └───────────────────┘
```

---

## 🛠️ Step 1: Inspect the Screen with Minimal Tokens

Instead of dumping the raw UI tree or taking a screenshot, request a **compact UI dump**:

```csharp
var engine = new AgentAutomationEngine(driver);
string screenDump = engine.InspectScreen("text");
```

### Output:
```text
[Edit #FirstNameTextBox]
[Edit #LastNameTextBox]
[Edit #AgeTextBox]
[ComboBox #CountryComboBox val="United States"]
[CheckBox #RememberMeCheckBox (unchecked)]
[RadioButton #UserRoleRadioButton (selected)]
[RadioButton #AdminRoleRadioButton]
[Button #SubmitButton "Submit"]
[Button #ClearButton "Clear"]
[Text #ResultTextBlock]
```
> **Token Impact**: Reduced from **~8,000 tokens** to **less than 150 tokens**!

---

## ⚡ Step 2: Execute Multi-Step Test Plans in 1 Call

Instead of issuing separate tool calls for each click and type, batch your complete test scenario in a single JSON payload:

```json
[
  { "action": "type", "id": "FirstNameTextBox", "value": "Ahmed" },
  { "action": "type", "id": "LastNameTextBox", "value": "Ali" },
  { "action": "type", "id": "AgeTextBox", "value": "30" },
  { "action": "select", "id": "CountryComboBox", "value": "Egypt" },
  { "action": "check", "id": "RememberMeCheckBox" },
  { "action": "click", "id": "AdminRoleRadioButton" },
  { "action": "click", "id": "SubmitButton" },
  { "action": "assert_text", "id": "ResultTextBlock", "contains": "Hello, Ahmed Ali!" }
]
```

### Execution:
```csharp
string resultSummary = engine.ExecutePlan(jsonPlan);
```

### Response:
```text
SUCCESS: 8/8 steps executed successfully. | Delta: #FirstNameTextBox val: "" -> "Ahmed"; #LastNameTextBox val: "" -> "Ali"; #AgeTextBox val: "" -> "30"; #CountryComboBox val: "United States" -> "Egypt"; #RememberMeCheckBox state: unchecked -> checked; #AdminRoleRadioButton state: normal -> selected; #ResultTextBlock val: "" -> "Hello, Ahmed Ali! You are 30 years old from Egypt (Admin)."
```
> **Token Impact**: Replaces 8 individual chat turns and tool round-trips with **1 single tool call**!

---

## 🩹 Step 3: Self-Healing Locators (No Guesswork Retries)

If the agent generates slightly approximate or colloquial IDs:
- Agent sends `submit_btn` ➔ Resolved to `SubmitButton`
- Agent sends `fname_txt` ➔ Resolved to `FirstNameTextBox`
- Agent sends `remember_me` ➔ Resolved to `RememberMeCheckBox`

The `FuzzyLocator` calculates normalized Levenshtein similarity against available UI candidates and self-heals in real time without failing the test or triggering expensive retry conversations.

---

## 📊 Summary of Supported Actions

| Action | Required Fields | Description |
|---|---|---|
| `click` | `id` | Clicks button, radio button, or clickable element |
| `type` | `id`, `value` | Enters text into textboxes |
| `check` | `id` | Checks a checkbox |
| `uncheck` | `id` | Unchecks a checkbox |
| `select` | `id`, `value` | Selects ComboBox item by text or RadioButton |
| `double_click` | `id` | Double-clicks an element |
| `right_click` | `id` | Right-clicks an element |
| `hover` | `id` | Hovers mouse pointer over element |
| `press_key` | `key` | Sends a key stroke (`"Return"`, `"Tab"`, `"Escape"`) |
| `send_keys` | `value` | Sends raw string keystrokes globally |
| `assert_text` | `id`, `contains` or `value` | Verifies element text content |

---

## 🔌 Using with Claude Desktop / Cursor / Antigravity via MCP

The framework includes a ready-to-run **Model Context Protocol (MCP) Server** located in `DesktopAutomationTest.McpServer`.

### MCP Configuration:
Add this to your MCP configuration file (e.g. `claude_desktop_config.json` or your agent settings):

```json
{
  "mcpServers": {
    "autonex-desktop": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "path/to/DesktopAutomationTest.McpServer/DesktopAutomationTest.McpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

### Exposed MCP Tools (9 Tools Total - v1.2.0):
1. **`launch_application`**: Starts the desktop app and connects FlaUI driver.
2. **`inspect_screen`**: Returns compact UI elements hierarchy (~90% fewer tokens).
3. **`execute_plan`**: Runs a multi-step batch in a single tool call with auto-healing and delta tracking.
4. **`wait_for_element`**: Polls until an element appears (configurable timeout, fails fast).
5. **`get_element_text`**: Lightweight read of a specific element's text without re-scanning the screen.
6. **`take_screenshot`**: Captures screen to PNG when visual verification is explicitly necessary.
7. **`record_session`**: Executes a plan AND records every action + result into a session JSON file.
8. **`replay_session`**: Replays a previously recorded session with **ZERO LLM tokens**.
9. **`close_application`**: Cleanly shuts down the app and frees memory.

---

## 🔁 Step 4: Zero-Token Replay Strategy

Once an AI Agent successfully navigates and automates a scenario, it can persist the session:
1. Use `record_session` (or `autonex record`) to execute the plan and save `session.json`.
2. On subsequent regression test runs, use `replay_session` (or `autonex replay`).
3. **Cost**: **0 LLM Tokens consumed**. The engine replays native actions and handles any slight UI ID shifts using `FuzzyLocator` self-healing.

---

## 💻 Step 5: AutoNex CLI Tool (`autonex`)

You can also run token-efficient automation directly from the terminal or CI/CD pipelines:

```bash
# Dump interactive UI elements (compact format)
autonex inspect --app "C:\MyApp\MyApp.exe"

# Execute a JSON batch plan
autonex run --app "C:\MyApp\MyApp.exe" --plan ./login_plan.json

# Record an automation flow for future zero-token replays
autonex record --app "C:\MyApp\MyApp.exe" --plan ./login_plan.json --out ./sessions/login.json --label "Login Flow"

# Replay a session at zero token cost
autonex replay --app "C:\MyApp\MyApp.exe" --session ./sessions/login.json

# Utility commands
autonex wait --app "C:\MyApp\MyApp.exe" --id SubmitButton --timeout 15
autonex text --app "C:\MyApp\MyApp.exe" --id ResultLabel
autonex shot --app "C:\MyApp\MyApp.exe" --out ./screenshot.png
```



