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

    /// <summary>
    /// Polls until an element with the given automationId appears or the timeout expires.
    /// Returns a human-readable result string suitable for MCP tool responses.
    /// </summary>
    /// <param name="automationId">Automation ID of the element to wait for.</param>
    /// <param name="timeoutSeconds">Max seconds to wait (default 10).</param>
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
    /// <param name="outputPath">File path where the PNG will be saved. Defaults to a timestamped file in the temp folder.</param>
    public string TakeScreenshot(string? outputPath = null)
    {
        outputPath ??= System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"autonex_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

        var ok = _driver.TakeScreenshot(outputPath);
        return ok
            ? $"Screenshot saved: {outputPath}"
            : $"Error: screenshot could not be taken.";
    }
}
