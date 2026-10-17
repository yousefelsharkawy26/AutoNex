using System;
using System.IO;
using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Provides high-level convenience facade for AI Coding Agents.
/// Minimizes LLM tool overhead by exposing token-optimized single-point operations:
/// - Dump UI with minimal token footprint
/// - Execute multi-step action plans in 1 call with self-healing and delta tracking
/// - Record sessions to JSON for zero-token replay
/// - Replay recorded sessions (zero LLM cost)
/// - Assert UI state efficiently
/// </summary>
public class AgentAutomationEngine
{
    private readonly IDriver _driver;
    private readonly AgentBatchRunner _batchRunner;

    public AgentAutomationEngine(IDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _batchRunner = new AgentBatchRunner(_driver);
    }

    /// <summary>
    /// Gets a token-minimized representation of the UI interactive elements.
    /// Recommended to call at the start of a test session.
    /// Format: "text" (minimal tokens, e.g. [Button #Submit "Submit"]) or "json".
    /// </summary>
    public string InspectScreen(string format = "text")
    {
        return _driver.GetCompactUiTree(format);
    }

    /// <summary>
    /// Runs a batch of actions specified as a compact JSON string.
    /// Supports: click, type, check, uncheck, select, hover, double_click, right_click, press_key, send_keys, assert_text.
    /// Auto-heals misspelled IDs and returns a single-line summary with UI state delta.
    /// </summary>
    public string ExecutePlan(string jsonActions)
    {
        var result = _batchRunner.ExecuteJson(jsonActions);
        return result.ToCompactSummary();
    }

    /// <summary>
    /// Quick single-assertion helper for agents.
    /// </summary>
    public bool AssertElementTextContains(string automationId, string expectedText)
    {
        if (_driver.TryGetTextByAutomationId(automationId, out var actualText))
        {
            return actualText?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true;
        }
        return false;
    }

    /// <summary>
    /// Polls until an element with the given automationId appears or the timeout expires.
    /// Returns a human-readable result string suitable for MCP tool responses.
    /// </summary>
    public string WaitForElement(string automationId, int timeoutSeconds = 10)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var found = _driver.TryFindByAutomationId(automationId, timeout);
        return found
            ? $"Element '{automationId}' found within {timeoutSeconds}s."
            : $"Timeout: element '{automationId}' not found after {timeoutSeconds}s.";
    }

    /// <summary>
    /// Returns the text of the element with the given automationId.
    /// Returns a human-readable result string suitable for MCP tool responses.
    /// </summary>
    public string GetElementText(string automationId)
    {
        if (_driver.TryGetTextByAutomationId(automationId, out var text))
            return text ?? string.Empty;
        return $"Error: element '{automationId}' not found.";
    }

    /// <summary>
    /// Takes a screenshot of the current screen and saves it to the specified path.
    /// Returns the saved file path on success, or an error message.
    /// </summary>
    public string TakeScreenshot(string? outputPath = null)
    {
        outputPath ??= Path.Combine(
            Path.GetTempPath(),
            $"autonex_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

        var ok = _driver.TakeScreenshot(outputPath);
        return ok
            ? $"Screenshot saved: {outputPath}"
            : $"Error: screenshot could not be taken.";
    }

    // ── [SESSION-REPLAY] Record & Replay ──────────────────────────────────────

    /// <summary>
    /// Executes a JSON plan AND records every step to a session file.
    /// Useful when the agent runs a plan for the first time and wants to persist it
    /// for zero-token replay in future runs.
    /// </summary>
    /// <param name="jsonActions">JSON array of actions (same format as ExecutePlan).</param>
    /// <param name="sessionFilePath">Path to save the session JSON. Auto-generated if null.</param>
    /// <param name="label">Optional human-readable label for this session.</param>
    /// <returns>Compact summary string including the session file path.</returns>
    public string ExecuteAndRecord(string jsonActions, string? sessionFilePath = null, string? label = null)
    {
        sessionFilePath ??= Path.Combine(
            Path.GetTempPath(),
            $"autonex_session_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

        // Parse the actions
        var actions = JsonSerializer.Deserialize<System.Collections.Generic.List<AgentAction>>(jsonActions,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new();

        var recorder = new SessionRecorder(_driver, label: label);

        foreach (var action in actions)
        {
            recorder.RecordStep(action.Action, action.AutomationId, action.Value);
        }

        recorder.Save(sessionFilePath);

        var summary = recorder.Session.ToSummary();
        return $"{summary}\nSession saved: {sessionFilePath}";
    }

    /// <summary>
    /// Replays a previously recorded session JSON file.
    /// Zero LLM tokens consumed — reads file and drives UI directly.
    /// Uses FuzzyLocator for self-healing if element IDs changed slightly.
    /// </summary>
    /// <param name="sessionFilePath">Path to the session JSON file.</param>
    /// <returns>Compact result summary.</returns>
    public string ReplaySession(string sessionFilePath)
    {
        var replayer = new SessionReplayer(_driver);
        var result   = replayer.ReplayFile(sessionFilePath);
        return result.ToSummary();
    }

    // ── [MULTI-WINDOW] Window Context Management ──────────────────────────────

    /// <summary>
    /// Lists titles of all currently open top-level windows belonging to the application.
    /// </summary>
    public string ListWindows()
    {
        var titles = _driver.GetOpenWindowTitles();
        if (titles.Count == 0) return "No open windows found.";
        return string.Join("\n", titles.Select((t, i) => $"[{i}] {t}"));
    }

    /// <summary>
    /// Switches automation context to a window by title or substring.
    /// </summary>
    public string SwitchWindow(string titleSubstring, int timeoutSeconds = 5)
    {
        var ok = _driver.SwitchToWindow(titleSubstring, TimeSpan.FromSeconds(timeoutSeconds));
        return ok
            ? $"Switched context to window matching: '{titleSubstring}'"
            : $"Error: window matching '{titleSubstring}' not found after {timeoutSeconds}s.";
    }

    /// <summary>
    /// Closes the currently active window.
    /// </summary>
    public string CloseCurrentWindow()
    {
        _driver.CloseCurrentWindow();
        return "Current window closed.";
    }
}
