using System;
using DesktopAutomationTest.Core;
using FluentAssertions;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// Custom FluentAssertions extensions for domain models in the desktop automation framework.
/// </summary>
public static class CustomAssertionExtensions
{
    public static TestResultAssertions Should(this TestResult? instance)
    {
        return new TestResultAssertions(instance);
    }
}

/// <summary>
/// Custom assertion wrapper for <see cref="TestResult"/>.
/// </summary>
public class TestResultAssertions
{
    private readonly TestResult? _subject;

    public TestResultAssertions(TestResult? subject)
    {
        _subject = subject;
    }

    private TestResult EnsureSubjectNotNull(string because)
    {
        if (_subject is null)
        {
            var reason = string.IsNullOrWhiteSpace(because) ? "." : $" because {because}.";
            throw new InvalidOperationException($"Expected test result not to be null{reason}");
        }
        return _subject;
    }

    public TestResultAssertions BeSuccessful(string because = "")
    {
        var subject = EnsureSubjectNotNull(because);
        var reason = string.IsNullOrWhiteSpace(because) ? "test to have passed" : because;
        subject.Passed.Should().BeTrue(reason);
        return this;
    }

    public TestResultAssertions HaveFailed(string because = "")
    {
        var subject = EnsureSubjectNotNull(because);
        var reason = string.IsNullOrWhiteSpace(because) ? "test to have failed" : because;
        subject.Passed.Should().BeFalse(reason);
        return this;
    }

    public TestResultAssertions HaveErrorMessage(string expectedError, string because = "")
    {
        var subject = EnsureSubjectNotNull(because);
        subject.ErrorMessage.Should().Be(expectedError, because);
        return this;
    }

    public TestResultAssertions HaveCompletedWithin(TimeSpan maxDuration, string because = "")
    {
        var subject = EnsureSubjectNotNull(because);
        subject.ExecutionTime.Should().BeLessThanOrEqualTo(maxDuration, because);
        return this;
    }

    public TestResultAssertions HaveMetadata(string key, string expectedValue, string because = "")
    {
        var subject = EnsureSubjectNotNull(because);
        subject.Metadata.Should().ContainKey(key, because)
            .WhoseValue.Should().Be(expectedValue, because);
        return this;
    }
}
