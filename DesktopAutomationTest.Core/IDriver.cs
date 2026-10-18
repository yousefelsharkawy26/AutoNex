using System;
using System.Collections.Generic;
using System.Drawing;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Defines the interface for UI automation drivers.
/// </summary>
public interface IDriver : IDisposable
{
    /// <summary>
    /// Initializes the driver with the specified application path.
    /// </summary>
    /// <param name="appPath">Path to the application executable.</param>
    /// <param name="arguments">Command line arguments for the application.</param>
    void Initialize(string appPath, string? arguments = null);

    /// <summary>
    /// Finds an element by its automation ID.
    /// </summary>
    /// <param name="automationId">The automation ID of the element.</param>
    /// <param name="timeout">Optional timeout for finding the element.</param>
    /// <returns>True if the element was found; otherwise, false.</returns>
    bool TryFindByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Clicks on an element identified by its automation ID.
    /// </param>
    /// <param name="automationId">The automation ID of the element to click.</param>
    /// <param name="timeout">Optional timeout for finding the element.</param>
    /// <returns>True if the element was clicked; otherwise, false.</returns>
    bool ClickByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Types text into an element identified by its automation ID.
    /// </summary>
    /// <param name="automationId">The automation ID of the element.</param>
    /// <param name="text">The text to type.</param>
    /// <param name="timeout">Optional timeout for finding the element.</param>
    /// <returns>True if the text was typed; otherwise, false.</returns>
    bool TypeTextByAutomationId(string automationId, string text, TimeSpan? timeout = null);

    /// <summary>
    /// Gets the text of an element identified by its automation ID.
    /// </summary>
    /// <param name="automationId">The automation ID of the element.</param>
    /// <param name="text">When this method returns, contains the text of the element if found.</param>
    /// <param name="timeout">Optional timeout for finding the element.</param>
    /// <returns>True if the element was found and text retrieved; otherwise, false.</returns>
    bool TryGetTextByAutomationId(string automationId, out string? text, TimeSpan? timeout = null);

    /// <summary>
    /// Takes a screenshot and saves it to the specified path.
    /// </summary>
    /// <param name="filePath">The path where the screenshot should be saved.</param>
    /// <returns>True if the screenshot was taken successfully; otherwise, false.</returns>
    bool TakeScreenshot(string filePath);

    /// <summary>
    /// Gets the current window bounds.
    /// </summary>
    /// <returns>The bounding rectangle of the main application window.</returns>
    Rectangle GetWindowBounds();

    /// <summary>
    /// Waits for the application to be idle.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the application to be idle.</param>
    /// <returns>True if the application became idle within the timeout; otherwise, false.</returns>
    bool WaitForInputIdle(TimeSpan timeout);

    /// <summary>
    /// Sets the checked state of a CheckBox element identified by its automation ID.
    /// </summary>
    bool SetCheckboxByAutomationId(string automationId, bool isChecked, TimeSpan? timeout = null);

    /// <summary>
    /// Checks whether a CheckBox element is currently checked.
    /// </summary>
    bool IsCheckboxChecked(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Selects an item from a ComboBox by its display text.
    /// </summary>
    bool SelectComboBoxItemByText(string automationId, string text, TimeSpan? timeout = null);

    /// <summary>
    /// Selects an item from a ComboBox by its zero-based index.
    /// </summary>
    bool SelectComboBoxItemByIndex(string automationId, int index, TimeSpan? timeout = null);

    /// <summary>
    /// Retrieves the text of the currently selected item in a ComboBox.
    /// </summary>
    string? GetSelectedComboBoxItem(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Selects a RadioButton element identified by its automation ID.
    /// </summary>
    bool SelectRadioButtonByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Checks whether a RadioButton element is currently selected.
    /// </summary>
    bool IsRadioButtonSelected(string automationId, TimeSpan? timeout = null);

    // ── [FEATURE-KEYBOARD-MOUSE] Advanced keyboard and mouse input ─────────────

    /// <summary>
    /// Double-clicks an element identified by its automation ID.
    /// </summary>
    /// <param name="automationId">The automation ID of the target element.</param>
    /// <param name="timeout">Optional element lookup timeout.</param>
    /// <returns>True if the double-click was performed; otherwise false.</returns>
    bool DoubleClickByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Right-clicks an element identified by its automation ID.
    /// </summary>
    /// <param name="automationId">The automation ID of the target element.</param>
    /// <param name="timeout">Optional element lookup timeout.</param>
    /// <returns>True if the right-click was performed; otherwise false.</returns>
    bool RightClickByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Moves the mouse pointer over an element (hover) without clicking.
    /// </summary>
    /// <param name="automationId">The automation ID of the target element.</param>
    /// <param name="timeout">Optional element lookup timeout.</param>
    /// <returns>True if the hover was performed; otherwise false.</returns>
    bool HoverOverByAutomationId(string automationId, TimeSpan? timeout = null);

    /// <summary>
    /// Presses a single key by its virtual-key name (e.g. "Return", "Escape", "Tab").
    /// </summary>
    /// <param name="key">The key name — values mirror <see cref="FlaUI.Core.Input.VirtualKeyShort"/>.</param>
    /// <returns>True if the key press was sent; otherwise false.</returns>
    bool PressKey(string key);

    /// <summary>
    /// Types a raw string as a sequence of key presses (simulates keyboard input).
    /// Use <see cref="TypeTextByAutomationId"/> to enter text into a specific element;
    /// use this method to send global keyboard input (e.g. shortcuts, Enter after focus).
    /// </summary>
    /// <param name="text">The text to type globally.</param>
    /// <returns>True if the keys were sent; otherwise false.</returns>
    bool SendKeys(string text);

    /// <summary>
    /// Drags an element and drops it onto another element.
    /// </summary>
    /// <param name="sourceAutomationId">AutomationId of the element to drag.</param>
    /// <param name="targetAutomationId">AutomationId of the drop target.</param>
    /// <param name="timeout">Optional element lookup timeout.</param>
    /// <returns>True if the drag-and-drop was performed; otherwise false.</returns>
    bool DragAndDrop(string sourceAutomationId, string targetAutomationId, TimeSpan? timeout = null);

    /// <summary>
    /// Returns a compact, token-minimized representation of the interactive UI tree for LLM agents.
    /// Includes all application windows (main window + open dialogs).
    /// </summary>
    /// <param name="format">Format of the dump: "text" (default, minimal tokens) or "json".</param>
    /// <param name="maxDepth">Optional maximum hierarchy traversal depth.</param>
    /// <param name="containerId">Optional container AutomationId to filter children.</param>
    string GetCompactUiTree(string format = "text", int maxDepth = -1, string? containerId = null);

    // ── [FEATURE-WINDOWS] Multi-window / dialog support ───────────────────────

    /// <summary>
    /// Lists titles of all top-level windows belonging to the automated application.
    /// </summary>
    IReadOnlyList<(string Title, int ProcessId)> ListAppWindows();

    /// <summary>
    /// Switches the automation context to the application window whose title contains
    /// the given text (case-insensitive).
    /// </summary>
    bool SwitchWindow(string titleContains);

    /// <summary>
    /// Attaches the driver to an already-running process by its OS process id.
    /// </summary>
    void AttachToProcess(int processId);

    /// <summary>
    /// Attaches the driver to an already-running process by executable name.
    /// </summary>
    void AttachToProcessByName(string processName);

    /// <summary>
    /// Captures the current window/screen and returns PNG bytes (for base64 image transport).
    /// </summary>
    byte[]? CaptureScreenshotBytes();

    /// <summary>
    /// Returns false when the target application has exited or the driver was disposed.
    /// </summary>
    bool IsApplicationAlive();

    // ── Window & Multi-Window Management ──────────────────────────────────────

    /// <summary>
    /// Returns titles of all open top-level windows belonging to the application process.
    /// </summary>
    IReadOnlyList<string> GetOpenWindowTitles();

    /// <summary>
    /// Switches the active automation context to a window matching the specified title (exact or substring).
    /// </summary>
    /// <param name="titleSubstring">Substring or title of the target window.</param>
    /// <param name="timeout">Optional timeout to locate the window.</param>
    /// <returns>True if switched successfully; otherwise false.</returns>
    bool SwitchToWindow(string titleSubstring, TimeSpan? timeout = null);

    /// <summary>
    /// Closes the currently active window. If it is the last window, the application terminates.
    /// </summary>
    void CloseCurrentWindow();

    // ── Scrolling & Accessibility Navigation ──────────────────────────────────

    /// <summary>
    /// Scrolls the element or active window in the specified direction ("up", "down", "left", "right").
    /// </summary>
    /// <param name="direction">Direction to scroll: "down" (default), "up", "left", "right".</param>
    /// <param name="automationId">Optional container ID. If null, scrolls active window.</param>
    /// <returns>True if scroll was executed; otherwise false.</returns>
    bool Scroll(string direction = "down", string? automationId = null);

    /// <summary>
    /// Scrolls a container until the target element is visible on screen.
    /// </summary>
    /// <param name="targetAutomationId">Automation ID of the element to scroll into view.</param>
    /// <returns>True if element was brought into view; otherwise false.</returns>
    bool ScrollIntoView(string targetAutomationId);
}