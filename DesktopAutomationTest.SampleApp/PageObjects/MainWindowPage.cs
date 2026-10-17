using System;
using DesktopAutomationTest.Core;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DesktopAutomationTest.SampleApp.PageObjects;

/// <summary>
/// Page object model for the MainWindow of the Sample Application.
/// Encapsulates UI elements and interactions for automated testing.
/// </summary>
public class MainWindowPage : IDisposable
{
    private readonly FlaUIAdapter _driver;
    private readonly Window _window;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MainWindowPage class.
    /// </summary>
    /// <param name="driver">The FlaUI adapter for UI automation.</param>
    /// <param name="window">The main window of the application.</param>
    public MainWindowPage(FlaUIAdapter driver, Window window)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    #region UI Elements (as AutomationElement)

    /// <summary>
    /// Gets the First Name text box as an AutomationElement.
    /// </summary>
    public AutomationElement FirstNameTextBoxElement => _window.FindFirstDescendant(cf => cf.ByAutomationId("FirstNameTextBox"));

    /// <summary>
    /// Gets the Last Name text box as an AutomationElement.
    /// </summary>
    public AutomationElement LastNameTextBoxElement => _window.FindFirstDescendant(cf => cf.ByAutomationId("LastNameTextBox"));

    /// <summary>
    /// Gets the Age text box as an AutomationElement.
    /// </summary>
    public AutomationElement AgeTextBoxElement => _window.FindFirstDescendant(cf => cf.ByAutomationId("AgeTextBox"));

    ///<summary>
    /// Gets the Submit button as an AutomationElement.
    /// </summary>
    public AutomationElement SubmitButtonElement => _window.FindFirstDescendant(cf => cf.ByAutomationId("SubmitButton"));

    /// <summary>
    /// Gets the Result text block as an AutomationElement.
    /// </summary>
    public AutomationElement ResultTextBlockElement => _window.FindFirstDescendant(cf => cf.ByAutomationId("ResultTextBlock"));

    #endregion

    #region Actions

    /// <summary>
    /// Enters text in the First Name field.
    /// </summary>
    /// <param name="text">The text to enter.</param>
    public void EnterFirstName(string text)
    {
        var element = FirstNameTextBoxElement;
        if (element == null)
            throw new InvalidOperationException("FirstNameTextBox not found");

        _driver.TypeTextByAutomationId("FirstNameTextBox", text);
    }

    /// <summary>
    /// Enters text in the Last Name field.
    /// </summary>
    /// <param name="text">The text to enter.</param>
    public void EnterLastName(string text)
    {
        _driver.TypeTextByAutomationId("LastNameTextBox", text);
    }

    /// <summary>
    /// Enters text in the Age field.
    /// </summary>
    /// <param name="text">The text to enter.</param>
    public void EnterAge(string text)
    {
        _driver.TypeTextByAutomationId("AgeTextBox", text);
    }

    /// <summary>
    /// Clicks the Submit button.
    /// </summary>
    /// <returns>True if the button was clicked successfully.</returns>
    public bool ClickSubmit()
    {
        return _driver.ClickByAutomationId("SubmitButton");
    }

    /// <summary>
    /// Gets the text from the First Name field.
    /// </summary>
    /// <returns>The first name text, or empty string if the element is not found.</returns>
    public string GetFirstNameText()
    {
        var element = FirstNameTextBoxElement;
        if (element == null)
            return string.Empty;

        return element.Properties.Name ?? string.Empty;
    }

    /// <summary>
    /// Gets the text from the Last Name field.
    /// </summary>
    /// <returns>The last name text, or empty string if the element is not found.</returns>
    public string GetLastNameText()
    {
        var element = LastNameTextBoxElement;
        if (element == null)
            return string.Empty;

        return element.Properties.Name ?? string.Empty;
    }

    /// <summary>
    /// Gets the text from the Age field.
    /// </summary>
    /// <returns>The age text, or empty string if the element is not found.</returns>
    public string GetAgeText()
    {
        var element = AgeTextBoxElement;
        if (element == null)
            return string.Empty;

        return element.Properties.Name ?? string.Empty;
    }

    /// <summary>
    /// Gets the text from the Result field.
    /// </summary>
    /// <returns>The result text, or null if the element is not found.</returns>
    public string GetResultText()
    {
        var element = ResultTextBlockElement;
        if (element == null)
            return null;

        return element.Properties.Name;
    }

    /// <summary>
    /// Clears all input fields.
    /// </summary>
    public void ClearAllFields()
    {
        EnterFirstName(string.Empty);
        EnterLastName(string.Empty);
        EnterAge(string.Empty);
    }

    /// <summary>
    /// Fills out the form with sample data and submits it.
    /// </summary>
    /// <param name="firstName">The first name to enter.</param>
    /// <param name="lastName">The last name to enter.</param>
    /// <param name="age">The age to enter.</param>
    public void SubmitForm(string firstName, string lastName, string age)
    {
        ClearAllFields();
        EnterFirstName(firstName);
        EnterLastName(lastName);
        EnterAge(age);
        ClickSubmit();
    }

    #endregion

    #region Verification Methods

    /// <summary>
    /// Verifies that the result text contains the expected value.
    /// </summary>
    /// <param name="expectedText">The expected text to find in the result.</param>
    /// <returns>True if the result contains the expected text.</returns>
    public bool VerifyResultContains(string expectedText)
    {
        var resultText = GetResultText();
        return !string.IsNullOrEmpty(resultText) && resultText.Contains(expectedText);
    }

    /// <summary>
    /// Verifies that the result text exactly matches the expected value.
    /// </summary>
    /// <param name="expectedText">The expected result text.</param>
    /// <returns>True if the result exactly matches the expected text.</returns>
    public bool VerifyResultEquals(string expectedText)
    {
        var resultText = GetResultText();
        return string.Equals(resultText, expectedText, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region IDisposable Implementation

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
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                // Note: We don't dispose the driver or window here as they are owned by the caller
            }

            // Free unmanaged resources
            _disposed = true;
        }
    }

    #endregion
}