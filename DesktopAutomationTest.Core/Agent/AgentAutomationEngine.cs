using System;
using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Structured result of a single agent operation: textual payload plus an optional
/// image (PNG bytes) for tools that can return visual content over MCP.
/// </summary>
public record AgentOperationResult
{
    public string Text { get; init; } = string.Empty;

    /// <summary>PNG bytes when the caller requested image content; null otherwise.</summary>
    public byte[]? ImagePng { get; init; }

    public bool IsError { get; init; }

    public static AgentOperationResult Ok(string text) => new() { Text = text };
    public static AgentOperationResult Fail(string text) => new() { Text = text, IsError = true };
    public static AgentOperationResult WithImage(string text, byte[] png) => new() { Text = text, ImagePng = png };
}

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
    /// Supports: click, type, check, uncheck, select, hover, double_click, right_click, press_key, send_keys, wait, assert_text.
    /// Auto-heals misspelled IDs for ALL locator-based actions and returns a single-line summary with UI state delta.
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
    /// Applies fuzzy self-healing before waiting so approximate ids still resolve.
    /// Returns a human-readable result string suitable for MCP tool responses.
    /// </summary>
    /// <param name="automationId">Automation ID of the element to wait for.</param>
    /// <param name="timeoutSeconds">Max seconds to wait (default 10).</param>
    public string WaitForElement(string automationId, int timeoutSeconds = 10)
    {
        var resolved = ResolveWithHealing(automationId);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var found = _driver.TryFindByAutomationId(resolved, timeout);
        if (found)
        {
            return resolved != automationId
                ? $"Element '{resolved}' (self-healed from '{automationId}') found within {timeoutSeconds}s."
                : $"Element '{resolved}' found within {timeoutSeconds}s.";
        }
        return $"Timeout: element '{automationId}' not found after {timeoutSeconds}s.";
    }

    /// <summary>
    /// Returns the text of the element with the given automationId (with self-healing).
    /// Returns a human-readable result string suitable for MCP tool responses.
    /// </summary>
    public string GetElementText(string automationId)
    {
        var resolved = ResolveWithHealing(automationId);
        if (_driver.TryGetTextByAutomationId(resolved, out var text))
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
        var result = CaptureScreenshot(outputPath, includeImage: false);
        return result.Text;
    }

    /// <summary>
    /// Captures a screenshot, optionally returning PNG bytes so MCP servers can ship
    /// base64 image content blocks back to vision-capable agents.
    /// </summary>
    /// <param name="outputPath">Optional file path to persist the PNG.</param>
    /// <param name="includeImage">When true, PNG bytes are attached to the result.</param>
    public AgentOperationResult CaptureScreenshot(string? outputPath = null, bool includeImage = false)
    {
        byte[]? pngBytes = null;
        if (includeImage)
        {
            pngBytes = _driver.CaptureScreenshotBytes();
            if (pngBytes == null)
            {
                return AgentOperationResult.Fail("Error: screenshot could not be taken.");
            }
        }

        string? savedPath = null;
        if (!string.IsNullOrEmpty(outputPath))
        {
            var ok = _driver.TakeScreenshot(outputPath!);
            if (!ok && pngBytes == null)
            {
                return AgentOperationResult.Fail("Error: screenshot could not be taken.");
            }
            if (ok)
            {
                savedPath = outputPath;
            }
        }
        else if (pngBytes != null)
        {
            // No explicit path requested but image captured — also persist to temp for traceability.
            savedPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"autonex_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            try
            {
                System.IO.File.WriteAllBytes(savedPath, pngBytes);
            }
            catch
            {
                savedPath = null; // Non-fatal: image is still returned inline.
            }
        }
        else
        {
            // Neither path nor image — default behaviour: save to temp.
            var fallback = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"autonex_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            var ok = _driver.TakeScreenshot(fallback);
            return ok
                ? AgentOperationResult.Ok($"Screenshot saved: {fallback}")
                : AgentOperationResult.Fail("Error: screenshot could not be taken.");
        }

        var message = savedPath != null
            ? $"Screenshot captured. Saved: {savedPath}"
            : "Screenshot captured.";
        return pngBytes != null
            ? AgentOperationResult.WithImage(message, pngBytes)
            : AgentOperationResult.Ok(message);
    }

    /// <summary>
    /// Lists all application windows (main + dialogs) with titles.
    /// </summary>
    public string ListWindows()
    {
        var windows = _driver.ListAppWindows();
        if (windows.Count == 0)
        {
            return "No windows found for the automated application.";
        }

        return string.Join("\n", windows.Select((w, i) => $"{i + 1}. \"{w.Title}\" (pid {w.ProcessId})"));
    }

    /// <summary>
    /// Switches automation context to the app window whose title contains the given text.
    /// </summary>
    public string SwitchWindow(string titleContains)
    {
        var ok = _driver.SwitchWindow(titleContains);
        return ok
            ? $"Switched active window to one matching '{titleContains}'."
            : $"Error: no application window matches '{titleContains}'.";
    }

    /// <summary>
    /// Attaches the driver to an already-running process (by pid or exe name) instead of launching a new instance.
    /// </summary>
    public string Attach(int? processId, string? processName)
    {
        if (processId.HasValue)
        {
            _driver.AttachToProcess(processId.Value);
            return $"Attached to process {processId.Value}.";
        }

        if (!string.IsNullOrEmpty(processName))
        {
            _driver.AttachToProcessByName(processName!);
            return $"Attached to running process '{processName}'.";
        }

        return "Error: either process_id or process_name is required to attach.";
    }

    /// <summary>
    /// Fuzzy self-healing for standalone (non-batch) tools: tries the exact id first,
    /// then resolves the closest match from the compact UI tree.
    /// </summary>
    private string ResolveWithHealing(string automationId)
    {
        try
        {
            if (_driver.TryFindByAutomationId(automationId, TimeSpan.FromMilliseconds(300)))
            {
                return automationId;
            }

            var rawJson = _driver.GetCompactUiTree("json");
            if (!string.IsNullOrEmpty(rawJson) && rawJson.StartsWith("["))
            {
                var candidates = JsonSerializer.Deserialize<System.Collections.Generic.List<CompactUiElement>>(
                    rawJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (candidates != null && candidates.Count > 0)
                {
                    var resolved = FuzzyLocator.ResolveBestMatch(automationId, candidates);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        return resolved!;
                    }
                }
            }
        }
        catch
        {
            // Fall through to original id.
        }

        return automationId;
    }
}
