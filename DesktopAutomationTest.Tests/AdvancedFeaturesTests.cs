using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.TestData;
using DesktopAutomationTest.Tests.Helpers;
using Xunit;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Demonstrates the advanced testing capabilities introduced in Milestone 3:
/// snapshot testing (Verify), visual regression baselines, property-based testing (FsCheck)
/// and the Allure reporting pipeline.
/// </summary>
[Collection("Snapshots")]
public class AdvancedFeaturesTests
{
    // ---------------------------------------------------------------- snapshots

    [Fact]
    public async Task Snapshot_UserObject_RoundTripsThroughSnapshot()
    {
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 30,
            Email = "john.doe@example.com"
        };

        await SnapshotHelper.VerifySnapshotAsync(user);
    }

    [Fact]
    public async Task Snapshot_TestResult_CapturesOutcomeAndMetadata()
    {
        var result = new TestResult
        {
            TestName = "Sample Test",
            Passed = true,
            ExecutionTime = TimeSpan.FromSeconds(5.5),
            Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        result.AddMetadata("Environment", "Test");
        result.AddMetadata("Browser", "Chrome");

        await SnapshotHelper.VerifyTestResultAsync(result);
    }

    [Fact]
    public async Task Snapshot_AutomationIdCollection_IsStable()
    {
        var ids = new[] { "SubmitButton", "CancelButton", "MainWindow", "StatusBar" };

        await SnapshotHelper.VerifySequenceAsync(ids);
    }

    [Fact]
    public async Task Snapshot_BuilderProducesDeterministicUser()
    {
        // The same builder sequence must always produce the same user.
        var user = new UserBuilder()
            .WithFirstName("Ada")
            .WithLastName("Lovelace")
            .WithAge(36)
            .WithEmail("ada@example.com")
            .Build();

        await SnapshotHelper.VerifySnapshotAsync(user);
    }

    // -------------------------------------------------------- visual regression
    // ScreenshotHelper compares images through System.Drawing, which needs GDI+ on Windows.

    [RequiresWindowsFact]
    public void VisualRegression_SameImage_MatchesBaseline()
    {
        var actual = WriteTempPng("identical", (x, y) =>
            Color(x * 7 % 256, y * 11 % 256, (x + y) % 256));

        // First call records the baseline if absent, then this second call must match it.
        VisualRegressionHelper.AssertMatches("gradient-baseline", actual);
        VisualRegressionHelper.AssertMatches("gradient-baseline", actual);
    }

    [RequiresWindowsFact]
    public void VisualRegression_DifferentImage_IsDetectedAndProducesDiff()
    {
        var basePath = WriteTempPng("regression-base", (x, y) => Color(10, 20, 30));
        VisualRegressionHelper.AssertMatches("regression-case", basePath);
        Assert.True(VisualRegressionHelper.BaselineExists("regression-case"));

        // Change a large region so the difference is well above the 1% threshold.
        var changed = WriteTempPng("regression-changed", (x, y) =>
            x < 60 ? Color(250, 250, 250) : Color(10, 20, 30));

        var exception = Assert.Throws<ImageComparisonException>(
            () => VisualRegressionHelper.AssertMatches("regression-case", changed));

        Assert.True(exception.DifferencePercentage > 1.0,
            $"Expected >1% difference but got {exception.DifferencePercentage}%");
    }

    [RequiresWindowsFact]
    public void VisualRegression_MissingActual_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => VisualRegressionHelper.AssertMatches("whatever", "/nonexistent/nope.png"));
    }

    [RequiresWindowsFact]
    public void VisualRegression_AcceptBaseline_ReplacesStoredBaseline()
    {
        var original = WriteTempPng("accept-original", (x, y) => Color(1, 2, 3));
        VisualRegressionHelper.AssertMatches("accept-case", original);

        var replacement = WriteTempPng("accept-replacement", (x, y) => Color(200, 100, 50));
        VisualRegressionHelper.AcceptBaseline("accept-case", replacement);

        // After accepting, the new image is the baseline so it must now match.
        VisualRegressionHelper.AssertMatches("accept-case", replacement);
    }

    // ------------------------------------------------------------- allure steps

    [Fact]
    public void Allure_StepsRecordNestedResults()
    {
        AllurePipeline.Label(severity: "normal", feature: "AdvancedTesting", story: "Reporting");

        var greeting = AllurePipeline.Step("compose-greeting", () =>
        {
            AllurePipeline.AttachText("note", "composing the greeting");
            return "Hello";
        });

        var shouted = AllurePipeline.Step("shout", () => greeting.ToUpperInvariant());

        Assert.Equal("HELLO", shouted);
    }

    [Fact]
    public void Allure_RecordResult_ForwardsMetadataAsParameters()
    {
        var result = new TestResult
        {
            TestName = "AllureDemo",
            Passed = true,
            ExecutionTime = TimeSpan.FromMilliseconds(250)
        };
        result.AddMetadata("Suite", "Advanced");

        AllurePipeline.RecordResult(result);

        Assert.True(result.Passed);
    }

    [Fact]
    public void Allure_InvalidSeverity_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => AllurePipeline.Label(severity: "not-a-severity"));
    }

    [Fact]
    public void Allure_FailedStep_PropagatesOriginalException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AllurePipeline.Step("explode", () => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", ex.Message);
    }

    // ------------------------------------------------------------- helpers

    // Only called from [RequiresWindowsFact] tests: System.Drawing needs GDI+ on Windows.
#pragma warning disable CA1416 // Platform compatibility
    private static string WriteTempPng(string name, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestAssets");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".png");

        using var bitmap = new System.Drawing.Bitmap(100, 100);
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                var (r, g, b) = pixel(x, y);
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(r, g, b));
            }
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }
#pragma warning restore CA1416

    private static (byte R, byte G, byte B) Color(int r, int g, int b) =>
        ((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
}