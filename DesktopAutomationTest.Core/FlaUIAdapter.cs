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

    // TODO: [FEATURE-TIMEOUT-LOOKUP] Polling with timeout for element lookup
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

    // TODO: [FEATURE-TYPETEXT] Real text typing via TextBox pattern, ValuePattern, or Keyboard
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

    // TODO: [FEATURE-SCREENSHOT] Real UI window screenshot capturing via FlaUI.Core.Capturing
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

    // TODO: [FEATURE-INPUTIDLE] Real application input idle synchronization
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

    // TODO: [FEATURE-CHECKBOX] CheckBox state setting and reading
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

    // TODO: [FEATURE-COMBOBOX] ComboBox selection by text/index and querying
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

    // TODO: [FEATURE-RADIOBUTTON] RadioButton selection and query
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
