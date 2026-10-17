using Xunit;
using FluentAssertions;
using DesktopAutomationTest.Core.TestData;

namespace DesktopAutomationTest.Tests.TestData;

public class TestDataBuilderTests
{
    [Fact]
    public void UserBuilder_CanCreateInstance()
    {
        // Act
        var builder = new UserBuilder();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<UserBuilder>();
    }
}
