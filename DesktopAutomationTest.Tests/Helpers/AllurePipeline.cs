using System;
using System.IO;
using System.Linq;
using System.Text;
using Allure.Net.Commons;
using DesktopAutomationTest.Core;
using CoreTestResult = DesktopAutomationTest.Core.TestResult;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// Bridges the framework's own <see cref="CoreTestResult"/> into the Allure reporting
/// pipeline, so tests can emit structured steps and attachments without hard-coding Allure
/// calls at every call site.
/// </summary>
public static class AllurePipeline
{
    /// <summary>Folder (relative to the working directory) that Allure results are written to.</summary>
    public const string ResultsDirectory = "allure-results";

    /// <summary>Runs <paramref name="action"/> as a named Allure step and returns its result.</summary>
    /// <remarks>
    /// A failing step is recorded as a broken Allure step with the exception text attached,
    /// then the original exception is rethrown so the test still fails.
    /// </remarks>
    public static T Step<T>(string name, Func<T> action)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNull(action, nameof(action));

        return AllureApi.Step(name, () =>
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                AllureApi.AddAttachment(
                    $"step-error-{name}",
                    "text/plain",
                    Encoding.UTF8.GetBytes(ex.ToString()),
                    ".txt");
                throw;
            }
        });
    }

    /// <summary>Runs <paramref name="action"/> as a named Allure step, discarding the result.</summary>
    public static void Step(string name, Action action)
    {
        Guard.AgainstNull(action, nameof(action));

        Step<object?>(name, () =>
        {
            action();
            return null;
        });
    }

    /// <summary>Attaches a file from disk to the current Allure step or test.</summary>
    public static void AttachFile(string path)
    {
        Guard.AgainstNullOrEmpty(path, nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Attachment not found", path);

        AllureApi.AddAttachment(Path.GetFileName(path), path);
    }

    /// <summary>Attaches plain text to the current Allure step or test.</summary>
    public static void AttachText(string name, string content)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNull(content, nameof(content));

        AllureApi.AddAttachment(
            name,
            "text/plain",
            Encoding.UTF8.GetBytes(content),
            ".txt");
    }

    /// <summary>
    /// Records a completed <see cref="CoreTestResult"/> against the current Allure test,
    /// adding its outcome, execution time and metadata as test parameters, and attaching any
    /// screenshot it references.
    /// </summary>
    public static void RecordResult(CoreTestResult result)
    {
        Guard.AgainstNull(result, nameof(result));

        AllureApi.AddTestParameter("TestName", result.TestName);
        AllureApi.AddTestParameter("Passed", result.Passed);
        AllureApi.AddTestParameter("ExecutionTime", result.ExecutionTime.ToString());

        foreach (var (key, value) in result.Metadata)
            AllureApi.AddTestParameter(key, value);

        if (result.ScreenshotPath is { } screenshot && File.Exists(screenshot))
            AttachFile(screenshot);
    }

    /// <summary>Attaches a baseline/actual/diff triple as an Allure screen diff.</summary>
    public static void AttachScreenDiff(string baselinePath, string actualPath, string diffPath)
    {
        Guard.AgainstNullOrEmpty(baselinePath, nameof(baselinePath));
        Guard.AgainstNullOrEmpty(actualPath, nameof(actualPath));
        Guard.AgainstNullOrEmpty(diffPath, nameof(diffPath));

        foreach (var (label, path) in new[] { ("baseline", baselinePath), ("actual", actualPath), ("diff", diffPath) })
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Visual regression {label} image not found", path);
        }

        AllureApi.AddScreenDiff(baselinePath, actualPath, diffPath);
    }

    /// <summary>Labels the current Allure test with severity, feature, story and tags.</summary>
    public static void Label(string? severity = null, string? feature = null, string? story = null, params string[] tags)
    {
        if (!string.IsNullOrWhiteSpace(severity))
            AllureApi.SetSeverity(ParseSeverity(severity!));

        if (!string.IsNullOrWhiteSpace(feature))
            AllureApi.AddFeature(feature!);

        if (!string.IsNullOrWhiteSpace(story))
            AllureApi.AddStory(story!);

        var clean = tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        if (clean.Length > 0)
            AllureApi.AddTags(clean);
    }

    private static SeverityLevel ParseSeverity(string value) =>
        value.ToLowerInvariant() switch
        {
            "blocker" => SeverityLevel.blocker,
            "critical" => SeverityLevel.critical,
            "normal" => SeverityLevel.normal,
            "minor" => SeverityLevel.minor,
            "trivial" => SeverityLevel.trivial,
            _ => throw new ArgumentException(
                $"Unknown severity '{value}'. Expected one of: blocker, critical, normal, minor, trivial.",
                nameof(value))
        };
}