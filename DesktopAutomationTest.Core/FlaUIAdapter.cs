using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace DesktopAutomationTest.Core;

/// <summary>
/// FlaUI implementation of the IDriver interface.
/// </summary>
public class FlaUIAdapter : IDriver
{
    private Application? _application;
    private UIA3Automation _automation;
    private Window? _mainWindow;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlaUIAdapter"/> class.
    /// </summary>
    public FlaUIAdapter()
    {
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// Gets the underlying FlaUI application instance.
    /// </summary>
    public Application? Application => _application;

    /// <summary>
    /// Gets the main window of the application.
    /// </summary>
    public Window? MainWindow => _mainWindow;

    /// <summary>
    /// Initializes the driver with the specified application path.
    /// </summary>
    /// <param name="appPath">Path to the application executable.</param>
    /// <param name="arguments">Command line arguments for the application.</param>
    public void Initialize(string appPath, string? arguments = null)
    {
        Guard.AgainstNullOrEmpty(appPath, nameof(appPath));

        try
        {
            _application = Application.Launch(appPath, arguments ?? "");
            _mainWindow = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
            _application?.WaitWhileBusy();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize FlaUI adapter: {ex.Message}", ex);
        }
    }

    // Helper: Finds descendant element with polling timeout
    private AutomationElement? FindElement(string automationId, TimeSpan? timeout = null)
    {
        var findTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime <= findTimeout)
        {
            var element = _mainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(50);
        }

        return _mainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    /// <summary>
    /// Finds an element by its automation ID within the specified timeout.
    /// </summary>
    public bool TryFindByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            return element != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Clicks on an element identified by its automation ID.
    /// </summary>
    public bool ClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null || !element.Properties.IsEnabled)
                return false;

            element.Click();
            Thread.Sleep(50);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Types text into an element identified by its automation ID.
    /// </summary>
    public bool TypeTextByAutomationId(string automationId, string text, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));
        Guard.AgainstNull(text, nameof(text));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            element.Focus();
            Thread.Sleep(50);

            var textBox = element.AsTextBox();
            if (textBox != null && textBox.Patterns.Value.IsSupported)
            {
                textBox.Text = text;
                return true;
            }

            if (element.Patterns.Value.IsSupported)
            {
                element.Patterns.Value.Pattern.SetValue(text);
                return true;
            }

            Keyboard.Type(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the text of an element identified by its automation ID.
    /// </summary>
    public bool TryGetTextByAutomationId(string automationId, out string? text, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));
        text = null;

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var textBox = element.AsTextBox();
            if (textBox != null)
            {
                text = textBox.Text;
                return true;
            }

            text = element.Properties.Name;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Takes a screenshot of the main application window and saves it to disk.
    /// </summary>
    public bool TakeScreenshot(string filePath)
    {
        Guard.AgainstNullOrEmpty(filePath, nameof(filePath));

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (_mainWindow != null)
            {
                using var captured = Capture.Element(_mainWindow);
                captured.Bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                return true;
            }

            using var fullScreen = Capture.Screen();
            fullScreen.Bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current window bounds.
    /// </summary>
    public Rectangle GetWindowBounds()
    {
        if (_mainWindow == null)
            return Rectangle.Empty;

        var rect = _mainWindow.BoundingRectangle;
        return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    /// <summary>
    /// Waits for the application to be idle within the specified timeout.
    /// </summary>
    public bool WaitForInputIdle(TimeSpan timeout)
    {
        try
        {
            if (_application == null)
                return false;

            _application.WaitWhileBusy();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the checked state of a CheckBox element.
    /// </summary>
    public bool SetCheckboxByAutomationId(string automationId, bool isChecked, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var checkBox = element.AsCheckBox();
            if (checkBox == null)
                return false;

            checkBox.IsChecked = isChecked;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a CheckBox element is currently checked.
    /// </summary>
    public bool IsCheckboxChecked(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var checkBox = element.AsCheckBox();
            return checkBox?.IsChecked == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Selects an item from a ComboBox by its display text.
    /// </summary>
    public bool SelectComboBoxItemByText(string automationId, string text, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));
        Guard.AgainstNull(text, nameof(text));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var comboBox = element.AsComboBox();
            if (comboBox == null)
                return false;

            comboBox.Select(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Selects an item from a ComboBox by its zero-based index.
    /// </summary>
    public bool SelectComboBoxItemByIndex(string automationId, int index, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var comboBox = element.AsComboBox();
            if (comboBox == null)
                return false;

            comboBox.Select(index);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Retrieves the text of the currently selected item in a ComboBox.
    /// </summary>
    public string? GetSelectedComboBoxItem(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return null;

            var comboBox = element.AsComboBox();
            return comboBox?.SelectedItem?.Text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Selects a RadioButton element identified by its automation ID.
    /// </summary>
    public bool SelectRadioButtonByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var radioButton = element.AsRadioButton();
            if (radioButton == null)
                return false;

            radioButton.IsChecked = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a RadioButton element is currently selected.
    /// </summary>
    public bool IsRadioButtonSelected(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            var radioButton = element.AsRadioButton();
            return radioButton?.IsChecked == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ── [FEATURE-KEYBOARD-MOUSE] Advanced keyboard and mouse input ──────────────

    /// <summary>
    /// Double-clicks an element identified by its automation ID.
    /// Uses FlaUI Mouse.DoubleClick on the element's center point.
    /// </summary>
    public bool DoubleClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null || !element.Properties.IsEnabled)
                return false;

            element.DoubleClick();
            Thread.Sleep(50);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Right-clicks an element identified by its automation ID.
    /// Uses FlaUI Mouse.RightClick on the element's center point.
    /// </summary>
    public bool RightClickByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null || !element.Properties.IsEnabled)
                return false;

            element.RightClick();
            Thread.Sleep(50);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Moves the mouse pointer over an element (hover) without clicking.
    /// Uses FlaUI Mouse.MoveTo on the element's center point.
    /// </summary>
    public bool HoverOverByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            // Move mouse to the center of the element bounding rectangle
            var rect = element.BoundingRectangle;
            var centerX = rect.Left + rect.Width / 2;
            var centerY = rect.Top + rect.Height / 2;
            Mouse.MoveTo(centerX, centerY);
            Thread.Sleep(100);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Presses a single key by name (e.g. "Return", "Escape", "Tab", "F5").
    /// Parses the key name against <see cref="VirtualKeyShort"/> enum values.
    /// </summary>
    public bool PressKey(string key)
    {
        Guard.AgainstNullOrEmpty(key, nameof(key));

        try
        {
            if (Enum.TryParse<VirtualKeyShort>(key, ignoreCase: true, out var vk))
            {
                Keyboard.Press(vk);
                Thread.Sleep(30);
                Keyboard.Release(vk);
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Types a raw string as global keyboard input using FlaUI Keyboard.Type.
    /// Useful for sending text after an element already has focus, or for
    /// sending keyboard shortcuts (e.g. Ctrl+A, Ctrl+C).
    /// </summary>
    public bool SendKeys(string text)
    {
        Guard.AgainstNull(text, nameof(text));

        try
        {
            Keyboard.Type(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Drags the source element and drops it onto the target element.
    /// Computes center points of both bounding rectangles and uses
    /// FlaUI Mouse.DragAndDrop for the operation.
    /// </summary>
    public bool DragAndDrop(string sourceAutomationId, string targetAutomationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(sourceAutomationId, nameof(sourceAutomationId));
        Guard.AgainstNullOrEmpty(targetAutomationId, nameof(targetAutomationId));

        try
        {
            var source = FindElement(sourceAutomationId, timeout);
            var target = FindElement(targetAutomationId, timeout);

            if (source == null || target == null)
                return false;

            var srcRect = source.BoundingRectangle;
            var tgtRect = target.BoundingRectangle;

            var srcCenter = new System.Drawing.Point(
                srcRect.Left + srcRect.Width / 2,
                srcRect.Top  + srcRect.Height / 2);

            var tgtCenter = new System.Drawing.Point(
                tgtRect.Left + tgtRect.Width / 2,
                tgtRect.Top  + tgtRect.Height / 2);

            Mouse.Drag(srcCenter, tgtCenter);
            Thread.Sleep(100);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a compact, token-minimized representation of the interactive UI tree for LLM agents.
    /// </summary>
    public string GetCompactUiTree(string format = "text")
    {
        if (_mainWindow == null)
        {
            return "[No Main Window Available]";
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Agent.CompactUiTree.DumpAsJson(_mainWindow);
        }

        return Agent.CompactUiTree.DumpAsText(_mainWindow);
    }

    /// <summary>
    /// Returns titles of all open top-level windows belonging to the application process.
    /// </summary>
    public IReadOnlyList<string> GetOpenWindowTitles()
    {
        if (_application == null) return Array.Empty<string>();

        var windows = _application.GetAllTopLevelWindows(_automation);
        var titles = new List<string>();
        foreach (var w in windows)
        {
            try
            {
                var title = w.Title;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titles.Add(title);
                }
            }
            catch
            {
                // Ignore transient window errors
            }
        }
        return titles;
    }

    /// <summary>
    /// Switches the active automation context to a window matching the specified title (exact or substring).
    /// </summary>
    public bool SwitchToWindow(string titleSubstring, TimeSpan? timeout = null)
    {
        if (_application == null) return false;
        var limit = timeout ?? TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < limit)
        {
            var windows = _application.GetAllTopLevelWindows(_automation);
            foreach (var w in windows)
            {
                try
                {
                    if (w.Title?.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _mainWindow = w;
                        try { w.Focus(); } catch { }
                        return true;
                    }
                }
                catch
                {
                    // Window might be transitioning
                }
            }
            Thread.Sleep(200);
        }

        return false;
    }

    /// <summary>
    /// Closes the currently active window.
    /// </summary>
    public void CloseCurrentWindow()
    {
        try
        {
            _mainWindow?.Close();
        }
        catch
        {
            // Ignore
        }
    }

    public IReadOnlyList<(string Title, int ProcessId)> ListAppWindows()
    {
        return GetOpenWindowTitles()
            .Select(t => (t, _application?.ProcessId ?? 0))
            .ToList();
    }

    public bool SwitchWindow(string titleContains)
    {
        return SwitchToWindow(titleContains);
    }

    public void AttachToProcess(int processId)
    {
        _application = FlaUI.Core.Application.Attach(processId);
        _mainWindow = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
    }

    public void AttachToProcessByName(string processName)
    {
        var procs = System.Diagnostics.Process.GetProcessesByName(processName);
        if (procs.Length == 0)
            throw new ArgumentException($"Process '{processName}' not found.");

        _application = FlaUI.Core.Application.Attach(procs[0].Id);
        _mainWindow = _application.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
    }

    public byte[]? CaptureScreenshotBytes()
    {
        try
        {
            if (_mainWindow == null) return null;
            using var img = FlaUI.Core.Capturing.Capture.Element(_mainWindow);
            using var ms = new MemoryStream();
            img.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public bool IsApplicationAlive()
    {
        if (_application == null || _disposed) return false;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(_application.ProcessId);
            return !proc.HasExited && _mainWindow != null;
        }
        catch
        {
            return false;
        }
    }

    public bool Scroll(string direction = "down", string? automationId = null)
    {
        try
        {
            var target = !string.IsNullOrEmpty(automationId) ? FindElement(automationId) : _mainWindow;
            if (target == null) return false;

            if (target.Patterns.Scroll.IsSupported)
            {
                var pattern = target.Patterns.Scroll.Pattern;
                var (h, v) = direction.ToLowerInvariant() switch
                {
                    "up" => (ScrollAmount.NoAmount, ScrollAmount.SmallDecrement),
                    "down" => (ScrollAmount.NoAmount, ScrollAmount.SmallIncrement),
                    "left" => (ScrollAmount.SmallDecrement, ScrollAmount.NoAmount),
                    "right" => (ScrollAmount.SmallIncrement, ScrollAmount.NoAmount),
                    _ => (ScrollAmount.NoAmount, ScrollAmount.SmallIncrement)
                };
                pattern.Scroll(h, v);
                return true;
            }

            target.Focus();
            if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
            {
                Mouse.Scroll(1);
                return true;
            }
            if (direction.Equals("down", StringComparison.OrdinalIgnoreCase))
            {
                Mouse.Scroll(-1);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool ScrollIntoView(string targetAutomationId)
    {
        Guard.AgainstNullOrEmpty(targetAutomationId, nameof(targetAutomationId));

        try
        {
            var target = FindElement(targetAutomationId);
            if (target == null) return false;

            if (target.Patterns.ScrollItem.IsSupported)
            {
                target.Patterns.ScrollItem.Pattern.ScrollIntoView();
                return true;
            }

            return target.Properties.IsOffscreen.ValueOrDefault == false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _application?.Close();
                _automation?.Dispose();
            }

            _disposed = true;
        }
    }
}
