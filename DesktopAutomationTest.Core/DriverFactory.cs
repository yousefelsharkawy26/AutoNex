using System;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Factory for creating UI automation driver instances.
/// </summary>
public static class DriverFactory
{
    /// <summary>
    /// Gets the default driver type for the current operating system ("flaui" on Windows, "avalonia" on Linux/macOS).
    /// </summary>
    public static string DefaultDriverType => OperatingSystem.IsWindows() ? "flaui" : "avalonia";

    /// <summary>
    /// Creates a FlaUI-based driver instance. Throws PlatformNotSupportedException on non-Windows platforms.
    /// </summary>
    /// <returns>A new FlaUIAdapter instance.</returns>
    public static IDriver CreateFlauiDriver()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "FlaUI relies on Windows UI Automation (UIA3) and is only supported on Windows. On Linux and macOS, use the 'avalonia' driver (--driver avalonia).");
        }
        return new FlaUIAdapter();
    }

    /// <summary>
    /// Creates an Avalonia-based cross-platform driver instance.
    /// </summary>
    public static IDriver CreateAvaloniaDriver()
    {
        return new AvaloniaDriver();
    }

    /// <summary>
    /// Creates a driver instance based on the specified type ("flaui", "avalonia", or "auto").
    /// </summary>
    /// <param name="driverType">The type of driver to create. Defaults to the OS-appropriate driver when omitted or "auto".</param>
    /// <returns>A new driver instance.</returns>
    /// <exception cref="ArgumentException">Thrown when an unsupported driver type is specified.</exception>
    public static IDriver CreateDriver(string? driverType = null)
    {
        var selected = string.IsNullOrWhiteSpace(driverType) || driverType.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? DefaultDriverType
            : driverType;

        return selected.ToLowerInvariant() switch
        {
            "flaui" => CreateFlauiDriver(),
            "avalonia" => CreateAvaloniaDriver(),
            _ => throw new ArgumentException($"Unsupported driver type: {driverType}. Valid options: flaui, avalonia", nameof(driverType))
        };
    }
}