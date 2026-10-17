using System;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Factory for creating UI automation driver instances.
/// </summary>
public static class DriverFactory
{
    /// <summary>
    /// Creates a FlaUI-based driver instance.
    /// </summary>
    /// <returns>A new FlaUIAdapter instance.</returns>
    public static IDriver CreateFlauiDriver()
    {
        return new FlaUIAdapter();
    }

    /// <summary>
    /// Creates a driver instance based on the specified type.
    /// </summary>
    /// <param name="driverType">The type of driver to create.</param>
    /// <returns>A new driver instance.</returns>
    /// <exception cref="ArgumentException">Thrown when an unsupported driver type is specified.</exception>
    public static IDriver CreateDriver(string driverType)
    {
        Guard.AgainstNullOrEmpty(driverType, nameof(driverType));
        return driverType.ToLowerInvariant() switch
        {
            "flaui" => new FlaUIAdapter(),
            _ => throw new ArgumentException($"Unsupported driver type: {driverType}", nameof(driverType))
        };
    }
}