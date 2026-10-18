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
    /// Window title when the element lives in a non-main window (dialog / secondary window).
    /// Null for elements in the main window — keeps token cost at zero for the common case.
    /// </summary>
    [JsonPropertyName("win")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Window { get; init; }

    /// <summary>
    /// Formats the element as a ultra-compact token-saving single-line string.
    /// Example: [Button #SubmitButton "Submit"] or [Edit #FirstNameTextBox val="John"]
    /// Dialog elements are suffixed with {window title} so agents know a dialog is open.
    /// </summary>
    public override string ToString()
    {
        var idPart = !string.IsNullOrEmpty(AutomationId) ? $" #{AutomationId}" : "";
        var namePart = !string.IsNullOrEmpty(Name) ? $" \"{Name}\"" : "";
        var valPart = !string.IsNullOrEmpty(Value) ? $" val=\"{Value}\"" : "";
        var statePart = !string.IsNullOrEmpty(State) ? $" ({State})" : "";
        var winPart = !string.IsNullOrEmpty(Window) ? $" {{{Window}}}" : "";

        return $"[{Type}{idPart}{namePart}{valPart}{statePart}]{winPart}";
    }
}

