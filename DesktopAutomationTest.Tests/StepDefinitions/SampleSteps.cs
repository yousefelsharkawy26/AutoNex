using DesktopAutomationTest.Core;
using DesktopAutomationTest.Tests.Helpers;
using FluentAssertions;
using Reqnroll;

namespace DesktopAutomationTest.Tests.StepDefinitions;

/// <summary>
/// Step definitions for <c>SampleFeature.feature</c>.
/// <para>
/// The constructor takes <see cref="IDriver"/> to prove Reqnroll dependency injection is
/// wired up: Reqnroll resolves constructor parameters from the collection built by
/// <see cref="ReqnrollServices"/>. If that registration is missing, every scenario fails with
/// <c>ObjectContainerException</c> before the first step executes.
/// </para>
/// <para>
/// The arithmetic scenarios never call into the driver, so they run on any platform. The step
/// that asserts the driver resolved is tagged <c>@windows</c> and ignored elsewhere, because
/// FlaUI.UIA3 is COM-based and only available on Windows.
/// </para>
/// </summary>
[Binding]
public class SampleSteps
{
    private readonly IDriver _driver;
    private int _firstNumber;
    private int _secondNumber;
    private int _result;

    public SampleSteps(IDriver driver)
    {
        _driver = driver;
    }

    [Given(@"I have entered (.*) into the calculator")]
    public void GivenIHaveEnteredIntoTheCalculator(int number)
    {
        if (_firstNumber == 0)
        {
            _firstNumber = number;
        }
        else
        {
            _secondNumber = number;
        }
    }

    [When(@"I press add")]
    public void WhenIPressAdd()
    {
        _result = _firstNumber + _secondNumber;
    }

    [When(@"I press subtract")]
    public void WhenIPressSubtract()
    {
        _result = _firstNumber - _secondNumber;
    }

    [Then(@"the result should be (.*) on the screen")]
    public void ThenTheResultShouldBeOnTheScreen(int expectedResult)
    {
        _result.Should().Be(expectedResult);
    }

    [Then(@"the UI automation driver should be available")]
    public void ThenTheUiAutomationDriverShouldBeAvailable()
    {
        if (!PlatformGuard.IsWindows)
        {
            // FlaUI.UIA3 requires Windows UI Automation; ignore rather than fail off Windows.
            // Since the scenario is tagged with @windows, Reqnroll should skip the entire scenario
            // on non-Windows platforms, so we can just return early here.
            return;
        }

        _driver.Should().NotBeNull();
    }
}