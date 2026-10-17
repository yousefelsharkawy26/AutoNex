using System;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Tests.Helpers;
using FluentAssertions;
using Reqnroll;

namespace DesktopAutomationTest.Tests.StepDefinitions;

/// <summary>
/// Step definitions for advanced desktop controls (CheckBox, ComboBox, RadioButton).
/// </summary>
[Binding]
public class DesktopControlsSteps
{
    private readonly IDriver _driver;

    public DesktopControlsSteps(IDriver driver)
    {
        _driver = driver;
    }

    [Given(@"the desktop application is running")]
    public void GivenTheDesktopApplicationIsRunning()
    {
        _driver.Should().NotBeNull();
    }

    [When(@"I check the ""(.*)""")]
    public void WhenICheckThe(string automationId)
    {
        _driver.SetCheckboxByAutomationId(automationId, true);
    }

    [When(@"I uncheck the ""(.*)""")]
    public void WhenIUncheckThe(string automationId)
    {
        _driver.SetCheckboxByAutomationId(automationId, false);
    }

    [Then(@"the ""(.*)"" should be checked")]
    public void ThenTheShouldBeChecked(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _driver.IsCheckboxChecked(automationId).Should().BeTrue();
        }
    }

    [Then(@"the ""(.*)"" should not be checked")]
    public void ThenTheShouldNotBeChecked(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _driver.IsCheckboxChecked(automationId).Should().BeFalse();
        }
    }

    [When(@"I select ""(.*)"" from the ""(.*)"" dropdown")]
    public void WhenISelectFromTheDropdown(string text, string automationId)
    {
        _driver.SelectComboBoxItemByText(automationId, text);
    }

    [Then(@"the selected item in ""(.*)"" should be ""(.*)""")]
    public void ThenTheSelectedItemInShouldBe(string automationId, string expectedText)
    {
        if (PlatformGuard.IsWindows)
        {
            _driver.GetSelectedComboBoxItem(automationId).Should().Be(expectedText);
        }
    }

    [When(@"I select the ""(.*)"" radio button")]
    public void WhenISelectTheRadioButton(string automationId)
    {
        _driver.SelectRadioButtonByAutomationId(automationId);
    }

    [Then(@"the ""(.*)"" radio button should be selected")]
    public void ThenTheRadioButtonShouldBeSelected(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _driver.IsRadioButtonSelected(automationId).Should().BeTrue();
        }
    }
}
