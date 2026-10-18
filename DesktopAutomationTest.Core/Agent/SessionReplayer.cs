using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Replays a previously recorded <see cref="SessionRecord"/> against a live application.
///
/// Key design decisions:
/// - Zero-token replay: reads JSON, calls driver directly — no LLM needed
/// - Self-healing: uses FuzzyLocator to tolerate minor ID renames between recordings
/// - Returns a compact <see cref="ReplayResult"/> with per-step outcomes
///
/// Usage:
/// <code>
/// var replayer = new SessionReplayer(driver);
/// var result   = replayer.ReplayFile("./sessions/login.json");
/// Console.WriteLine(result.ToSummary());
/// </code>
/// </summary>
public sealed class SessionReplayer
{
    private readonly IDriver _driver;

    public SessionReplayer(IDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a session JSON file and replays all steps.
    /// </summary>
    public ReplayResult ReplayFile(string filePath)
    {
        if (!File.Exists(filePath))
            return ReplayResult.Error($"Session file not found: {filePath}");

        SessionRecord? session;
        try
        {
            var json = File.ReadAllText(filePath);
            session  = JsonSerializer.Deserialize<SessionRecord>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            return ReplayResult.Error($"Failed to parse session file: {ex.Message}");
        }

        if (session is null)
            return ReplayResult.Error("Session file deserialized to null.");

        return Replay(session);
    }

    /// <summary>
    /// Replays a <see cref="SessionRecord"/> loaded directly (e.g. from MCP tool call).
    /// Uses FuzzyLocator for self-healing: resolves IDs against the live compact UI tree.
    /// </summary>
    public ReplayResult Replay(SessionRecord session)
    {
        var stepResults = new List<ReplayStepResult>(session.Steps.Count);

        // Get the live UI tree once for ID healing
        List<CompactUiElement> liveTree = new();
        try
        {
            var rawJson = _driver.GetCompactUiTree("json");
            if (!string.IsNullOrEmpty(rawJson) && rawJson.StartsWith("["))
            {
                liveTree = JsonSerializer.Deserialize<List<CompactUiElement>>(rawJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
        }
        catch
        {
            // Fallback to empty if parse fails
        }

        foreach (var step in session.Steps)
        {
            // Resolve ID via FuzzyLocator (self-healing)
            string? resolvedId = step.AutomationId;
            if (step.AutomationId is not null && liveTree.Count > 0)
            {
                var candidate = FuzzyLocator.ResolveBestMatch(step.AutomationId, liveTree);
                if (candidate is not null && candidate != step.AutomationId)
                    resolvedId = candidate;
            }

            var (success, message) = ExecuteStep(step, resolvedId);

            stepResults.Add(new ReplayStepResult
            {
                Index        = step.Index,
                Action       = step.Action,
                AutomationId = step.AutomationId,
                ResolvedId   = resolvedId != step.AutomationId ? resolvedId : null,
                Value        = step.Value,
                Success      = success,
                Message      = message
            });

            // Stop on first failure (fail-fast — agent should re-inspect and re-plan)
            if (!success) break;
        }

        var allPassed = stepResults.TrueForAll(s => s.Success);
        return new ReplayResult
        {
            SessionId   = session.SessionId,
            Label       = session.Label,
            TotalSteps  = session.Steps.Count,
            StepResults = stepResults,
            Success     = allPassed
        };
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private (bool success, string message) ExecuteStep(SessionStep step, string? resolvedId)
    {
        var id  = resolvedId ?? step.AutomationId;
        var val = step.Value;

        try
        {
            return step.Action.ToLowerInvariant() switch
            {
                "click"        => (_driver.ClickByAutomationId(id!),                   $"Clicked '{id}'"),
                "double_click" => (_driver.DoubleClickByAutomationId(id!),             $"Double-clicked '{id}'"),
                "right_click"  => (_driver.RightClickByAutomationId(id!),              $"Right-clicked '{id}'"),
                "hover"        => (_driver.HoverOverByAutomationId(id!),               $"Hovered '{id}'"),
                "type"         => (_driver.TypeTextByAutomationId(id!, val ?? ""),     $"Typed into '{id}'"),
                "select"       => (_driver.SelectComboBoxItemByText(id!, val ?? ""),   $"Selected '{val}' in '{id}'"),
                "check"        => (_driver.SetCheckboxByAutomationId(id!, true),       $"Checked '{id}'"),
                "uncheck"      => (_driver.SetCheckboxByAutomationId(id!, false),      $"Unchecked '{id}'"),
                "radio"        => (_driver.SelectRadioButtonByAutomationId(id!),       $"Selected radio '{id}'"),
                "press_key"    => (_driver.PressKey(val ?? ""),                        $"Pressed '{val}'"),
                "send_keys"    => (_driver.SendKeys(val ?? ""),                        $"Sent keys '{val}'"),
                "assert_text"  => AssertText(id!, val ?? ""),
                _              => (false, $"Unknown action: '{step.Action}'")
            };
        }
        catch (Exception ex)
        {
            return (false, $"Exception in step {step.Index}: {ex.Message}");
        }
    }

    private (bool, string) AssertText(string id, string expected)
    {
        if (_driver.TryGetTextByAutomationId(id, out var actual))
        {
            var ok = actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
            return (ok, ok
                ? $"Assert OK: '{id}' contains '{expected}'"
                : $"Assert FAILED: '{id}' = '{actual}', expected '{expected}'");
        }
        return (false, $"Assert FAILED: '{id}' not found");
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

/// <summary>Result of a single replayed step.</summary>
public sealed class ReplayStepResult
{
    public int     Index        { get; init; }
    public string  Action       { get; init; } = string.Empty;
    public string? AutomationId { get; init; }
    /// <summary>Non-null only when FuzzyLocator healed the ID.</summary>
    public string? ResolvedId   { get; init; }
    public string? Value        { get; init; }
    public bool    Success      { get; init; }
    public string? Message      { get; init; }
}

/// <summary>Full result of replaying a session.</summary>
public sealed class ReplayResult
{
    public string  SessionId   { get; init; } = string.Empty;
    public string? Label       { get; init; }
    public int     TotalSteps  { get; init; }
    public bool    Success     { get; init; }
    public List<ReplayStepResult> StepResults { get; init; } = new();

    public int PassedSteps => StepResults.Count(s => s.Success);
    public int FailedSteps => StepResults.Count(s => !s.Success);

    /// <summary>Returns a compact summary string suitable for MCP/CLI output.</summary>
    public string ToSummary()
    {
        var status   = Success ? "✓ PASSED" : "✗ FAILED";
        var healed   = StepResults.Count(s => s.ResolvedId != null);
        var healNote = healed > 0 ? $" | {healed} self-healed" : "";
        var lastFail = StepResults.LastOrDefault(s => !s.Success);
        var failDetail = lastFail is not null
            ? $"\n  ↳ Step {lastFail.Index}: {lastFail.Message}"
            : "";

        return $"{status} | Session '{SessionId}'" +
               (Label is not null ? $" [{Label}]" : "") +
               $" | {PassedSteps}/{TotalSteps} steps{healNote}{failDetail}";
    }

    /// <summary>Creates an error result (e.g. file not found).</summary>
    public static ReplayResult Error(string message) => new()
    {
        SessionId   = "error",
        Success     = false,
        TotalSteps  = 0,
        StepResults = new List<ReplayStepResult>
        {
            new() { Index = 0, Action = "load", Success = false, Message = message }
        }
    };
}

