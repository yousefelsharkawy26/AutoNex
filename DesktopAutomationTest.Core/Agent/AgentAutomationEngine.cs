using System;
using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Provides high-level convenience facade for AI Coding Agents.
/// Minimizes LLM tool overhead by exposing token-optimized single-point operations:
/// - Dump UI with minimal token footprint
/// - Execute multi-step action plans in 1 call with self-healing and delta tracking
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
}

