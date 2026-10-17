using System;
using System.IO;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// Visual regression testing on top of <see cref="ScreenshotHelper"/>.
/// <para>
/// A test supplies the current screenshot plus the name of the approved baseline. The first
/// run has no baseline, so it is created and the test passes (recording the baseline). Later
/// runs compare against it, write a diff image when they differ, and fail with an actionable
/// message naming the paths involved.
/// </para>
/// </summary>
public static class VisualRegressionHelper
{
    /// <summary>Folder (relative to the test assembly) that stores approved baselines.</summary>
    public static string BaselineDirectory { get; set; } = "Baselines";

    /// <summary>Folder (relative to the test assembly) that stores generated diff images.</summary>
    public static string DiffDirectory { get; set; } = "TestResults/VisualDiffs";

    /// <summary>
    /// Compares <paramref name="actualPath"/> against the named baseline and returns the
    /// difference percentage.
    /// </summary>
    /// <exception cref="FileNotFoundException">The actual screenshot does not exist.</exception>
    public static double Compare(string name, string actualPath, string? diffOutputPath = null)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNullOrEmpty(actualPath, nameof(actualPath));

        if (!File.Exists(actualPath))
            throw new FileNotFoundException("Actual screenshot not found", actualPath);

        var (baseline, actual) = Resolve(name, actualPath);

        if (!File.Exists(baseline))
        {
            // No approved baseline yet: record it and treat this run as passing.
            Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
            File.Copy(actual, baseline, overwrite: true);
            return 0d;
        }

        return ScreenshotHelper.CompareScreenshots(baseline, actual, diffOutputPath);
    }

    /// <summary>
    /// Asserts that <paramref name="actualPath"/> matches the named baseline within
    /// <paramref name="thresholdPercentage"/>. On failure a diff image is written and an
    /// exception is thrown explaining where the baseline, actual and diff images live.
    /// </summary>
    public static void AssertMatches(string name, string actualPath, double thresholdPercentage = 1.0)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNullOrEmpty(actualPath, nameof(actualPath));

        if (!File.Exists(actualPath))
            throw new FileNotFoundException("Actual screenshot not found", actualPath);

        var (baseline, _) = Resolve(name, actualPath);

        if (!File.Exists(baseline))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
            File.Copy(actualPath, baseline, overwrite: true);
            return;
        }

        var diffPath = Path.Combine(Root(), DiffDirectory, $"{name}-diff.png");
        var difference = ScreenshotHelper.CompareScreenshots(baseline, actualPath, diffPath);

        if (difference <= thresholdPercentage)
            return;

        throw new ImageComparisonException(
            $"Visual regression detected for '{name}': {difference:F3}% of pixels differ, " +
            $"which exceeds the {thresholdPercentage:F3}% threshold. " +
            $"Baseline: {baseline} Actual: {actualPath} Diff: {diffPath}",
            difference);
    }

    /// <summary>Overwrites the stored baseline for <paramref name="name"/>.</summary>
    public static void AcceptBaseline(string name, string actualPath)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        Guard.AgainstNullOrEmpty(actualPath, nameof(actualPath));

        if (!File.Exists(actualPath))
            throw new FileNotFoundException("Actual screenshot not found", actualPath);

        var (baseline, _) = Resolve(name, actualPath);
        Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
        File.Copy(actualPath, baseline, overwrite: true);
    }

    /// <summary>Returns true when an approved baseline already exists for <paramref name="name"/>.</summary>
    public static bool BaselineExists(string name)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        var (baseline, _) = Resolve(name, default!);
        return File.Exists(baseline);
    }

    private static (string Baseline, string Actual) Resolve(string name, string actualPath)
    {
        var baseline = Path.Combine(Root(), BaselineDirectory, name);
        return (baseline, actualPath);
    }

    private static string Root() => AppContext.BaseDirectory;
}

/// <summary>Thrown when a screenshot differs from its approved baseline beyond the threshold.</summary>
public class ImageComparisonException : Exception
{
    public ImageComparisonException(string message, double differencePercentage)
        : base(message)
    {
        DifferencePercentage = differencePercentage;
    }

    /// <summary>Percentage of pixels that differed between baseline and actual.</summary>
    public double DifferencePercentage { get; }
}