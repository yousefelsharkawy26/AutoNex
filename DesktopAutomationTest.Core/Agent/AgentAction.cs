using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Definition of a single automation action sent by an agent.
/// </summary>
public record AgentAction
{
    /// <summary>
    /// Type of action: "click", "type", "select", "check", "uncheck", "double_click", "right_click", "hover", "press_key", "assert_text".
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Target element Automation ID.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; init; }

    /// <summary>
    /// Text or value to type / select / assert.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>
    /// Expected text substring for assertion (optional).
    /// </summary>
    [JsonPropertyName("contains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Contains { get; init; }

    /// <summary>
    /// Key name for keypress action (e.g. "Return", "Tab", "Escape").
    /// </summary>
    [JsonPropertyName("key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }
}

/// <summary>
/// Result of an individual action inside a batch execution.
/// </summary>
public record ActionStepResult
{
    [JsonPropertyName("step")]
    public int StepIndex { get; init; }

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetId { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Overall result of a batch action execution.
/// Designed to be concise and minimal in tokens when reported back to an Agent.
/// </summary>
public record AgentBatchResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("executed_steps")]
    public int ExecutedStepsCount { get; init; }

    [JsonPropertyName("total_steps")]
    public int TotalStepsCount { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("steps")]
    public List<ActionStepResult> Steps { get; init; } = new();

    /// <summary>
    /// Compact single-line summary to return to the LLM agent.
    /// Example: "SUCCESS: 4/4 steps executed" or "FAILED step 2 (click #Submit): element not found"
    /// </summary>
    public string ToCompactSummary()
    {
        if (Success)
        {
            return $"SUCCESS: {ExecutedStepsCount}/{TotalStepsCount} steps executed successfully.";
        }

        var failedStep = Steps.Find(s => !s.Success);
        var detail = failedStep != null 
            ? $"Step {failedStep.StepIndex + 1} ({failedStep.Action} #{failedStep.TargetId}): {failedStep.ErrorMessage}" 
            : Error;

        return $"FAILED at {detail} (Completed {ExecutedStepsCount}/{TotalStepsCount})";
    }
}
