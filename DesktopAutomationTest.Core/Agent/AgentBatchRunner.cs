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
    public AgentBatchResult ExecuteJson(string jsonActions, bool dryRun = false)
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

            return Execute(actions, dryRun);
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
    /// Supports dry-run validation to test plans before actual execution.
    /// Halts on first failure and reports step index.
    /// </summary>
    public AgentBatchResult Execute(IEnumerable<AgentAction> actions, bool dryRun = false)
    {
        var actionList = actions.ToList();
        var stepResults = new List<ActionStepResult>();

        for (int i = 0; i < actionList.Count; i++)
        {
            var action = actionList[i];
            var (success, error, diagnostic) = dryRun
                ? ValidateSingleActionDryRun(action)
                : ExecuteSingleAction(action);

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
                    Delta = dryRun ? "[Dry-run validation failed]" : ComputeCurrentDelta()
                };
            }
        }

        return new AgentBatchResult
        {
            Success = true,
            ExecutedStepsCount = actionList.Count,
            TotalStepsCount = actionList.Count,
            Steps = stepResults,
            Delta = dryRun ? "[Dry-run: all locators and actions validated successfully]" : ComputeCurrentDelta()
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
                // Optional: "clear": true empties the field before typing so agents
                // don't need a separate step (saves tokens and avoids appended text).
                // Accepted spellings: "clear" (canonical) and "clear_first" (alias).
                if ((action.Clear == true || action.ClearFirst == true) && !_driver.ClearByAutomationId(targetId))
                {
                    return (false, $"Failed to clear '{targetId}' before typing.", GetCandidateDiagnostic(targetId));
                }
                return _driver.TypeTextByAutomationId(targetId, action.Value ?? string.Empty)
                    ? (true, null, null)
                    : (false, $"Failed to type text into '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "paste":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for paste action.", null);
                if (action.Clear == true || action.ClearFirst == true)
                {
                    _driver.ClearByAutomationId(targetId);
                }
                return _driver.TypeTextByAutomationId(targetId, action.Value ?? string.Empty)
                    ? (true, null, null)
                    : (false, $"Failed to paste text into '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "clear":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for clear action.", null);
                return (_driver.ClearByAutomationId(targetId) || _driver.TypeTextByAutomationId(targetId, string.Empty))
                    ? (true, null, null)
                    : (false, $"Failed to clear element '{targetId}'.", GetCandidateDiagnostic(targetId));

            case "scroll":
                var scrollDirection = (action.Direction ?? action.Value ?? "down").Trim().ToLowerInvariant();
                if (scrollDirection is not ("up" or "down" or "left" or "right"))
                    return (false, $"Invalid scroll direction '{scrollDirection}'. Use up/down/left/right.", null);
                return _driver.Scroll(scrollDirection, targetId)
                    ? (true, null, null)
                    : (false, $"Failed to scroll {scrollDirection} on '{targetId ?? "<focused window>"}'.", GetCandidateDiagnostic(targetId));

            case "scroll_into_view":
                if (string.IsNullOrEmpty(targetId))
                    return (false, "Missing 'id' for scroll_into_view action.", null);
                return _driver.ScrollIntoView(targetId)
                    ? (true, null, null)
                    : (false, $"Failed to scroll '{targetId}' into view.", GetCandidateDiagnostic(targetId));

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

            case "assert":
            case "assert_text":
                if (string.IsNullOrEmpty(targetId))
                    return (false, $"Missing 'id' for {type} action.", null);

                if (!_driver.TryGetTextByAutomationId(targetId, out var text))
                    return (false, $"Element '{targetId}' not found for assertion.", GetCandidateDiagnostic(targetId));

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
                // Self-healing for action typos: suggest the closest known action
                // (e.g. "clik" -> "click") so the agent can fix it in one retry.
                var suggestion = FuzzyLocator.SuggestActionType(type);
                var hint = suggestion != null
                    ? $" Did you mean '{suggestion}'?"
                    : $" Valid actions: {string.Join(", ", FuzzyLocator.KnownActionTypes)}.";
                return (false, $"Unknown action type '{action.Action}'.{hint}", null);
        }
    }

    private (bool success, string? error, string? diagnostic) ValidateSingleActionDryRun(AgentAction action)
    {
        if (string.IsNullOrEmpty(action.Action))
        {
            return (false, "Missing required field 'action'.", null);
        }

        var type = action.Action.ToLowerInvariant();
        var validActions = new[] { "click", "type", "check", "uncheck", "select", "double_click", "right_click", "hover", "press_key", "send_keys", "wait", "assert", "assert_text", "scroll", "scroll_into_view", "clear", "paste" };

        if (!validActions.Contains(type))
        {
            var closest = validActions
                .Select(a => new { Action = a, Distance = FuzzyLocator.CalculateSimilarity(type, a) })
                .OrderByDescending(x => x.Distance)
                .FirstOrDefault();

            var suggestion = closest != null && closest.Distance > 0.4
                ? $" Did you mean '{closest.Action}'?"
                : "";

            return (false, $"Unknown action type '{action.Action}'.{suggestion}", null);
        }

        var requiresLocator = new[] { "click", "type", "check", "uncheck", "select", "double_click", "right_click", "hover", "assert", "assert_text", "scroll_into_view", "clear", "paste" };
        if (requiresLocator.Contains(type))
        {
            if (string.IsNullOrEmpty(action.AutomationId))
            {
                return (false, $"Action '{type}' requires an 'id'.", null);
            }

            var resolvedId = ResolveTargetId(action.AutomationId);
            if (!_driver.TryFindByAutomationId(resolvedId, TimeSpan.FromMilliseconds(500)))
            {
                return (false, $"[Dry-run] Element '{action.AutomationId}' not found on screen.", GetCandidateDiagnostic(action.AutomationId));
            }
        }

        return (true, null, null);
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

