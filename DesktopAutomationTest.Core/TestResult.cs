using System;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Represents the result of a UI test execution.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Gets or sets the name of the test.
    /// </summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the test passed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Gets or sets the execution time of the test.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the error message if the test failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the stack trace if the test failed.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the path to a screenshot captured during the test.
    /// </summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>
    /// Gets or sets the path to a video captured during the test.
    /// </summary>
    public string? VideoPath { get; set; }

    /// <summary>
    /// Gets or sets additional metadata associated with the test result.
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the timestamp when the test was executed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Adds metadata to the test result.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void AddMetadata(string key, string value)
    {
        Guard.AgainstNullOrEmpty(key, nameof(key));

        Metadata[key] = value;
    }

    /// <summary>
    /// Gets metadata value by key.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The metadata value if found; otherwise, null.</returns>
    public string? GetMetadata(string key)
    {
        Metadata.TryGetValue(key, out var value);
        return value;
    }
}