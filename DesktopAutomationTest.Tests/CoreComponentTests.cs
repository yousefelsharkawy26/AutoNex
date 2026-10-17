using System;
using System.Threading;
using Xunit;
using FluentAssertions;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Comprehensive unit tests for Core library components: Guard, WaitHelper, BaseTestFixture, and TestResult.
/// </summary>
public class CoreComponentTests
{
    // =========================================================================
    // Guard Tests
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Guard_AgainstNullOrEmpty_ThrowsArgumentException_OnNullOrWhitespace(string? value)
    {
        Action act = () => Guard.AgainstNullOrEmpty(value!, "paramName");

        act.Should().Throw<ArgumentException>()
           .WithParameterName("paramName")
           .WithMessage("*Value cannot be null, empty, or white-space.*");
    }

    [Fact]
    public void Guard_AgainstNullOrEmpty_Succeeds_OnValidString()
    {
        Action act = () => Guard.AgainstNullOrEmpty("valid string", "paramName");

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_AgainstNull_ThrowsArgumentNullException_OnNull()
    {
        Action act = () => Guard.AgainstNull(null, "nullParam");

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("nullParam");
    }

    [Fact]
    public void Guard_AgainstNull_Succeeds_OnNonNullObject()
    {
        Action act = () => Guard.AgainstNull(new object(), "validParam");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(11, 1, 10)]
    [InlineData(-5, 0, 100)]
    public void Guard_AgainstOutOfRange_ThrowsArgumentOutOfRangeException_WhenOutsideBounds(int value, int min, int max)
    {
        Action act = () => Guard.AgainstOutOfRange(value, "rangeParam", min, max);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("rangeParam");
    }

    [Theory]
    [InlineData(1, 1, 10)]
    [InlineData(5, 1, 10)]
    [InlineData(10, 1, 10)]
    public void Guard_AgainstOutOfRange_Succeeds_WhenWithinBounds(int value, int min, int max)
    {
        Action act = () => Guard.AgainstOutOfRange(value, "rangeParam", min, max);

        act.Should().NotThrow();
    }

    [Fact]
    public void Guard_Against_ThrowsInvalidOperationException_WhenConditionIsFalse()
    {
        Action act = () => Guard.Against(false, "Condition failed!");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Condition failed!");
    }

    [Fact]
    public void Guard_Against_Succeeds_WhenConditionIsTrue()
    {
        Action act = () => Guard.Against(true, "Condition should not fail");

        act.Should().NotThrow();
    }

    // =========================================================================
    // WaitHelper Tests
    // =========================================================================

    [Fact]
    public void WaitHelper_WaitUntil_ThrowsWhenConditionIsNull()
    {
        Action act = () => WaitHelper.WaitUntil(null!, TimeSpan.FromMilliseconds(100));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WaitHelper_WaitUntil_ReturnsTrue_WhenConditionImmediatelyMet()
    {
        var result = WaitHelper.WaitUntil(() => true, TimeSpan.FromSeconds(1));

        result.Should().BeTrue();
    }

    [Fact]
    public void WaitHelper_WaitUntil_ReturnsTrue_AfterMultiplePolls()
    {
        var counter = 0;
        var result = WaitHelper.WaitUntil(
            () => ++counter >= 3,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(20));

        result.Should().BeTrue();
        counter.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void WaitHelper_WaitUntil_ReturnsFalse_WhenTimeoutExpires()
    {
        var result = WaitHelper.WaitUntil(
            () => false,
            TimeSpan.FromMilliseconds(60),
            TimeSpan.FromMilliseconds(20));

        result.Should().BeFalse();
    }

    [Fact]
    public void WaitHelper_WaitUntil_DefaultTimeoutOverload_Works()
    {
        var result = WaitHelper.WaitUntil(() => true);

        result.Should().BeTrue();
    }

    [Fact]
    public void WaitHelper_WaitFor_TimeSpan_SleepsExpectedDuration()
    {
        var start = DateTime.UtcNow;
        WaitHelper.WaitFor(TimeSpan.FromMilliseconds(50));
        var elapsed = DateTime.UtcNow - start;

        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public void WaitHelper_WaitFor_Milliseconds_SleepsExpectedDuration()
    {
        var start = DateTime.UtcNow;
        WaitHelper.WaitFor(50);
        var elapsed = DateTime.UtcNow - start;

        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(30));
    }

    // =========================================================================
    // BaseTestFixture Tests
    // =========================================================================

    [Fact]
    public void BaseTestFixture_Lifecycle_InitializesAndCleansUp()
    {
        var fixture = new TestableFixture();
        fixture.IsInitialized.Should().BeFalse();

        fixture.Initialize();
        fixture.IsInitialized.Should().BeTrue();

        fixture.Cleanup();
        fixture.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void BaseTestFixture_Dispose_InvokesCleanup()
    {
        var fixture = new TestableFixture();
        fixture.Initialize();
        fixture.IsInitialized.Should().BeTrue();

        fixture.Dispose();
        fixture.IsInitialized.Should().BeFalse();
    }

    private class TestableFixture : BaseTestFixture
    {
    }

    // =========================================================================
    // TestResult Tests
    // =========================================================================

    [Fact]
    public void TestResult_DefaultConstructor_SetsDefaultValues()
    {
        var result = new TestResult();

        result.TestName.Should().BeEmpty();
        result.Passed.Should().BeFalse();
        result.ExecutionTime.Should().Be(TimeSpan.Zero);
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().NotBeNull().And.BeEmpty();
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TestResult_AddMetadata_StoresAndOverwritesKey()
    {
        var result = new TestResult();
        result.AddMetadata("Key1", "Value1");
        result.AddMetadata("Key1", "Value2");
        result.AddMetadata("Key2", "Value3");

        result.Metadata.Should().HaveCount(2);
        result.Metadata["Key1"].Should().Be("Value2");
        result.Metadata["Key2"].Should().Be("Value3");
    }
}

