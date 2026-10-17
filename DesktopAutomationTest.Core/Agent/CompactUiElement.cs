using System.Text.Json.Serialization;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// A highly compressed representation of a UI element designed to minimize LLM token usage.
/// Only interactive and semantically meaningful elements are kept.
/// </summary>
public record CompactUiElement
{
    /// <summary>
    /// Type/ControlType of the element (e.g. "Button", "Edit", "CheckBox", "ComboBox", "Text").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Automation ID of the element (primary locator for automation).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; init; }

    /// <summary>
    /// Display name or label of the element.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>
    /// Current value/text (e.g. content of a TextBox or selected ComboBox item).
    /// </summary>
    [JsonPropertyName("val")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>
    /// State flags (e.g. "checked", "disabled", "selected"). Null if standard/default.
    /// </summary>
    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; init; }

    /// <summary>
    /// Formats the element as a ultra-compact token-saving single-line string.
    /// Example: [Button #SubmitButton "Submit"] or [Edit #FirstNameTextBox val="John"]
    /// </summary>
    public override string ToString()
    {
        var idPart = !string.IsNullOrEmpty(AutomationId) ? $" #{AutomationId}" : "";
        var namePart = !string.IsNullOrEmpty(Name) ? $" \"{Name}\"" : "";
        var valPart = !string.IsNullOrEmpty(Value) ? $" val=\"{Value}\"" : "";
        var statePart = !string.IsNullOrEmpty(State) ? $" ({State})" : "";

        return $"[{Type}{idPart}{namePart}{valPart}{statePart}]";
    }
}

