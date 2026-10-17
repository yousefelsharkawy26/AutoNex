using System.Text.Json;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Executes a sequence of automation actions in a single batch.
/// Saves multiple LLM agent tool call round-trips and drastic token costs.
/// </summary>
public class AgentBatchRunner
{
    private readonly IDriver _driver;
    private readonly AgentStateTracker _stateTracker = new();

    public AgentBatchRunner(IDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>
    /// Executes a JSON string representing an array of actions.
    /// Example:
    /// [
    ///   {"action": "type", "id": "FirstNameTextBox", "value": "Ahmed"},
    ///   {"action": "click", "id": "SubmitButton"},
    ///   {"action": "assert_text", "id": "ResultTextBlock", "contains": "Ahmed"}
    /// ]
    /// </summary>
    public AgentBatchResult ExecuteJson(string jsonActions)
    {
        Guard.AgainstNullOrEmpty(jsonActions, nameof(jsonActions));

        try
        {
            var actions = JsonSerializer.Deserialize<List<AgentAction>>(jsonActions, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (actions == null || actions.Count == 0)
            {
                return new AgentBatchResult
                {
                    Success = false,
                    Error = "No actions found in payload."
                };
            }

            return Execute(actions);
        }
        catch (Exception ex)
        {
            return new AgentBatchResult
            {
                Success = false,
                Error = $"Failed to parse actions JSON: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Executes a list of structured agent actions sequentially.
    /// Halts on first failure and reports step index.
    /// </summary>
    public AgentBatchResult Execute(IEnumerable<AgentAction> actions)
    {
        var actionList = actions.ToList();
        var stepResults = new List<ActionStepResult>();

        for (int i = 0; i < actionList.Count; i++)
        {
            var action = actionList[i];
            var (success, error) = ExecuteSingleAction(action);

            stepResults.Add(new ActionStepResult
            {
                StepIndex = i,
                Action = action.Action,
                TargetId = action.AutomationId,
                Success = success,
                ErrorMessage = error
            });

            if (!success)
            {
                return new AgentBatchResult
                {
                    Success = false,
                    ExecutedStepsCount = i + 1,
                    TotalStepsCount = actionList.Count,
                    Error = error,
                    Steps = stepResults,
                    Delta = ComputeCurrentDelta()
                };
            }
        }

        return new AgentBatchResult
        {
            Success = true,
            ExecutedStepsCount = actionList.Count,
            TotalStepsCount = actionList.Count,
            Steps = stepResults,
            Delta = ComputeCurrentDelta()
        };
    }

    private string? ComputeCurrentDelta()
    {
        try
        {
            var rawJson = _driver.GetCompactUiTree("json");
            if (!string.IsNullOrEmpty(rawJson) && rawJson.StartsWith("["))
            {
                var elements = JsonSerializer.Deserialize<List<CompactUiElement>>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (elements != null)
                {
                    return _stateTracker.ComputeDeltaAndRecord(elements);
                }
            }
        }
        catch
        {
            // Ignore delta extraction failures
        }

        return null;
    }

    private (bool Success, string? Error) ExecuteSingleAction(AgentAction action)
    {
        var type = action.Action.Trim().ToLowerInvariant();
        var targetId = ResolveTargetId(action.AutomationId);

        switch (type)
        {
            case "click":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for click action.");
                return _driver.ClickByAutomationId(targetId) 
                    ? (true, null) 
                    : (false, $"Failed to click element '{targetId}'. Element not found or disabled.");

            case "type":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for type action.");
                return _driver.TypeTextByAutomationId(targetId, action.Value ?? string.Empty)
                    ? (true, null)
                    : (false, $"Failed to type text into '{targetId}'.");

            case "check":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for check action.");
                return _driver.SetCheckboxByAutomationId(action.AutomationId, true)
                    ? (true, null)
                    : (false, $"Failed to check element '{action.AutomationId}'.");

            case "uncheck":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for uncheck action.");
                return _driver.SetCheckboxByAutomationId(action.AutomationId, false)
                    ? (true, null)
                    : (false, $"Failed to uncheck element '{action.AutomationId}'.");

            case "select":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for select action.");
                if (string.IsNullOrEmpty(action.Value))
                    return (false, "Missing 'value' for select action.");

                // If ComboBox
                if (_driver.SelectComboBoxItemByText(action.AutomationId, action.Value))
                    return (true, null);

                // If RadioButton
                if (_driver.SelectRadioButtonByAutomationId(action.AutomationId))
                    return (true, null);

                return (false, $"Failed to select '{action.Value}' in element '{action.AutomationId}'.");

            case "double_click":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for double_click action.");
                return _driver.DoubleClickByAutomationId(action.AutomationId)
                    ? (true, null)
                    : (false, $"Failed to double-click '{action.AutomationId}'.");

            case "right_click":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for right_click action.");
                return _driver.RightClickByAutomationId(action.AutomationId)
                    ? (true, null)
                    : (false, $"Failed to right-click '{action.AutomationId}'.");

            case "hover":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for hover action.");
                return _driver.HoverOverByAutomationId(action.AutomationId)
                    ? (true, null)
                    : (false, $"Failed to hover over '{action.AutomationId}'.");

            case "press_key":
                if (string.IsNullOrEmpty(action.Key))
                    return (false, "Missing 'key' for press_key action.");
                return _driver.PressKey(action.Key)
                    ? (true, null)
                    : (false, $"Failed to press key '{action.Key}'.");

            case "send_keys":
                if (action.Value == null)
                    return (false, "Missing 'value' for send_keys action.");
                return _driver.SendKeys(action.Value)
                    ? (true, null)
                    : (false, "Failed to send keys.");

            case "assert_text":
                if (string.IsNullOrEmpty(action.AutomationId))
                    return (false, "Missing 'id' for assert_text action.");

                if (!_driver.TryGetTextByAutomationId(action.AutomationId, out var text))
                    return (false, $"Element '{action.AutomationId}' not found for text assertion.");

                text ??= string.Empty;

                if (!string.IsNullOrEmpty(action.Contains))
                {
                    if (!text.Contains(action.Contains, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"Assertion failed for '{action.AutomationId}': Expected to contain '{action.Contains}', but was '{text}'.");
                    }
                }
                else if (action.Value != null)
                {
                    if (!string.Equals(text, action.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"Assertion failed for '{action.AutomationId}': Expected '{action.Value}', but was '{text}'.");
                    }
                }
                return (true, null);

            default:
                return (false, $"Unknown action type '{action.Action}'.");
        }
    }

    private string? ResolveTargetId(string? originalId)
    {
        if (string.IsNullOrEmpty(originalId))
        {
            return originalId;
        }

        // Direct test if element exists
        if (_driver.TryFindByAutomationId(originalId))
        {
            return originalId;
        }

        // Self-healing: parse candidates from CompactUiTree if available
        try
        {
            var rawJson = _driver.GetCompactUiTree("json");
            if (!string.IsNullOrEmpty(rawJson) && rawJson.StartsWith("["))
            {
                var candidates = JsonSerializer.Deserialize<List<CompactUiElement>>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (candidates != null && candidates.Count > 0)
                {
                    var resolved = FuzzyLocator.ResolveBestMatch(originalId, candidates);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        return resolved;
                    }
                }
            }
        }
        catch
        {
            // Fallback to original
        }

        return originalId;
    }
}

