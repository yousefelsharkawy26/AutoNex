using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Records every automation action executed through an <see cref="IDriver"/> into a
/// <see cref="SessionRecord"/> that can be saved as JSON and replayed later.
///
/// Usage:
/// <code>
/// var recorder = new SessionRecorder(driver, appPath: "MyApp.exe", label: "login flow");
/// recorder.RecordStep("type",  "UsernameTextBox", "admin");
/// recorder.RecordStep("click", "LoginButton");
/// recorder.Save("./sessions/login.json");
/// </code>
/// </summary>
public sealed class SessionRecorder
{
    private readonly IDriver _driver;
    private int _stepIndex;

    /// <summary>The accumulated session data being built up by this recorder.</summary>
    public SessionRecord Session { get; }

    public SessionRecorder(IDriver driver, string? appPath = null, string? label = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        Session = new SessionRecord
        {
            AppPath = appPath,
            Label   = label,
            RecordedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Executes <paramref name="action"/> via the driver and captures the result as a step.
    /// Returns the step result (success + message).
    /// </summary>
    public (bool Success, string Message) RecordStep(string action, string? automationId = null, string? value = null)
    {
        var sw = Stopwatch.StartNew();
        var (success, message) = ExecuteAction(action, automationId, value);
        sw.Stop();

        var step = new SessionStep
        {
            Index        = _stepIndex++,
            Timestamp    = DateTimeOffset.UtcNow,
            Action       = action,
            AutomationId = automationId,
            Value        = value,
            Success      = success,
            Message      = message,
            DurationMs   = sw.ElapsedMilliseconds
        };

        Session.Steps.Add(step);
        return (success, message);
    }

    /// <summary>
    /// Serializes the recorded session to a JSON file.
    /// Creates parent directories automatically.
    /// </summary>
    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json    = JsonSerializer.Serialize(Session, options);
        File.WriteAllText(filePath, json);
    }

    // ── Internal action executor ──────────────────────────────────────────────

    private (bool, string) ExecuteAction(string action, string? id, string? value)
    {
        try
        {
            return action.ToLowerInvariant() switch
            {
                "click"        => (_driver.ClickByAutomationId(id!),                   $"Clicked '{id}'"),
                "double_click" => (_driver.DoubleClickByAutomationId(id!),             $"Double-clicked '{id}'"),
                "right_click"  => (_driver.RightClickByAutomationId(id!),              $"Right-clicked '{id}'"),
                "hover"        => (_driver.HoverOverByAutomationId(id!),               $"Hovered '{id}'"),
                "type"         => (_driver.TypeTextByAutomationId(id!, value ?? ""),   $"Typed into '{id}'"),
                "select"       => (_driver.SelectComboBoxItemByText(id!, value ?? ""), $"Selected '{value}' in '{id}'"),
                "check"        => (_driver.SetCheckboxByAutomationId(id!, true),       $"Checked '{id}'"),
                "uncheck"      => (_driver.SetCheckboxByAutomationId(id!, false),      $"Unchecked '{id}'"),
                "radio"        => (_driver.SelectRadioButtonByAutomationId(id!),       $"Selected radio '{id}'"),
                "press_key"    => (_driver.PressKey(value ?? ""),                      $"Pressed key '{value}'"),
                "send_keys"    => (_driver.SendKeys(value ?? ""),                      $"Sent keys '{value}'"),
                "assert_text"  => AssertText(id!, value ?? ""),
                _              => (false, $"Unknown action: '{action}'")
            };
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    private (bool, string) AssertText(string id, string expected)
    {
        if (_driver.TryGetTextByAutomationId(id, out var actual))
        {
            var ok = actual?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
            return (ok, ok
                ? $"Assert passed: '{id}' contains '{expected}'"
                : $"Assert FAILED: '{id}' = '{actual}', expected contains '{expected}'");
        }
        return (false, $"Assert FAILED: element '{id}' not found");
    }
}

