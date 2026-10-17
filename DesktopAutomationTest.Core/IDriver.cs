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
}