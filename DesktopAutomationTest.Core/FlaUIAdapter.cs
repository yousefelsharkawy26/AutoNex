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

    /// <summary>
    /// Returns true when the launched process is still running and a window handle exists.
    /// </summary>
    public bool IsApplicationAlive()
    {
        try
        {
            if (_application == null || _disposed)
                return false;

            var proc = _application.Process;
            if (proc == null)
                return false;

            proc.Refresh();
            return !proc.HasExited && _mainWindow != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enumerates every top-level window belonging to the launched application's process.
    /// Includes modal dialogs (MessageBox, Open/Save file dialogs, secondary windows) that
    /// are NOT descendants of the main window — without this, dialogs are unreachable.
    /// The main window is searched first so it always wins ties.
    /// </summary>
    private IEnumerable<AutomationElement> GetAppWindows()
    {
        if (_mainWindow != null)
        {
            yield return _mainWindow;
        }

        try
        {
            var pid = _application?.Process?.Id ?? -1;
            if (pid <= 0)
            {
                yield break;
            }

            var desktop = _automation.GetDesktop();
            var topLevelWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

            foreach (var win in topLevelWindows)
            {
                try
                {
                    if (win.Properties.ProcessId.ValueOrDefault == pid &&
                        win.Equals(_mainWindow) == false)
                    {
                        yield return win;
                    }
                }
                catch
                {
                    // Transient COM error on a single window — skip it.
                }
            }
        }
        catch
        {
            // Desktop enumeration failed — main window search above already ran.
        }
    }

    /// <summary>
    /// Finds an element by AutomationId across ALL application windows (main + dialogs).
    /// </summary>
    private AutomationElement? FindAcrossWindows(string automationId)
    {
        foreach (var window in GetAppWindows())
        {
            try
            {
                var element = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (element != null)
                {
                    return element;
                }
            }
            catch
            {
                // Window may have closed between enumeration and search — try next.
            }
        }

        return null;
    }

    // Helper: Finds descendant element with polling timeout (searches main window AND dialogs)
    private AutomationElement? FindElement(string automationId, TimeSpan? timeout = null)
    {
        if (!IsApplicationAlive())
        {
            throw new InvalidOperationException(
                "The target application is no longer running. Call launch_application again.");
        }

        var findTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime <= findTimeout)
        {
            var element = FindAcrossWindows(automationId);
            if (element != null)
            {
                return element;
            }
            Thread.Sleep(50);
        }

        return FindAcrossWindows(automationId);
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

            // Prefer the Value pattern: it returns live content for Edits, Text blocks,
            // ComboBoxes, etc. Reading only Properties.Name misses TextBox/TextBlock values.
            if (element.Patterns.Value.IsSupported)
            {
                var value = element.Patterns.Value.Pattern.Value.ValueOrDefault;
                if (!string.IsNullOrEmpty(value))
                {
                    text = value;
                    return true;
                }
            }

            var textBox = element.AsTextBox();
            if (textBox != null && !string.IsNullOrEmpty(textBox.Text))
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
    /// Clears the current text/value of an input element. Prefers the UIA
    /// ValuePattern (instant, no keystrokes), falls back to Ctrl+A + Delete.
    /// </summary>
    public bool ClearByAutomationId(string automationId, TimeSpan? timeout = null)
    {
        Guard.AgainstNullOrEmpty(automationId, nameof(automationId));

        try
        {
            var element = FindElement(automationId, timeout);
            if (element == null)
                return false;

            element.Focus();
            Thread.Sleep(50);

            if (element.Patterns.Value.IsSupported)
            {
                element.AsTextBox().Text = string.Empty;
                return true;
            }

            // Fallback: select-all then delete via keyboard.
            Keyboard.TypeSimultaneously(FlaUI.Core.VirtualKeyShort.CONTROL, FlaUI.Core.VirtualKeyShort.KEY_A);
            Keyboard.Type(FlaUI.Core.VirtualKeyShort.DELETE);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Scrolls using the UIA ScrollPattern when supported; otherwise moves the
    /// mouse over the target and sends wheel ticks (amount = number of ticks).
    /// </summary>
    public bool Scroll(string? automationId, string direction, int amount = 3)
    {
        try
        {
            AutomationElement? element = null;
            if (!string.IsNullOrEmpty(automationId))
            {
                element = FindElement(automationId);
                if (element == null)
                    return false;
            }

            var vertical = direction is "up" or "down";
            var forward = direction is "down" or "right";

            // Preferred: UIA ScrollPattern (no focus/mouse dependency).
            var scrollTarget = element ?? (_mainWindow as AutomationElement);
            if (scrollTarget != null && scrollTarget.Patterns.Scroll.IsSupported)
            {
                var pattern = scrollTarget.Patterns.Scroll.Pattern;
                for (var i = 0; i < Math.Max(1, amount); i++)
                {
                    if (vertical)
                    {
                        
                        pattern.ScrollVertical(forward ? ScrollAmount.SmallScroll : -ScrollAmount.SmallScroll);
                        
                    }
                    else
                    {
                        pattern.ScrollHorizontal(forward ? ScrollAmount.SmallScroll : -ScrollAmount.SmallScroll);
                    }
                }
                return true;
            }

            // Fallback: mouse wheel over the element (or window center).
            var rect = (element ?? _mainWindow)?.BoundingRectangle ?? System.Drawing.Rectangle.Empty;
            if (rect.Width == 0 && rect.Height == 0)
                return false;

            Mouse.Position = new System.Drawing.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            Thread.Sleep(50);
            var delta = (forward ? -1 : 1) * Math.Max(1, amount) * 120;
            Mouse.Scroll(delta);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a compact, token-minimized representation of the interactive UI tree for LLM agents.
    /// Includes the main window AND any open dialogs/secondary windows of the app process,
    /// each labelled with its window title so agents can target them via switch_window.
    /// </summary>
    public string GetCompactUiTree(string format = "text")
    {
        if (_mainWindow == null)
        {
            return "[No Main Window Available]";
        }

        var asJson = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase);
        var elements = new List<Agent.CompactUiElement>();

        foreach (var window in GetAppWindows())
        {
            try
            {
                var windowElements = Agent.CompactUiTree.ExtractInteractiveElements(window);

                // Label elements that live in a non-main window so agents know a
                // dialog is open (token cost: one short suffix per element).
                if (!window.Equals(_mainWindow))
                {
                    var title = SafeWindowTitle(window);
                    foreach (var e in windowElements)
                    {
                        elements.Add(e with { Window = title });
                    }
                }
                else
                {
                    elements.AddRange(windowElements);
                }
            }
            catch
            {
                // Window closed mid-enumeration — skip it.
            }
        }

        if (asJson)
        {
            return Agent.CompactUiTree.SerializeElements(elements);
        }

        if (elements.Count == 0)
        {
            return "[Empty or No Interactive Elements Found]";
        }

        return string.Join("\n", elements.Select(e => e.ToString()));
    }

    private static string SafeWindowTitle(AutomationElement window)
    {
        try
        {
            var name = window.Properties.Name.ValueOrDefault;
            return string.IsNullOrWhiteSpace(name) ? "Dialog" : name;
        }
        catch
        {
            return "Dialog";
        }
    }

    /// <summary>
    /// Lists all top-level windows of the application (title + class + automationId hints).
    /// </summary>
    public IReadOnlyList<(string Title, int ProcessId)> ListAppWindows()
    {
        var result = new List<(string, int)>();
        try
        {
            var desktop = _automation.GetDesktop();
            var pid = _application?.Process?.Id ?? -1;
            var topLevelWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

            foreach (var win in topLevelWindows)
            {
                try
                {
                    var procId = win.Properties.ProcessId.ValueOrDefault;
                    if (procId == pid)
                    {
                        result.Add((SafeWindowTitle(win), procId));
                    }
                }
                catch
                {
                    // Skip unreadable window.
                }
            }
        }
        catch
        {
            // Enumeration failed; caller gets an empty list.
        }

        return result;
    }

    /// <summary>
    /// Switches the driver's active main-window context to the app window whose title
    /// contains the given text (case-insensitive). Subsequent element lookups prefer
    /// this window while still falling back to all app windows.
    /// </summary>
    public bool SwitchWindow(string titleContains)
    {
        Guard.AgainstNullOrEmpty(titleContains, nameof(titleContains));

        try
        {
            var windows = _automation.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window));

            var pid = _application?.Process?.Id ?? -1;

            foreach (var win in windows)
            {
                try
                {
                    if (win.Properties.ProcessId.ValueOrDefault != pid)
                        continue;

                    var title = SafeWindowTitle(win);
                    if (title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
                    {
                        _mainWindow = win.AsWindow();
                        _mainWindow.SetForeground();
                        return true;
                    }
                }
                catch
                {
                    // Keep scanning.
                }
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Attaches the driver to an already-running process by its OS process id,
    /// reusing its main window instead of launching a new copy of the app.
    /// </summary>
    public void AttachToProcess(int processId)
    {
        try
        {
            var app = Application.Attach(processId);
            _application = app;
            _mainWindow = app.GetMainWindow(_automation, TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to attach to process {processId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attaches the driver to an already-running process by executable name
    /// (without extension). Fails when zero or multiple matches exist unless
    /// the newest process is requested.
    /// </summary>
    public void AttachToProcessByName(string processName)
    {
        Guard.AgainstNullOrEmpty(processName, nameof(processName));

        try
        {
            var candidates = System.Diagnostics.Process
                .GetProcessesByName(processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? processName[..^4]
                    : processName);

            if (candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No running process named '{processName}' was found.");
            }

            // Prefer the most recently started instance.
            var proc = candidates.OrderByDescending(p =>
            {
                try { return p.StartTime; } catch { return DateTime.MinValue; }
            }).First();

            AttachToProcess(proc.Id);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to attach to process '{processName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Captures the current screen/window and returns raw PNG bytes (for base64 transport to agents).
    /// </summary>
    public byte[]? CaptureScreenshotBytes()
    {
        try
        {
            using var memory = new MemoryStream();
            if (_mainWindow != null)
            {
                using var captured = Capture.Element(_mainWindow);
                captured.Bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            }
            else
            {
                using var fullScreen = Capture.Screen();
                fullScreen.Bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            }

            return memory.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
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
