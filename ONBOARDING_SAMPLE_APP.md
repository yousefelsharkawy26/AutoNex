# Sample Application Onboarding Guide

## Overview
This document provides guidance on using the DesktopAutomationTest.SampleApp WPF application for UI testing with FlaUI.

## Application Description
The Sample Application is a simple data entry form designed for demonstrating UI testing concepts:
- First Name text box
- Last Name text box  
- Age text box
- Submit button
- Result display area

## UI Elements and Automation IDs
All interactive elements have been assigned Automation IDs for reliable test automation:

| Element | Automation ID | Type |
|---------|---------------|------|
| First Name Input | FirstNameTextBox | TextBox |
| Last Name Input | LastNameTextBox | TextBox |
| Age Input | AgeTextBox | TextBox |
| Submit Button | SubmitButton | Button |
| Clear Button | ClearButton | Button |
| Result Display | ResultTextBlock | TextBlock |

## Application Behavior
1. **Validation**: The application validates that all fields are filled and that age is a valid number
2. **Submission**: When valid data is entered and the Submit button is clicked, a greeting message is displayed
3. **Error Handling**: Validation errors are displayed in red; successful submissions appear in green

## Using with FlaUI Tests
The application is designed to work with the FlaUIAdapter in DesktopAutomationTest.Core:

### Basic Usage Pattern
```csharp
// Initialize FlaUI adapter
var adapter = new FlaUIAdapter();

// Launch or attach to the sample application
var app = adapter.LaunchApplication("path/to/DesktopAutomationTest.SampleApp.exe");
// Or: var app = adapter.AttachToApplication(processId);

// Get the main window
var window = adapter.GetMainWindow(app);

// Create page object
var page = new MainWindowPage(adapter, window);

// Perform actions
page.EnterFirstName("John");
page.EnterLastName("Doe");
page.EnterAge("30");
page.ClickSubmit();

// Verify results
bool success = page.VerifyResultEquals("Hello, John Doe! You are 30 years old.");
```

### Page Object Methods
The MainWindowPage class provides the following methods:
- `EnterFirstName(string text)`: Enters text in the first name field
- `EnterLastName(string text)`: Enters text in the last name field  
- `EnterAge(string text)`: Enters text in the age field
- `ClickSubmit()`: Clicks the submit button
- `GetResultText()`: Retrieves the result text
- `ClearAllFields()`: Clears all input fields
- `SubmitForm(string firstName, string lastName, string age)`: Completes the full form submission
- `VerifyResultContains(string expectedText)`: Checks if result contains expected text
- `VerifyResultEquals(string expectedText)`: Checks if result exactly matches expected text

## Building and Running
### Build the Application
```bash
dotnet build DesktopAutomationTest.SampleApp/DesktopAutomationTest.SampleApp.csproj
```

### Run the Application
```bash
dotnet run --project DesktopAutomationTest.SampleApp/DesktopAutomationTest.SampleApp.csproj
```

## Test Implementation Notes
1. The application targets .NET 10.0 Windows (net10.0-windows)
2. UI tests require Windows OS due to WPF dependencies
3. Tests should be marked with `[RequiresWindowsFact]` or `[RequiresWindowsTheory]` 
4. The application will skip execution on non-Windows platforms when using proper test attributes

## Troubleshooting
### Common Issues
1. **Element Not Found**: Ensure the application is fully launched before attempting to find elements
2. **Timing Issues**: Consider adding appropriate waits when interacting with the UI
3. **Permission Issues**: Ensure the test process has sufficient UI automation permissions

### Debugging Tips
- Use tools like Inspect.exe (Windows SDK) to verify Automation IDs
- Check application logs for startup errors
- Verify that FlaUI UIA3 adapter is properly configured