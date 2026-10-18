using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.Agent;
using FluentAssertions;
using Xunit;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Unit and integration tests for <see cref="AvaloniaDriver"/> running completely Cross-Platform (Linux/macOS/Windows).
/// Verifies interactive element extraction, actions (type, click, select, check), and batch runner execution.
/// </summary>
public class AvaloniaDriverTests
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    public AvaloniaDriverTests()
    {
        lock (InitLock)
        {
            if (!_initialized)
            {
                AppBuilder.Configure<Application>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
                _initialized = true;
            }
        }
    }

    private Window CreateSampleWindow()
    {
        var window = new Window
        {
            Title = "Sample Avalonia Window",
            Width = 800,
            Height = 600
        };

        var stack = new StackPanel();

        var tb = new TextBox
        {
            Name = "UsernameTextBox",
            Text = ""
        };
        AutomationProperties.SetAutomationId(tb, "UsernameTextBox");

        var cb = new CheckBox
        {
            Name = "AgreeCheckBox",
            Content = "I Agree",
            IsChecked = false
        };
        AutomationProperties.SetAutomationId(cb, "AgreeCheckBox");

        var combo = new ComboBox
        {
            Name = "CountryComboBox",
            ItemsSource = new List<string> { "Egypt", "United States", "Germany" }
        };
        AutomationProperties.SetAutomationId(combo, "CountryComboBox");

        var statusText = new TextBlock
        {
            Name = "StatusLabel",
            Text = "Ready"
        };
        AutomationProperties.SetAutomationId(statusText, "StatusLabel");

        var btn = new Button
        {
            Name = "SubmitButton",
            Content = "Submit"
        };
        AutomationProperties.SetAutomationId(btn, "SubmitButton");
        btn.Click += (_, _) =>
        {
            statusText.Text = $"Submitted: {tb.Text}";
        };

        stack.Children.Add(tb);
        stack.Children.Add(cb);
        stack.Children.Add(combo);
        stack.Children.Add(btn);
        stack.Children.Add(statusText);

        window.Content = stack;
        window.Show();
        return window;
    }

    [Fact]
    public void AvaloniaDriver_CanAttach_And_FindElements()
    {
        using var driver = new AvaloniaDriver();
        var window = CreateSampleWindow();
        driver.Attach(window);

        driver.TryFindByAutomationId("UsernameTextBox").Should().BeTrue();
        driver.TryFindByAutomationId("SubmitButton").Should().BeTrue();
        driver.TryFindByAutomationId("AgreeCheckBox").Should().BeTrue();
        driver.TryFindByAutomationId("NonExistent").Should().BeFalse();
    }

    [Fact]
    public void AvaloniaDriver_TypeText_And_Click_UpdatesUiState()
    {
        using var driver = new AvaloniaDriver();
        var window = CreateSampleWindow();
        driver.Attach(window);

        driver.TypeTextByAutomationId("UsernameTextBox", "Mohamed").Should().BeTrue();
        driver.TryGetTextByAutomationId("UsernameTextBox", out var text).Should().BeTrue();
        text.Should().Be("Mohamed");

        driver.ClickByAutomationId("SubmitButton").Should().BeTrue();
        driver.TryGetTextByAutomationId("StatusLabel", out var status).Should().BeTrue();
        status.Should().Be("Submitted: Mohamed");
    }

    [Fact]
    public void AvaloniaDriver_SetCheckbox_And_SelectComboBox_WorkCorrectly()
    {
        using var driver = new AvaloniaDriver();
        var window = CreateSampleWindow();
        driver.Attach(window);

        driver.SetCheckboxByAutomationId("AgreeCheckBox", true).Should().BeTrue();
        driver.IsCheckboxChecked("AgreeCheckBox").Should().BeTrue();

        driver.SelectComboBoxItemByText("CountryComboBox", "Egypt").Should().BeTrue();
        driver.GetSelectedComboBoxItem("CountryComboBox").Should().Be("Egypt");
    }

    [Fact]
    public void AvaloniaDriver_GetCompactUiTree_ProducesCleanTokenMinimizedOutput()
    {
        using var driver = new AvaloniaDriver();
        var window = CreateSampleWindow();
        driver.Attach(window);

        var tree = driver.GetCompactUiTree("text");

        tree.Should().Contain("[TextBox #UsernameTextBox]");
        tree.Should().Contain("[Button #SubmitButton \"Submit\"]");
        tree.Should().Contain("[CheckBox #AgreeCheckBox \"I Agree\" (unchecked)]");
    }

    [Fact]
    public void AvaloniaDriver_WorksSeamlesslyWith_AgentBatchRunner()
    {
        using var driver = new AvaloniaDriver();
        var window = CreateSampleWindow();
        driver.Attach(window);

        var runner = new AgentBatchRunner(driver);
        var planJson = """
        [
          {"action": "type", "id": "UsernameTextBox", "value": "AgentUser"},
          {"action": "check", "id": "AgreeCheckBox"},
          {"action": "select", "id": "CountryComboBox", "value": "Germany"},
          {"action": "click", "id": "SubmitButton"},
          {"action": "assert_text", "id": "StatusLabel", "value": "Submitted: AgentUser"}
        ]
        """;

        var result = runner.ExecuteJson(planJson);

        result.Success.Should().BeTrue();
        result.ExecutedStepsCount.Should().Be(5);
        result.ToCompactSummary().Should().Contain("SUCCESS: 5/5 steps");
    }
}
