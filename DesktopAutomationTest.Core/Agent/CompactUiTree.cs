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
    /// <param name="maxDepth">Optional maximum traversal depth.</param>
    /// <returns>A flat list of compact interactive UI elements.</returns>
    public static List<CompactUiElement> ExtractInteractiveElements(AutomationElement? root, int maxDepth = -1)
    {
        var results = new List<CompactUiElement>();
        if (root == null)
        {
            return results;
        }

        Traverse(root, results, 0, maxDepth);

        // De-duplicate identical elements: UIA can expose the same logical control
        // more than once (e.g. a ComboBox and its inner edit part). Duplicates waste
        // tokens and can confuse agent-side matching.
        return results
            .GroupBy(e => $"{e.Type}|{e.AutomationId}|{e.Name}|{e.Value}|{e.State}")
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Intelligently summarizes repetitive consecutive elements (such as list items or rows)
    /// to drastically reduce token consumption on large grids or lists.
    /// </summary>
    public static List<CompactUiElement> SummarizeRepetitiveElements(IEnumerable<CompactUiElement> elements, int maxRepeated = 3)
    {
        var input = elements.ToList();
        var result = new List<CompactUiElement>();

        int i = 0;
        while (i < input.Count)
        {
            var current = input[i];
            int runLength = 1;
            while (i + runLength < input.Count &&
                   string.Equals(input[i + runLength].Type, current.Type, StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(current.Type, "ListItem", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current.Type, "MenuItem", StringComparison.OrdinalIgnoreCase) ||
                    (string.IsNullOrEmpty(current.AutomationId) && string.IsNullOrEmpty(input[i + runLength].AutomationId))))
            {
                runLength++;
            }

            if (runLength > maxRepeated)
            {
                for (int k = 0; k < maxRepeated; k++)
                {
                    result.Add(input[i + k]);
                }
                int omitted = runLength - maxRepeated;
                result.Add(new CompactUiElement
                {
                    Type = current.Type,
                    Name = $"... and {omitted} more {current.Type} items omitted ({runLength} total)"
                });
                i += runLength;
            }
            else
            {
                result.Add(current);
                i++;
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a token-minimal text dump of the current UI.
    /// Format:
    /// - [Edit #FirstNameTextBox]
    /// - [Button #SubmitButton "Submit"]
    /// </summary>
    public static string DumpAsText(AutomationElement? root, int maxDepth = -1, bool summarize = true)
    {
        var elements = ExtractInteractiveElements(root, maxDepth);
        if (summarize)
        {
            elements = SummarizeRepetitiveElements(elements);
        }

        if (elements.Count == 0)
        {
            return "[Empty or No Interactive Elements Found]";
        }

        return string.Join("\n", elements.Select(e => e.ToString()));
    }

    /// <summary>
    /// Generates a compact JSON array of interactive elements.
    /// </summary>
    public static string DumpAsJson(AutomationElement? root, int maxDepth = -1, bool summarize = true)
    {
        var elements = ExtractInteractiveElements(root, maxDepth);
        if (summarize)
        {
            elements = SummarizeRepetitiveElements(elements);
        }

        return SerializeElements(elements);
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

    private static void Traverse(AutomationElement element, List<CompactUiElement> list, int currentDepth = 0, int maxDepth = -1)
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

            if (maxDepth > 0 && currentDepth >= maxDepth)
            {
                return;
            }

            // Recurse into children
            foreach (var child in element.FindAllChildren())
            {
                Traverse(child, list, currentDepth + 1, maxDepth);
            }
        }
        catch
        {
            // Safeguard against transient COM / UIA exceptions during traversal
        }
    }
}

