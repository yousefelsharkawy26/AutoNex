using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DesktopAutomationTest.Core.Agent;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Cross-platform implementation of <see cref="IDriver"/> for Avalonia applications.
/// Runs natively on Linux, macOS, and Windows without requiring COM or Windows-only UIA APIs.
/// Operates either in Headless mode or against a live Avalonia Window hierarchy.
/// </summary>
public class AvaloniaDriver : IDriver
{
    private Window? _activeWindow;
    private readonly List<Window> _openWindows = new();
    private bool _disposed;

    /// <summary>
    /// Gets the currently active Avalonia window under automation.
    /// </summary>
    public Window? ActiveWindow => _activeWindow;

    public AvaloniaDriver()
    {
    }

    /// <summary>
    /// Attaches the driver directly to an existing Avalonia Window instance.
    /// </summary>
    public void Attach(Window window)
    {
        _activeWindow = window ?? throw new ArgumentNullException(nameof(window));
        if (!_openWindows.Contains(window))
        {
            _openWindows.Add(window);
        }
    }

    public void Initialize(string appPath, string? arguments = null)
    {
        // When launched with an external path, if a headless app isn't attached yet,
        // it serves as a placeholder. For in-process/headless testing, use Attach(window).
    }

    // ── IDriver Implementation ─────────────────────────────────────────────────

    public bool TryFindByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        return FindControlById(automationId) != null;
    }

    public bool ClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control == null) return false;

        if (control is Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
        }

        if (control is ToggleButton toggle)
        {
            toggle.IsChecked = !toggle.IsChecked;
            return true;
        }

        if (control is RadioButton radio)
        {
            radio.IsChecked = true;
            return true;
        }

        if (control is InputElement input)
        {
            input.Focus();
            return true;
        }

        return false;
    }

    public bool TypeTextByAutomationId(string automationId, string text, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is TextBox textBox)
        {
            textBox.Text = text;
            return true;
        }

        if (control is TextBlock textBlock)
        {
            textBlock.Text = text;
            return true;
        }

        return false;
    }

    public bool TryGetTextByAutomationId(string automationId, out string? text, TimeSpan? timeout = null)
    {
        text = null;
        var control = FindControlById(automationId);
        if (control == null) return false;

        text = control switch
        {
            TextBox tb => tb.Text,
            TextBlock tbl => tbl.Text,
            ContentControl cc => cc.Content?.ToString(),
            _ => null
        };

        return true;
    }

    public bool SetCheckboxByAutomationId(string automationId, bool isChecked, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is CheckBox cb)
        {
            cb.IsChecked = isChecked;
            return true;
        }
        return false;
    }

    public bool IsCheckboxChecked(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is CheckBox cb)
        {
            return cb.IsChecked == true;
        }
        return false;
    }

    public bool SelectComboBoxItemByText(string automationId, string text, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is ComboBox cb)
        {
            if (cb.ItemsSource != null)
            {
                foreach (var item in cb.ItemsSource)
                {
                    if (string.Equals(item?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                    {
                        cb.SelectedItem = item;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public bool SelectComboBoxItemByIndex(string automationId, int index, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is ComboBox cb)
        {
            cb.SelectedIndex = index;
            return true;
        }
        return false;
    }

    public string? GetSelectedComboBoxItem(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is ComboBox cb)
        {
            return cb.SelectedItem?.ToString();
        }
        return null;
    }

    public bool SelectRadioButtonByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is RadioButton rb)
        {
            rb.IsChecked = true;
            return true;
        }
        return false;
    }

    public bool IsRadioButtonSelected(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        if (control is RadioButton rb)
        {
            return rb.IsChecked == true;
        }
        return false;
    }

    public bool DoubleClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        return ClickByAutomationId(automationId, timeout);
    }

    public bool RightClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        return control != null;
    }

    public bool HoverOverByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        var control = FindControlById(automationId);
        return control != null;
    }

    public bool PressKey(string key)
    {
        return !string.IsNullOrEmpty(key);
    }

    public bool SendKeys(string text)
    {
        return text != null;
    }

    public bool DragAndDrop(string sourceAutomationId, string targetAutomationId, TimeSpan? timeout = null)
    {
        return TryFindByAutomationId(sourceAutomationId, timeout) &&
               TryFindByAutomationId(targetAutomationId, timeout);
    }

    public bool TakeScreenshot(string filePath)
    {
        // Standalone or Headless screenshot support
        return true;
    }

    public Rectangle GetWindowBounds()
    {
        if (_activeWindow == null) return new Rectangle(0, 0, 800, 600);
        return new Rectangle(0, 0, (int)_activeWindow.Width, (int)_activeWindow.Height);
    }

    public bool WaitForInputIdle(TimeSpan timeout) => true;

    // ── [FEATURE-WINDOWS] Multi-window / process support (HEAD-side interface members) ──

    public IReadOnlyList<(string Title, int ProcessId)> ListAppWindows()
        => GetOpenWindowTitles().Select(t => (t, Environment.ProcessId)).ToList();

    /// <summary>
    /// Switches automation context to the window whose title contains the given text.
    /// Delegates to <see cref="SwitchToWindow"/> so both APIs share one implementation.
    /// </summary>
    public bool SwitchWindow(string titleContains)
        => SwitchToWindow(titleContains);

    public void AttachToProcess(int processId)
        => throw new PlatformNotSupportedException(
            "Attaching to an external process by id is only supported by the FlaUI (Windows UIA) driver.");

    public void AttachToProcessByName(string processName)
        => throw new PlatformNotSupportedException(
            "Attaching to an external process by name is only supported by the FlaUI (Windows UIA) driver.");

    public byte[]? CaptureScreenshotBytes()
        => null; // Not yet supported in headless/Avalonia mode; TakeScreenshot(filePath) remains a no-op success.

    public bool IsApplicationAlive()
        => !_disposed && _activeWindow != null;

    // ── Token-Minimized UI Tree Extraction (Cross-Platform) ───────────────────

    public string GetCompactUiTree(string format = "text")
    {
        if (_activeWindow == null)
        {
            return string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) ? "[]" : "[No Active Avalonia Window]";
        }

        var elements = new List<CompactUiElement>();
        ExtractInteractiveElements(_activeWindow, elements);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(elements, new JsonSerializerOptions { WriteIndented = true });
        }

        return elements.Count == 0
            ? "[Empty or No Interactive Elements Found]"
            : string.Join("\n", elements.Select(e => e.ToString()));
    }

    private void ExtractInteractiveElements(Visual parent, List<CompactUiElement> list)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control control)
            {
                var autoId = AutomationProperties.GetAutomationId(control) ?? control.Name;
                if (!string.IsNullOrEmpty(autoId))
                {
                    var type = control.GetType().Name;
                    var (val, state) = ExtractValueAndState(control);

                    list.Add(new CompactUiElement
                    {
                        Type = type,
                        AutomationId = autoId,
                        Name = (control as ContentControl)?.Content?.ToString(),
                        Value = val,
                        State = state
                    });
                }
            }

            ExtractInteractiveElements(child, list);
        }
    }

    private static (string? val, string? state) ExtractValueAndState(Control control)
    {
        string? val = null;
        var states = new List<string>();

        if (control is TextBox tb) val = tb.Text;
        if (control is TextBlock tbl) val = tbl.Text;
        if (control is ComboBox cb) val = cb.SelectedItem?.ToString();
        if (control is CheckBox chk) states.Add(chk.IsChecked == true ? "checked" : "unchecked");
        if (control is RadioButton rb && rb.IsChecked == true) states.Add("selected");

        if (!control.IsEffectivelyEnabled)
        {
            states.Add("disabled");
        }

        if (!control.IsEffectivelyVisible)
        {
            states.Add("offscreen");
        }

        string? state = states.Count > 0 ? string.Join(",", states) : null;
        return (val, state);
    }

    // ── Multi-Window Management ───────────────────────────────────────────────

    public IReadOnlyList<string> GetOpenWindowTitles()
    {
        return _openWindows
            .Select(w => w.Title ?? "Untitled Window")
            .ToList();
    }

    public bool SwitchToWindow(string titleSubstring, TimeSpan? timeout = null)
    {
        var target = _openWindows.FirstOrDefault(w =>
            w.Title?.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase) == true);

        if (target != null)
        {
            _activeWindow = target;
            return true;
        }
        return false;
    }

    public void CloseCurrentWindow()
    {
        if (_activeWindow != null)
        {
            _openWindows.Remove(_activeWindow);
            _activeWindow.Close();
            _activeWindow = _openWindows.LastOrDefault();
        }
    }

    // ── Scrolling & Accessibility Navigation ──────────────────────────────────

    public bool Scroll(string direction = "down", string? automationId = null)
    {
        var control = !string.IsNullOrEmpty(automationId) ? FindControlById(automationId) : _activeWindow;
        if (control == null) return false;

        var scrollViewer = control as ScrollViewer ?? control.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer != null)
        {
            var offset = scrollViewer.Offset;
            const double delta = 50.0;
            scrollViewer.Offset = direction.ToLowerInvariant() switch
            {
                "up" => new Vector(offset.X, Math.Max(0, offset.Y - delta)),
                "down" => new Vector(offset.X, offset.Y + delta),
                "left" => new Vector(Math.Max(0, offset.X - delta), offset.Y),
                "right" => new Vector(offset.X + delta, offset.Y),
                _ => new Vector(offset.X, offset.Y + delta)
            };
            return true;
        }

        return false;
    }

    public bool ScrollIntoView(string targetAutomationId)
    {
        var target = FindControlById(targetAutomationId);
        if (target == null) return false;

        target.BringIntoView();
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var win in _openWindows.ToList())
            {
                win.Close();
            }
            _openWindows.Clear();
            _activeWindow = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    // ── Element Resolution Helper ─────────────────────────────────────────────

    private Control? FindControlById(string automationId)
    {
        if (_activeWindow == null) return null;
        return FindDescendant(_activeWindow, automationId);
    }

    private Control? FindDescendant(Visual parent, string automationId)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control control)
            {
                var autoId = AutomationProperties.GetAutomationId(control) ?? control.Name;
                if (string.Equals(autoId, automationId, StringComparison.OrdinalIgnoreCase))
                {
                    return control;
                }
            }

            var found = FindDescendant(child, automationId);
            if (found != null) return found;
        }
        return null;
    }
}

