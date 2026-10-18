using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Extracts and filters UI element hierarchies into token-minimized representations for LLM agents.
/// Strips out non-interactive layout containers, borders, and decorative elements.
/// </summary>
public static class CompactUiTree
{
    private static readonly HashSet<ControlType> InteractiveTypes = new()
    {
        ControlType.Button,
        ControlType.Edit,
        ControlType.CheckBox,
        ControlType.ComboBox,
        ControlType.RadioButton,
        ControlType.Hyperlink,
        ControlType.List,
        ControlType.ListItem,
        ControlType.MenuItem,
        ControlType.TabItem,
        ControlType.Text // Included if it has a non-empty name or automation ID (e.g. status labels, results)
    };

    /// <summary>
    /// Traverses the window hierarchy and extracts only interactive/semantic elements.
    /// </summary>
    /// <param name="root">The root window or container element.</param>
    /// <returns>A flat list of compact interactive UI elements.</returns>
    public static List<CompactUiElement> ExtractInteractiveElements(AutomationElement? root)
    {
        var results = new List<CompactUiElement>();
        if (root == null)
        {
            return results;
        }

        Traverse(root, results);

        // De-duplicate identical elements: UIA can expose the same logical control
        // more than once (e.g. a ComboBox and its inner edit part). Duplicates waste
        // tokens and can confuse agent-side matching.
        return results
            .GroupBy(e => $"{e.Type}|{e.AutomationId}|{e.Name}|{e.Value}|{e.State}")
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Generates a token-minimal text dump of the current UI.
    /// Format:
    /// - [Edit #FirstNameTextBox]
    /// - [Button #SubmitButton "Submit"]
    /// </summary>
    public static string DumpAsText(AutomationElement? root)
    {
        var elements = ExtractInteractiveElements(root);
        if (elements.Count == 0)
        {
            return "[Empty or No Interactive Elements Found]";
        }

        return string.Join("\n", elements.Select(e => e.ToString()));
    }

    /// <summary>
    /// Generates a compact JSON array of interactive elements.
    /// </summary>
    public static string DumpAsJson(AutomationElement? root)
    {
        return SerializeElements(ExtractInteractiveElements(root));
    }

    /// <summary>
    /// Serializes a pre-built element list (used when merging multiple windows).
    /// </summary>
    public static string SerializeElements(IEnumerable<CompactUiElement> elements)
    {
        return JsonSerializer.Serialize(elements, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static void Traverse(AutomationElement element, List<CompactUiElement> list)
    {
        try
        {
            var controlType = element.Properties.ControlType.ValueOrDefault;
            var isVisible = !element.Properties.IsOffscreen.ValueOrDefault;

            if (isVisible && InteractiveTypes.Contains(controlType))
            {
                var autoId = element.Properties.AutomationId.ValueOrDefault;
                var name = element.Properties.Name.ValueOrDefault;

                // For plain Text elements, only keep if they have an AutomationId or meaningful message
                if (controlType == ControlType.Text && string.IsNullOrWhiteSpace(autoId) && string.IsNullOrWhiteSpace(name))
                {
                    // Ignore empty decoration text
                }
                else
                {
                    string? value = null;
                    if (element.Patterns.Value.IsSupported)
                    {
                        value = element.Patterns.Value.Pattern.Value.ValueOrDefault;
                    }

                    string? state = null;
                    if (element.Patterns.Toggle.IsSupported)
                    {
                        var toggleState = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                        state = toggleState == ToggleState.On ? "checked" : "unchecked";
                    }
                    else if (element.Patterns.SelectionItem.IsSupported)
                    {
                        var isSelected = element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
                        if (isSelected) state = "selected";
                    }

                    if (!element.Properties.IsEnabled.ValueOrDefault)
                    {
                        state = state == null ? "disabled" : $"{state},disabled";
                    }

                    list.Add(new CompactUiElement
                    {
                        Type = controlType.ToString(),
                        AutomationId = string.IsNullOrEmpty(autoId) ? null : autoId,
                        Name = string.IsNullOrEmpty(name) ? null : name,
                        Value = string.IsNullOrEmpty(value) ? null : value,
                        State = state
                    });
                }
            }

            // Recurse into children
            foreach (var child in element.FindAllChildren())
            {
                Traverse(child, list);
            }
        }
        catch
        {
            // Safeguard against transient COM / UIA exceptions during traversal
        }
    }
}

