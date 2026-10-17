using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DesktopAutomationTest.Core;
using VerifyXunit;
using CoreTestResult = DesktopAutomationTest.Core.TestResult;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// Thin convenience layer over Verify for snapshot and image assertions.
/// <para>
/// Approved <c>*.verified.*</c> files live under a <c>Snapshots</c> folder beside each source
/// file (configured in <see cref="ModuleInit"/>). Verify derives the snapshot location from
/// the caller file path, so these helpers forward it rather than inventing their own names.
/// </para>
/// <para>
/// Every method awaits the <see cref="VerifyTests.SettingsTask"/> Verify returns. That task is
/// lazily evaluated: dropping it without awaiting silently skips the comparison, so these
/// helpers must be awaited and their tests marked <c>async Task</c>.
/// </para>
/// </summary>
public static class SnapshotHelper
{
    /// <summary>Folder, relative to each source file, that holds approved snapshots.</summary>
    public const string SnapshotDirectoryName = "Snapshots";

    /// <summary>Asserts that <paramref name="value"/> matches its approved snapshot.</summary>
    public static Task VerifySnapshotAsync<T>(T value, [CallerFilePath] string sourceFile = "")
        => Verifier.Verify(value, sourceFile: sourceFile);

    /// <summary>Asserts that <paramref name="text"/> matches its approved snapshot.</summary>
    public static Task VerifyTextAsync(string text, [CallerFilePath] string sourceFile = "")
        => Verifier.Verify(text, sourceFile: sourceFile);

    /// <summary>Asserts that a <see cref="CoreTestResult"/> matches its approved snapshot.</summary>
    public static Task VerifyTestResultAsync(CoreTestResult result, [CallerFilePath] string sourceFile = "")
        => Verifier.Verify(result, sourceFile: sourceFile);

    /// <summary>Asserts that a collection matches its approved snapshot.</summary>
    public static Task VerifySequenceAsync<T>(IEnumerable<T> items, [CallerFilePath] string sourceFile = "")
        => Verifier.Verify(items.ToList(), sourceFile: sourceFile);

    /// <summary>
    /// Asserts that the image at <paramref name="screenshotPath"/> matches its approved
    /// snapshot. The file must exist; Verify hashes the bytes and writes the snapshot next to
    /// the calling source file.
    /// </summary>
    public static Task VerifyScreenshotAsync(string screenshotPath, [CallerFilePath] string sourceFile = "")
    {
        Guard.AgainstNullOrEmpty(screenshotPath, nameof(screenshotPath));

        if (!File.Exists(screenshotPath))
            throw new FileNotFoundException("Screenshot file not found", screenshotPath);

        return Verifier.Verify(File.ReadAllBytes(screenshotPath), sourceFile: sourceFile);
    }
}