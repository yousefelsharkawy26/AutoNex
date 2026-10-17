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

    [Theory]
    [InlineData("submit_btn", "SubmitButton")]
    [InlineData("submit", "SubmitButton")]
    [InlineData("first_name", "FirstNameTextBox")]
    [InlineData("fname_txt", "FirstNameTextBox")]
    [InlineData("remember_me", "RememberMeCheckBox")]
    public void FuzzyLocator_ResolveBestMatch_HealsApproximateIdentifiers(string approximate, string expected)
    {
        var candidates = new List<CompactUiElement>
        {
            new() { Type = "Edit", AutomationId = "FirstNameTextBox", Name = "First Name" },
            new() { Type = "Edit", AutomationId = "LastNameTextBox", Name = "Last Name" },
            new() { Type = "Edit", AutomationId = "AgeTextBox", Name = "Age" },
            new() { Type = "Button", AutomationId = "SubmitButton", Name = "Submit" },
            new() { Type = "Button", AutomationId = "ClearButton", Name = "Clear" },
            new() { Type = "CheckBox", AutomationId = "RememberMeCheckBox", Name = "Remember Me" }
        };

        var resolved = FuzzyLocator.ResolveBestMatch(approximate, candidates);

        resolved.Should().Be(expected);
    }

    [Fact]
    public void AgentBatchRunner_ExecutesWithSelfHealing_WhenAgentProvidesSlightlyWrongId()
    {
        var mockDriver = new Mock<IDriver>();
        // Returns false for the misspelled ID
        mockDriver.Setup(d => d.TryFindByAutomationId("submit_btn", null)).Returns(false);
        // Returns true for the healed target ID
        mockDriver.Setup(d => d.TryFindByAutomationId("SubmitButton", null)).Returns(true);
        mockDriver.Setup(d => d.ClickByAutomationId("SubmitButton", null)).Returns(true);

        mockDriver.Setup(d => d.GetCompactUiTree("json"))
                  .Returns("""[{"type":"Button","id":"SubmitButton","name":"Submit"}]""");

        var runner = new AgentBatchRunner(mockDriver.Object);
        var actions = new List<AgentAction>
        {
            new() { Action = "click", AutomationId = "submit_btn" }
        };

        var result = runner.Execute(actions);

        result.Success.Should().BeTrue();
        mockDriver.Verify(d => d.ClickByAutomationId("SubmitButton", null), Times.Once);
    }

    [Fact]
    public void AgentStateTracker_ComputeDeltaAndRecord_IdentifiesStateChanges()
    {
        var tracker = new AgentStateTracker();

        var initial = new List<CompactUiElement>
        {
            new() { Type = "Edit", AutomationId = "FirstNameTextBox", Value = "" },
            new() { Type = "Text", AutomationId = "ResultTextBlock", Value = "" }
        };

        tracker.ComputeDeltaAndRecord(initial);

        var next = new List<CompactUiElement>
        {
            new() { Type = "Edit", AutomationId = "FirstNameTextBox", Value = "Sarah" },
            new() { Type = "Text", AutomationId = "ResultTextBlock", Value = "Welcome Sarah!" },
            new() { Type = "Button", AutomationId = "LogoutButton", Name = "Logout" }
        };

        var delta = tracker.ComputeDeltaAndRecord(next);

        delta.Should().Contain("#FirstNameTextBox val: \"\" -> \"Sarah\"");
        delta.Should().Contain("#ResultTextBlock val: \"\" -> \"Welcome Sarah!\"");
        delta.Should().Contain("+ [Button #LogoutButton \"Logout\"]");
    }

    [Fact]
    public void AgentStateTracker_ComputeDeltaAndRecord_ReturnsNoChange_WhenStateIdentical()
    {
        var tracker = new AgentStateTracker();

        var elements = new List<CompactUiElement>
        {
            new() { Type = "Button", AutomationId = "SubmitButton", Name = "Submit" }
        };

        tracker.ComputeDeltaAndRecord(elements);
        var delta = tracker.ComputeDeltaAndRecord(elements);

        delta.Should().Be("No UI state change.");
    }

    [Fact]
    public void AgentAutomationEngine_InspectScreen_ReturnsCompactFormat()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetCompactUiTree("text"))
                  .Returns("[Button #SubmitButton \"Submit\"]");

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var screen = engine.InspectScreen();

        screen.Should().Be("[Button #SubmitButton \"Submit\"]");
    }

    [Fact]
    public void AgentAutomationEngine_ExecutePlan_ExecutesAndReturnsSummary()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.ClickByAutomationId("SubmitButton", null)).Returns(true);
        mockDriver.Setup(d => d.TryFindByAutomationId("SubmitButton", null)).Returns(true);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var summary = engine.ExecutePlan("""[{"action":"click","id":"SubmitButton"}]""");

        summary.Should().Contain("SUCCESS: 1/1");
    }

    [Fact]
    public void AgentBatchRunner_Execute_IncludesSmartDiagnosticHint_OnFailure()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.ClickByAutomationId("NonExistentButton", null)).Returns(false);
        mockDriver.Setup(d => d.GetCompactUiTree("json"))
                  .Returns("""[{"type":"Button","id":"SubmitButton"},{"type":"Button","id":"ClearButton"}]""");

        var runner = new AgentBatchRunner(mockDriver.Object);
        var result = runner.Execute(new List<AgentAction>
        {
            new() { Action = "click", AutomationId = "NonExistentButton" }
        });

        result.Success.Should().BeFalse();
        result.ToCompactSummary().Should().Contain("Available IDs: SubmitButton, ClearButton");
    }
}

