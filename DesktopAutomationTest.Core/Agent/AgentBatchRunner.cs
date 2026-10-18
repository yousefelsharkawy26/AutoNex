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
            var (success, error, diagnostic) = ExecuteSingleAction(action);

            stepResults.Add(new ActionStepResult
            {
                StepIndex = i,
                Action = action.Action,
                TargetId = action.AutomationId,
                Success = success,
                ErrorMessage = error,
                DiagnosticDetails = diagnostic
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

    private (bool Success, string? Error, string? Diagnostic) ExecuteSingleAction(AgentAction action)
    {
        var type = action.Action.Trim().ToLowerInvariant();

        // [Self-Healing] Resolve the target id ONCE per action for EVERY locator-based
        // action (previously only click/type were healed). Falls back to the original
        // id when no confident fuzzy match exists.
        var targetId = ResolveTargetId(action.AutomationId);

        switch (type)
        {
            case "click":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for click action.", null);
                return _driver.ClickByAutomationId(targetId) 
                    ? (true, null, null) 
                    : (false, $"Failed to click element '{targetId}'. Element not found or disabled.", GetCandidateDiagnostic(targetId));

            case "type":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for type action.", null);
                return _driver.TypeTextByAutomationId(targetId, action.Value ?? string.Empty)
                    ? (true, null, null)
                    : (false, $"Failed to type text into '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "check":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for check action.", null);
                return _driver.SetCheckboxByAutomationId(targetId, true)
                    ? (true, null, null)
                    : (false, $"Failed to check element '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "uncheck":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for uncheck action.", null);
                return _driver.SetCheckboxByAutomationId(targetId, false)
                    ? (true, null, null)
                    : (false, $"Failed to uncheck element '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "select":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for select action.", null);
                if (string.IsNullOrEmpty(action.Value))
                    return (false, "Missing 'value' for select action.", null);

                // If ComboBox
                if (_driver.SelectComboBoxItemByText(targetId, action.Value))
                    return (true, null, null);

                // If RadioButton
                if (_driver.SelectRadioButtonByAutomationId(targetId))
                    return (true, null, null);

                return (false, $"Failed to select '{action.Value}' in element '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "double_click":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for double_click action.", null);
                return _driver.DoubleClickByAutomationId(targetId)
                    ? (true, null, null)
                    : (false, $"Failed to double-click '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "right_click":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for right_click action.", null);
                return _driver.RightClickByAutomationId(targetId)
                    ? (true, null, null)
                    : (false, $"Failed to right-click '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "hover":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for hover action.", null);
                return _driver.HoverOverByAutomationId(targetId)
                    ? (true, null, null)
                    : (false, $"Failed to hover over '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "press_key":
                if (string.IsNullOrEmpty(action.Key))
                    return (false, "Missing 'key' for press_key action.", null);
                return _driver.PressKey(action.Key)
                    ? (true, null, null)
                    : (false, $"Failed to press key '{action.Key}'.", null);

            case "send_keys":
                if (action.Value == null)
                    return (false, "Missing 'value' for send_keys action.", null);
                return _driver.SendKeys(action.Value)
                    ? (true, null, null)
                    : (false, "Failed to send keys.", null);

            case "wait":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for wait action.", null);
                var waitSeconds = action.TimeoutSeconds ?? 10;
                return _driver.TryFindByAutomationId(targetId, TimeSpan.FromSeconds(waitSeconds))
                    ? (true, null, null)
                    : (false, $"Timeout: element '{targetId}' not found after {waitSeconds}s.", GetCandidateDiagnostic(targetId));

            case "assert_text":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for assert_text action.", null);

                if (!_driver.TryGetTextByAutomationId(targetId, out var text))
                    return (false, $"Element '{targetId}' not found for text assertion.", GetCandidateDiagnostic(targetId));

                text ??= string.Empty;

                if (!string.IsNullOrEmpty(action.Contains))
                {
                    if (!text.Contains(action.Contains, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"Assertion failed for '{targetId}': Expected to contain '{action.Contains}', but was '{text}'.", null);
                    }
                }
                else if (action.Value != null)
                {
                    if (!string.Equals(text, action.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"Assertion failed for '{targetId}': Expected '{action.Value}', but was '{text}'.", null);
                    }
                }
                return (true, null, null);

            default:
                return (false, $"Unknown action type '{action.Action}'.", null);
        }
    }

    private string? GetCandidateDiagnostic(string? targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

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
                    var availableIds = candidates
                        .Where(c => !string.IsNullOrEmpty(c.AutomationId))
                        .Select(c => c.AutomationId)
                        .Take(5);

                    return $"Available IDs: {string.Join(", ", availableIds)}";
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
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

