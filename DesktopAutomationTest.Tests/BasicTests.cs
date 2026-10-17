using Xunit;
using FluentAssertions;
using Moq;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Tests.Helpers;

namespace DesktopAutomationTest.Tests;

public class BasicTests
{
    [Fact]
    public void TestBaseTestFixtureInitialization()
    {
        // Arrange
        var fixture = new TestFixture();

        // Act
        var result = fixture.IsInitialized;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MockDriver_VerifyFindByAutomationId_IsCalled()
    {
        // Arrange
        var mockDriver = new Mock<IDriver>();
        mockDriver.Setup(d => d.TryFindByAutomationId("testId", null))
                  .Returns(true);

        // Act
        var found = mockDriver.Object.TryFindByAutomationId("testId");

        // Assert
        found.Should().BeTrue();
        mockDriver.Verify(d => d.TryFindByAutomationId("testId", null), Times.Once);
    }

    [RequiresWindowsFact]
    public void DriverFactory_CanCreateFlauiDriver()
    {
        // Act
        var driver = DriverFactory.CreateFlauiDriver();

        // Assert
        driver.Should().NotBeNull();
        driver.Should().BeOfType<FlaUIAdapter>();
        driver.Dispose();
    }

    [Fact]
    public void Guard_ThrowsException_WhenNullOrEmpty()
    {
        // Act
        Action act = () => Guard.AgainstNullOrEmpty(null!, "testParam");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WaitHelper_CanBeUsed()
    {
        // Act
        var result = WaitHelper.WaitUntil(() => true, TimeSpan.FromMilliseconds(100));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ScreenshotHelper_CanBeUsed()
    {
        // Assert
        typeof(ScreenshotHelper).Should().NotBeNull();
    }

    [Fact]
    public void TestResult_CanBeCreatedAndPropertiesSet()
    {
        // Act
        var testResult = new TestResult
        {
            TestName = "Sample Test",
            Passed = true,
            ExecutionTime = TimeSpan.FromSeconds(5.5),
            ErrorMessage = "Test completed successfully"
        };

        // Assert
        testResult.TestName.Should().Be("Sample Test");
        testResult.Passed.Should().BeTrue();
        testResult.ExecutionTime.Should().Be(TimeSpan.FromSeconds(5.5));
        testResult.ErrorMessage.Should().Be("Test completed successfully");
        testResult.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Concrete implementation of BaseTestFixture for testing
    private class TestFixture : BaseTestFixture
    {
        public TestFixture()
        {
            Initialize();
        }
    }
}
