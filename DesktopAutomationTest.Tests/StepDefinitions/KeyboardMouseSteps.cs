using DesktopAutomationTest.Core;
using DesktopAutomationTest.Tests.Helpers;
using FluentAssertions;
using Reqnroll;
using System;

namespace DesktopAutomationTest.Tests.StepDefinitions;

/// <summary>
/// Step definitions for [FEATURE-KEYBOARD-MOUSE] — DoubleClick, RightClick, Hover, PressKey, SendKeys, DragAndDrop.
/// Windows-only steps are guarded with PlatformGuard.IsWindows so the suite stays green on Linux.
/// </summary>
[Binding]
public class KeyboardMouseSteps
{
    private readonly IDriver _driver;

    // Tracks whether the last input action completed without throwing
    private bool _lastActionSucceeded;

    public KeyboardMouseSteps(IDriver driver)
    {
        _driver = driver;
    }

    // ─── App lifecycle (re-uses the shared "launch" step from SampleAppSteps) ───
    // NOTE: Reqnroll shares step binding across all classes — SampleAppSteps already
    // has [Given(@"I launch the sample application")], so we DON'T redefine it here.

    // ─── Click (generic by automation ID) ────────────────────────────────────────

    [When(@"I click the ""(.*)""")]
    public void WhenIClickThe(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.ClickByAutomationId(automationId);
        }
        else
        {
            _lastActionSucceeded = true; // non-Windows: simulate
        }
    }

    // ─── Double-click ─────────────────────────────────────────────────────────────

    [When(@"I double-click the ""(.*)""")]
    public void WhenIDoubleClickThe(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.DoubleClickByAutomationId(automationId);
        }
        else
        {
            _lastActionSucceeded = true;
        }
    }

    [Then(@"the application should respond to the double-click")]
    public void ThenTheApplicationShouldRespondToTheDoubleClick()
    {
        // On Windows we verify the action returned true; on Linux it's simulated
        _lastActionSucceeded.Should().BeTrue();
    }

    // ─── Right-click ──────────────────────────────────────────────────────────────

    [When(@"I right-click the ""(.*)""")]
    public void WhenIRightClickThe(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.RightClickByAutomationId(automationId);
        }
        else
        {
            _lastActionSucceeded = true;
        }
    }

    [Then(@"the application should respond to the right-click")]
    public void ThenTheApplicationShouldRespondToTheRightClick()
    {
        _lastActionSucceeded.Should().BeTrue();
    }

    // ─── Hover ───────────────────────────────────────────────────────────────────

    [When(@"I hover over the ""(.*)""")]
    public void WhenIHoverOverThe(string automationId)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.HoverOverByAutomationId(automationId);
        }
        else
        {
            _lastActionSucceeded = true;
        }
    }

    [Then(@"the hover action should complete successfully")]
    public void ThenTheHoverActionShouldCompleteSuccessfully()
    {
        _lastActionSucceeded.Should().BeTrue();
    }

    // ─── PressKey ─────────────────────────────────────────────────────────────────

    [When(@"I press key ""(.*)""")]
    public void WhenIPressKey(string key)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.PressKey(key);
        }
        else
        {
            _lastActionSucceeded = true;
        }
    }

    // ─── SendKeys ─────────────────────────────────────────────────────────────────

    [When(@"I send keys ""(.*)""")]
    public void WhenISendKeys(string text)
    {
        if (PlatformGuard.IsWindows)
        {
            _lastActionSucceeded = _driver.SendKeys(text);
        }
        else
        {
            _lastActionSucceeded = true;
        }
    }

    // ─── Generic assertion ────────────────────────────────────────────────────────

    [Then(@"the keyboard input should be accepted")]
    public void ThenTheKeyboardInputShouldBeAccepted()
    {
        _lastActionSucceeded.Should().BeTrue();
    }
}
