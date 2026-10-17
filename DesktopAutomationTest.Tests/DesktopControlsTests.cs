using System;
using Xunit;
using FluentAssertions;
using Moq;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Unit tests for desktop UI controls automation (CheckBox, ComboBox, RadioButton).
/// </summary>
public class DesktopControlsTests
{
    [Fact]
    public void MockDriver_SetCheckbox_VerifiesInteraction()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SetCheckboxByAutomationId("RememberMeCheckbox", true, null))
                  .Returns(true);

        var result = mockDriver.Object.SetCheckboxByAutomationId("RememberMeCheckbox", true);

        result.Should().BeTrue();
        mockDriver.Verify(d => d.SetCheckboxByAutomationId("RememberMeCheckbox", true, null), Times.Once);
    }

    [Fact]
    public void MockDriver_IsCheckboxChecked_ReturnsExpectedState()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.IsCheckboxChecked("TermsCheckbox", null))
                  .Returns(true);

        var isChecked = mockDriver.Object.IsCheckboxChecked("TermsCheckbox");

        isChecked.Should().BeTrue();
        mockDriver.Verify(d => d.IsCheckboxChecked("TermsCheckbox", null), Times.Once);
    }

    [Fact]
    public void MockDriver_SelectComboBoxItemByText_VerifiesInteraction()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SelectComboBoxItemByText("CountryDropdown", "Egypt", null))
                  .Returns(true);

        var result = mockDriver.Object.SelectComboBoxItemByText("CountryDropdown", "Egypt");

        result.Should().BeTrue();
        mockDriver.Verify(d => d.SelectComboBoxItemByText("CountryDropdown", "Egypt", null), Times.Once);
    }

    [Fact]
    public void MockDriver_SelectComboBoxItemByIndex_VerifiesInteraction()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SelectComboBoxItemByIndex("CountryDropdown", 2, null))
                  .Returns(true);

        var result = mockDriver.Object.SelectComboBoxItemByIndex("CountryDropdown", 2);

        result.Should().BeTrue();
        mockDriver.Verify(d => d.SelectComboBoxItemByIndex("CountryDropdown", 2, null), Times.Once);
    }

    [Fact]
    public void MockDriver_GetSelectedComboBoxItem_ReturnsSelectedText()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetSelectedComboBoxItem("CountryDropdown", null))
                  .Returns("United States");

        var selected = mockDriver.Object.GetSelectedComboBoxItem("CountryDropdown");

        selected.Should().Be("United States");
        mockDriver.Verify(d => d.GetSelectedComboBoxItem("CountryDropdown", null), Times.Once);
    }

    [Fact]
    public void MockDriver_SelectRadioButton_VerifiesInteraction()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SelectRadioButtonByAutomationId("MaleRadioButton", null))
                  .Returns(true);

        var result = mockDriver.Object.SelectRadioButtonByAutomationId("MaleRadioButton");

        result.Should().BeTrue();
        mockDriver.Verify(d => d.SelectRadioButtonByAutomationId("MaleRadioButton", null), Times.Once);
    }

    [Fact]
    public void MockDriver_IsRadioButtonSelected_ReturnsExpectedState()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.IsRadioButtonSelected("FemaleRadioButton", null))
                  .Returns(false);

        var isSelected = mockDriver.Object.IsRadioButtonSelected("FemaleRadioButton");

        isSelected.Should().BeFalse();
        mockDriver.Verify(d => d.IsRadioButtonSelected("FemaleRadioButton", null), Times.Once);
    }
}

