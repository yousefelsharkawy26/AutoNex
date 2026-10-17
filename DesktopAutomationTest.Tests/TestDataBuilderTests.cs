using System;
using Xunit;
using FluentAssertions;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.TestData;

namespace DesktopAutomationTest.Tests.TestData;

public class TestDataBuilderTests
{
    [Fact]
    public void UserBuilder_CreatesDefaultUser()
    {
        var builder = new UserBuilder();
        var user = builder.Build();

        user.Should().NotBeNull();
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Age.Should().Be(30);
        user.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public void UserBuilder_CustomProperties_OverridesDefaults()
    {
        var user = new UserBuilder()
            .WithFirstName("Alice")
            .WithLastName("Wonderland")
            .WithAge(25)
            .WithEmail("alice@example.com")
            .Build();

        user.FirstName.Should().Be("Alice");
        user.LastName.Should().Be("Wonderland");
        user.Age.Should().Be(25);
        user.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void UserBuilder_FromUser_CopiesAllValues()
    {
        var original = new User
        {
            FirstName = "Bob",
            LastName = "Builder",
            Age = 40,
            Email = "bob@example.com"
        };

        var copy = UserBuilder.FromUser(original).WithAge(41).Build();

        copy.FirstName.Should().Be("Bob");
        copy.LastName.Should().Be("Builder");
        copy.Age.Should().Be(41);
        original.Age.Should().Be(40);
    }

    [Fact]
    public void TestResultBuilder_CreatesSuccessfulDefault()
    {
        var result = new TestResultBuilder().Build();

        result.Should().NotBeNull();
        result.Passed.Should().BeTrue();
        result.TestName.Should().Be("DefaultTest");
        result.ErrorMessage.Should().BeNull();
        result.ExecutionTime.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void TestResultBuilder_CanConfigureFailureAndMetadata()
    {
        var result = new TestResultBuilder()
            .WithTestName("CheckoutTest")
            .WithPassed(false)
            .WithErrorMessage("Button not clickable")
            .WithExecutionTime(TimeSpan.FromSeconds(2))
            .WithMetadata("Browser", "Edge")
            .Build();

        result.Passed.Should().BeFalse();
        result.TestName.Should().Be("CheckoutTest");
        result.ErrorMessage.Should().Be("Button not clickable");
        result.ExecutionTime.Should().Be(TimeSpan.FromSeconds(2));
        result.Metadata.Should().ContainKey("Browser").WhoseValue.Should().Be("Edge");
    }

    [Fact]
    public void AppConfigBuilder_CreatesDefaultConfiguration()
    {
        var config = new AppConfigBuilder().Build();

        config.ExecutablePath.Should().Be("SampleApp.exe");
        config.Arguments.Should().BeEmpty();
        config.LaunchTimeout.Should().Be(TimeSpan.FromSeconds(10));
        config.MaximizeOnStartup.Should().BeFalse();
        config.WindowWidth.Should().Be(800);
        config.WindowHeight.Should().Be(600);
    }

    [Fact]
    public void AppConfigBuilder_CustomValues_ConfiguresSuccessfully()
    {
        var config = new AppConfigBuilder()
            .WithExecutablePath("C:\\Apps\\MyApp.exe")
            .WithArguments("--env staging --debug")
            .WithLaunchTimeout(TimeSpan.FromSeconds(30))
            .WithMaximizeOnStartup(true)
            .WithWindowSize(1920, 1080)
            .Build();

        config.ExecutablePath.Should().Be("C:\\Apps\\MyApp.exe");
        config.Arguments.Should().Be("--env staging --debug");
        config.LaunchTimeout.Should().Be(TimeSpan.FromSeconds(30));
        config.MaximizeOnStartup.Should().BeTrue();
        config.WindowWidth.Should().Be(1920);
        config.WindowHeight.Should().Be(1080);
    }

    [Fact]
    public void TestDataBuilder_AndChaining_ExecutesAction()
    {
        var builder = new UserBuilder();
        builder.And(b => ((UserBuilder)b).WithFirstName("CustomName"));

        var user = builder.Build();
        user.FirstName.Should().Be("CustomName");
    }
}
