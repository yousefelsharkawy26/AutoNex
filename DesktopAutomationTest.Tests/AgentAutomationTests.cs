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

    // ── [MCP-TOOLS-V1.1] New engine helper tests ─────────────────────────────

    [Fact]
    public void AgentAutomationEngine_WaitForElement_ReturnsFound_WhenElementExists()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver
            .Setup(d => d.TryFindByAutomationId("OkButton", It.IsAny<TimeSpan?>()))
            .Returns(true);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.WaitForElement("OkButton", 5);

        result.Should().Contain("found");
        result.Should().NotContain("Timeout");
    }

    [Fact]
    public void AgentAutomationEngine_WaitForElement_ReturnsTimeout_WhenElementMissing()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver
            .Setup(d => d.TryFindByAutomationId("GhostButton", It.IsAny<TimeSpan?>()))
            .Returns(false);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.WaitForElement("GhostButton", 3);

        result.Should().StartWith("Timeout:");
        result.Should().Contain("GhostButton");
    }

    [Fact]
    public void AgentAutomationEngine_GetElementText_ReturnsText_WhenElementFound()
    {
        var mockDriver = new Mock<IDriver>();
        string? txt = "Hello World";
        mockDriver
            .Setup(d => d.TryGetTextByAutomationId("LabelElement", out txt, null))
            .Returns(true);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.GetElementText("LabelElement");

        result.Should().Be("Hello World");
    }

    [Fact]
    public void AgentAutomationEngine_GetElementText_ReturnsError_WhenElementMissing()
    {
        var mockDriver = new Mock<IDriver>();
        string? txt = null;
        mockDriver
            .Setup(d => d.TryGetTextByAutomationId("Missing", out txt, null))
            .Returns(false);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.GetElementText("Missing");

        result.Should().Contain("Error");
        result.Should().Contain("Missing");
    }

    [Fact]
    public void AgentAutomationEngine_TakeScreenshot_ReturnsPath_WhenSuccessful()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver
            .Setup(d => d.TakeScreenshot(It.IsAny<string>()))
            .Returns(true);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.TakeScreenshot("/tmp/test.png");

        result.Should().Contain("Screenshot saved");
        result.Should().Contain("/tmp/test.png");
    }

    // ── [SESSION-REPLAY] Tests ─────────────────────────────────────────────

    [Fact]
    public void SessionRecorder_RecordStep_CapturesStepsAndSavesToJson()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.TypeTextByAutomationId("UserField", "Alice", null)).Returns(true);
        mockDriver.Setup(d => d.ClickByAutomationId("LoginBtn", null)).Returns(true);

        var recorder = new SessionRecorder(mockDriver.Object, appPath: "TestApp.exe", label: "test session");
        var (s1, m1) = recorder.RecordStep("type", "UserField", "Alice");
        var (s2, m2) = recorder.RecordStep("click", "LoginBtn");

        s1.Should().BeTrue();
        s2.Should().BeTrue();
        recorder.Session.TotalSteps.Should().Be(2);
        recorder.Session.PassedSteps.Should().Be(2);
        recorder.Session.Label.Should().Be("test session");

        var tempPath = Path.Combine(Path.GetTempPath(), $"rec_test_{Guid.NewGuid():N}.json");
        try
        {
            recorder.Save(tempPath);
            File.Exists(tempPath).Should().BeTrue();
            var json = File.ReadAllText(tempPath);
            json.Should().Contain("UserField");
            json.Should().Contain("LoginBtn");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SessionReplayer_Replay_ExecutesStepsAndHandlesSelfHealing()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetCompactUiTree("json"))
                  .Returns("[{\"type\":\"Button\",\"id\":\"SubmitButton\"}]");
        mockDriver.Setup(d => d.ClickByAutomationId("SubmitButton", null)).Returns(true);

        var session = new SessionRecord
        {
            SessionId = "test-replay",
            Steps = new List<SessionStep>
            {
                new() { Index = 0, Action = "click", AutomationId = "submit_btn" }
            }
        };

        var replayer = new SessionReplayer(mockDriver.Object);
        var result = replayer.Replay(session);

        result.Success.Should().BeTrue();
        result.PassedSteps.Should().Be(1);
        result.StepResults[0].ResolvedId.Should().Be("SubmitButton");
    }

    [Fact]
    public void SessionReplayer_ReplayFile_FailsGracefully_WhenFileNotFound()
    {
        var mockDriver = new Mock<IDriver>();
        var replayer = new SessionReplayer(mockDriver.Object);
        var result = replayer.ReplayFile("non_existent_file.json");

        result.Success.Should().BeFalse();
        result.ToSummary().Should().Contain("not found");
    }

    [Fact]
    public void AgentAutomationEngine_ExecuteAndRecord_And_ReplaySession_WorkTogether()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.ClickByAutomationId("OkBtn", null)).Returns(true);
        mockDriver.Setup(d => d.GetCompactUiTree("json"))
                  .Returns("[{\"type\":\"Button\",\"id\":\"OkBtn\"}]");

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var tempPath = Path.Combine(Path.GetTempPath(), $"eng_rec_{Guid.NewGuid():N}.json");

        try
        {
            var planJson = "[{\"action\":\"click\",\"id\":\"OkBtn\"}]";
            var recordResult = engine.ExecuteAndRecord(planJson, tempPath, "engine-flow");
            recordResult.Should().Contain("Session saved");

            var replayResult = engine.ReplaySession(tempPath);
            replayResult.Should().Contain("PASSED");
            replayResult.Should().Contain("1/1 steps");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ── [MULTI-WINDOW] Tests ───────────────────────────────────────────────

    [Fact]
    public void AgentAutomationEngine_ListWindows_ReturnsFormattedList_WhenWindowsExist()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetOpenWindowTitles())
                  .Returns(new List<string> { "Main Window", "Settings Dialog" });

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.ListWindows();

        result.Should().Contain("[0] Main Window");
        result.Should().Contain("[1] Settings Dialog");
    }

    [Fact]
    public void AgentAutomationEngine_ListWindows_ReturnsMessage_WhenNoWindows()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.GetOpenWindowTitles())
                  .Returns(new List<string>());

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.ListWindows();

        result.Should().Be("No open windows found.");
    }

    [Fact]
    public void AgentAutomationEngine_SwitchWindow_ReturnsSuccess_WhenFound()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SwitchToWindow("Settings", It.IsAny<TimeSpan?>()))
                  .Returns(true);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.SwitchWindow("Settings", 3);

        result.Should().Contain("Switched context to window matching: 'Settings'");
    }

    [Fact]
    public void AgentAutomationEngine_SwitchWindow_ReturnsError_WhenNotFound()
    {
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.SwitchToWindow("NonExistent", It.IsAny<TimeSpan?>()))
                  .Returns(false);

        var engine = new AgentAutomationEngine(mockDriver.Object);
        var result = engine.SwitchWindow("NonExistent", 2);

        result.Should().StartWith("Error:");
        result.Should().Contain("NonExistent");
    }

    // ── [SESSION-TO-TEST] Generator Tests ────────────────────────────────────

    [Fact]
    public void SessionTestGenerator_GenerateCSharpTest_ProducesValidCSharpMethod()
    {
        var session = new SessionRecord
        {
            SessionId = "test-export",
            AppPath = "C:\\Apps\\Sample.exe",
            Label = "User Registration Flow",
            Steps = new List<SessionStep>
            {
                new() { Index = 0, Action = "type", AutomationId = "UsernameTextBox", Value = "JohnDoe" },
                new() { Index = 1, Action = "click", AutomationId = "RegisterButton" },
                new() { Index = 2, Action = "assert_text", AutomationId = "SuccessLabel", Value = "Welcome" }
            }
        };

        var code = SessionTestGenerator.GenerateCSharpTest(session);

        code.Should().Contain("[Fact]");
        code.Should().Contain("public void User_Registration_Flow()");
        code.Should().Contain("driver.TypeTextByAutomationId(\"UsernameTextBox\", \"JohnDoe\")");
        code.Should().Contain("driver.ClickByAutomationId(\"RegisterButton\")");
        code.Should().Contain("text2.Should().Contain(\"Welcome\")");
    }

    [Fact]
    public void SessionTestGenerator_GenerateGherkinScenario_ProducesValidGherkin()
    {
        var session = new SessionRecord
        {
            SessionId = "test-gherkin",
            AppPath = "App.exe",
            Label = "Login Feature",
            Steps = new List<SessionStep>
            {
                new() { Index = 0, Action = "type", AutomationId = "Email", Value = "test@example.com" },
                new() { Index = 1, Action = "click", AutomationId = "LoginBtn" },
                new() { Index = 2, Action = "assert_text", AutomationId = "Dashboard", Value = "Overview" }
            }
        };

        var gherkin = SessionTestGenerator.GenerateGherkinScenario(session);

        gherkin.Should().Contain("Scenario: Login Feature");
        gherkin.Should().Contain("Given the application \"App.exe\" is launched");
        gherkin.Should().Contain("When I type \"test@example.com\" into \"Email\"");
        gherkin.Should().Contain("When I click \"LoginBtn\"");
        gherkin.Should().Contain("Then \"Dashboard\" should contain text \"Overview\"");
    }

    [Fact]
    public void AgentAutomationEngine_ExportSessionAsTest_ExportsCSharpAndGherkinFromFile()
    {
        var mockDriver = new Mock<IDriver>();
        var engine = new AgentAutomationEngine(mockDriver.Object);

        var tempPath = Path.Combine(Path.GetTempPath(), $"gen_test_{Guid.NewGuid():N}.json");
        var session = new SessionRecord
        {
            SessionId = "gen-check",
            Label = "Test Flow",
            Steps = new List<SessionStep>
            {
                new() { Index = 0, Action = "click", AutomationId = "StartBtn" }
            }
        };

        try
        {
            File.WriteAllText(tempPath, System.Text.Json.JsonSerializer.Serialize(session));

            var csResult = engine.ExportSessionAsTest(tempPath, "csharp");
            csResult.Should().Contain("public class RecordedFlowTests");
            csResult.Should().Contain("StartBtn");

            var ghResult = engine.ExportSessionAsTest(tempPath, "gherkin");
            ghResult.Should().Contain("Scenario: Test Flow");
            ghResult.Should().Contain("When I click \"StartBtn\"");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
