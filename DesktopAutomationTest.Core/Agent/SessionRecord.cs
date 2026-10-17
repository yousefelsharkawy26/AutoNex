using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Immutable record of a single automation step captured during a recording session.
/// Stored inside <see cref="SessionRecord"/>.
/// </summary>
public sealed class SessionStep
{
    /// <summary>Zero-based step index.</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>UTC timestamp when the action was executed.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Action type: click, type, select, check, press_key, etc.</summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    /// <summary>Automation ID of the target element (if applicable).</summary>
    [JsonPropertyName("id")]
    public string? AutomationId { get; init; }

    /// <summary>Value used in the action (text typed, item selected, key pressed, etc.).</summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    /// <summary>Whether this step succeeded when originally recorded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Message returned by the driver/engine for this step.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Duration of the step in milliseconds.</summary>
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; init; }
}

/// <summary>
/// Complete record of an automation session: metadata + ordered list of steps.
/// Serialized as JSON for storage and later replay.
/// </summary>
public sealed class SessionRecord
{
    /// <summary>Unique ID for this session (auto-generated).</summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>UTC timestamp when the session was started.</summary>
    [JsonPropertyName("recorded_at")]
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Path to the application that was automated.</summary>
    [JsonPropertyName("app_path")]
    public string? AppPath { get; init; }

    /// <summary>Human-readable label for this session (optional).</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>AutoNex version that produced this recording.</summary>
    [JsonPropertyName("autonex_version")]
    public string AutoNexVersion { get; init; } = "1.0";

    /// <summary>Ordered list of automation steps.</summary>
    [JsonPropertyName("steps")]
    public List<SessionStep> Steps { get; init; } = new();

    // ── Computed summary ─────────────────────────────────────────────────────

    /// <summary>Total number of steps in this session.</summary>
    [JsonIgnore]
    public int TotalSteps => Steps.Count;

    /// <summary>Number of steps that succeeded.</summary>
    [JsonIgnore]
    public int PassedSteps => Steps.Count(s => s.Success);

    /// <summary>Returns a one-line summary string.</summary>
    public string ToSummary() =>
        $"Session '{SessionId}' | {TotalSteps} steps | {PassedSteps} passed | " +
        $"Recorded: {RecordedAt:yyyy-MM-dd HH:mm:ss}z";
}

