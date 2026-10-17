using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopAutomationTest.Core.Agent;

/// <summary>
/// Tracks UI state changes and produces minimal token delta diffs for LLM agents.
/// Eliminates the need to send full screen or full tree dumps after every action.
/// </summary>
public class AgentStateTracker
{
    private Dictionary<string, CompactUiElement> _previousSnapshot = new();

    /// <summary>
    /// Computes the delta diff between the current UI elements and the previous recorded state.
    /// Updates the internal state baseline with the current elements.
    /// </summary>
    /// <param name="currentElements">The interactive elements currently present on screen.</param>
    /// <returns>A compact summary of what changed (or null/empty if no change).</returns>
    public string ComputeDeltaAndRecord(IEnumerable<CompactUiElement> currentElements)
    {
        var currentMap = new Dictionary<string, CompactUiElement>();
        var changes = new List<string>();

        foreach (var el in currentElements)
        {
            var key = el.AutomationId ?? el.Name ?? el.Type;
            currentMap[key] = el;

            if (_previousSnapshot.TryGetValue(key, out var oldEl))
            {
                // Check if value changed
                if (oldEl.Value != el.Value && !string.IsNullOrEmpty(el.Value))
                {
                    changes.Add($"#{key} val: \"{oldEl.Value}\" -> \"{el.Value}\"");
                }
                // Check if state changed
                else if (oldEl.State != el.State && !string.IsNullOrEmpty(el.State))
                {
                    changes.Add($"#{key} state: {oldEl.State ?? "normal"} -> {el.State}");
                }
            }
            else
            {
                // Newly appeared element (e.g. error banner or popup)
                changes.Add($"+ {el}");
            }
        }

        // Check for disappeared elements
        foreach (var oldKey in _previousSnapshot.Keys)
        {
            if (!currentMap.ContainsKey(oldKey))
            {
                changes.Add($"- #{oldKey}");
            }
        }

        // Update baseline
        _previousSnapshot = currentMap;

        if (changes.Count == 0)
        {
            return "No UI state change.";
        }

        return "Delta: " + string.Join("; ", changes);
    }

    /// <summary>
    /// Resets the tracked baseline state.
    /// </summary>
    public void Reset()
    {
        _previousSnapshot.Clear();
    }
}
