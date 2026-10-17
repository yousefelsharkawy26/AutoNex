using System.Runtime.InteropServices;
using Xunit;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// xUnit fact that skips itself on platforms this desktop-automation project cannot run on.
/// <para>
/// Two dependencies are Windows-only:
/// <list type="bullet">
/// <item><description>FlaUI.UIA3 talks to COM (<c>UIAutomationClient</c> / <c>Accessibility</c>),
/// used by <see cref="Core.DriverFactory"/> for UI automation.</description></item>
/// <item><description><c>System.Drawing.Common</c> needs GDI+ (<c>gdiplus</c>), used by
/// <see cref="Core.ScreenshotHelper"/> for image comparison.</description></item>
/// </list>
/// Both throw at construction time elsewhere, so these tests report as skipped rather than
/// failed when the suite runs on Linux or macOS.
/// </para>
/// </summary>
public sealed class RequiresWindowsFactAttribute : FactAttribute
{
    public RequiresWindowsFactAttribute()
    {
        if (!PlatformGuard.IsWindows)
            Skip = "Requires Windows: FlaUI.UIA3 (COM UI Automation) and System.Drawing (GDI+) are Windows-only.";
    }
}

/// <summary>Theory counterpart of <see cref="RequiresWindowsFactAttribute"/>.</summary>
public sealed class RequiresWindowsTheoryAttribute : TheoryAttribute
{
    public RequiresWindowsTheoryAttribute()
    {
        if (!PlatformGuard.IsWindows)
            Skip = "Requires Windows: FlaUI.UIA3 (COM UI Automation) and System.Drawing (GDI+) are Windows-only.";
    }
}

/// <summary>Platform checks shared by the xUnit attributes and the Reqnroll step definitions.</summary>
public static class PlatformGuard
{
    /// <summary>True when running on Windows, the only platform with UI Automation and GDI+.</summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}