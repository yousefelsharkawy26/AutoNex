using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.Agent;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Unit tests for Token-Efficient Agent Features:
/// - CompactUiElement formatting
/// - CompactUiTree serialization
/// - AgentBatchRunner pipeline execution (batching actions in 1 tool call)
/// </summary>
public class AgentAutomationTests
{
    [Fact]
    public void CompactUiElement_ToString_GeneratesTokenMinimizedFormat()
    {
        var element = new CompactUiElement
        {
            Type = "Button",
            AutomationId = "SubmitButton",
            Name = "Submit",
            State = "disabled"
        };

        var output = element.ToString();

        output.Should().Be("[Button #SubmitButton \"Submit\" (disabled)]");
    }

    [Fact]
    public void CompactUiElement_ToString_OmitsNullFieldsToSaveTokens()
    {
        var element = new CompactUiElement
        {
            Type = "Edit",
            AutomationId = "AgeTextBox"
        };

        var output = element.ToString();

        output.Should().Be("[Edit #AgeTextBox]");
    }

    [Fact]
    public void AgentBatchRunner_Execute_AllStepsSucceed_ReturnsSuccessSummary()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.TypeTextByAutomationId("FirstNameTextBox", "John", null)).Returns(true);
        mockDriver.Setup(d => d.ClickByAutomationId("SubmitButton", null)).Returns(true);
        string? resultText = "Hello, John!";
        mockDriver.Setup(d => d.TryGetTextByAutomationId("ResultTextBlock", out resultText, null)).Returns(true);

        var runner = new AgentBatchRunner(mockDriver.Object);
        var actions = new List<AgentAction>
        {
            new() { Action = "type", AutomationId = "FirstNameTextBox", Value = "John" },
            new() { Action = "click", AutomationId = "SubmitButton" },
            new() { Action = "assert_text", AutomationId = "ResultTextBlock", Contains = "John" }
        };

        var result = runner.Execute(actions);

        result.Success.Should().BeTrue();
        result.ExecutedStepsCount.Should().Be(3);
        result.TotalStepsCount.Should().Be(3);
        result.ToCompactSummary().Should().Contain("SUCCESS: 3/3");
    }

    [Fact]
    public void AgentBatchRunner_Execute_StepFails_HaltsAndReportsFailingStep()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.TypeTextByAutomationId("FirstNameTextBox", "John", null)).Returns(true);
        mockDriver.Setup(d => d.ClickByAutomationId("NonExistentButton", null)).Returns(false);

        var runner = new AgentBatchRunner(mockDriver.Object);
        var actions = new List<AgentAction>
        {
            new() { Action = "type", AutomationId = "FirstNameTextBox", Value = "John" },
            new() { Action = "click", AutomationId = "NonExistentButton" },
            new() { Action = "click", AutomationId = "ShouldNotRunButton" }
        };

        var result = runner.Execute(actions);

        result.Success.Should().BeFalse();
        result.ExecutedStepsCount.Should().Be(2);
        result.TotalStepsCount.Should().Be(3);
        result.ToCompactSummary().Should().Contain("FAILED at Step 2");
        mockDriver.Verify(d => d.ClickByAutomationId("ShouldNotRunButton", null), Times.Never);
    }

    [Fact]
    public void AgentBatchRunner_ExecuteJson_ParsesAndExecutesMultiStepJson()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SetCheckboxByAutomationId("RememberMeCheckBox", true, null)).Returns(true);
        mockDriver.Setup(d => d.SelectComboBoxItemByText("CountryComboBox", "Egypt", null)).Returns(true);
        mockDriver.Setup(d => d.PressKey("Return")).Returns(true);

        var runner = new AgentBatchRunner(mockDriver.Object);
        var jsonPayload = """
        [
            { "action": "check", "id": "RememberMeCheckBox" },
            { "action": "select", "id": "CountryComboBox", "value": "Egypt" },
            { "action": "press_key", "key": "Return" }
        ]
        """;

        var result = runner.ExecuteJson(jsonPayload);

        result.Success.Should().BeTrue();
        result.ExecutedStepsCount.Should().Be(3);
    }

    [Fact]
    public void AgentBatchRunner_ExecuteJson_InvalidJson_ReturnsErrorResult()
    {
        var mockDriver = new Mock<IDriver>();
        var runner = new AgentBatchRunner(mockDriver.Object);

        var result = runner.ExecuteJson("invalid json content");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed to parse actions JSON");
    }

    [Fact]
    public void Driver_GetCompactUiTree_ReturnsCompactFormat()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetCompactUiTree("text"))
                  .Returns("[Edit #FirstNameTextBox]\n[Button #SubmitButton \"Submit\"]");

        var textTree = mockDriver.Object.GetCompactUiTree("text");

        textTree.Should().Contain("[Edit #FirstNameTextBox]");
        textTree.Should().Contain("[Button #SubmitButton \"Submit\"]");
    }
}
