using System;
using Xunit;
using FluentAssertions;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.TestData;
using DesktopAutomationTest.Tests.Helpers;

namespace DesktopAutomationTest.Tests;

public class CustomAssertionExtensionsTests
{
    [Fact]
    public void BeSuccessful_PassesWhenTestPassed()
    {
        var result = new TestResultBuilder()
            .WithTestName("PassedTest")
            .WithPassed(true)
            .Build();

        result.Should().BeSuccessful();
    }

    [Fact]
    public void BeSuccessful_FailsWhenTestFailed()
    {
        var result = new TestResultBuilder()
            .WithTestName("FailedTest")
            .WithPassed(false)
            .WithErrorMessage("Null pointer")
            .Build();

        Action act = () => result.Should().BeSuccessful();
        act.Should().Throw<Exception>().WithMessage("*to have passed*");
    }

    [Fact]
    public void HaveFailed_PassesWhenTestFailed()
    {
        var result = new TestResultBuilder()
            .WithTestName("FailedTest")
            .WithPassed(false)
            .Build();

        result.Should().HaveFailed();
    }

    [Fact]
    public void HaveErrorMessage_ValidatesExactMessage()
    {
        var result = new TestResultBuilder()
            .WithTestName("ErrorTest")
            .WithPassed(false)
            .WithErrorMessage("Element not found")
            .Build();

        result.Should().HaveErrorMessage("Element not found");
    }

    [Fact]
    public void HaveCompletedWithin_ValidatesDuration()
    {
        var result = new TestResultBuilder()
            .WithExecutionTime(TimeSpan.FromSeconds(1))
            .Build();

        result.Should().HaveCompletedWithin(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void HaveMetadata_ValidatesKeyValuePair()
    {
        var result = new TestResultBuilder()
            .WithMetadata("Category", "Regression")
            .Build();

        result.Should().HaveMetadata("Category", "Regression");
    }
}

