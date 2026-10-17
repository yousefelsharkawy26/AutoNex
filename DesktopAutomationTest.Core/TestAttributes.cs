using System;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Provides custom test attributes for categorizing and configuring UI tests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class TestCategoryAttribute : Attribute
{
    public string CategoryName { get; }

    public TestCategoryAttribute(string categoryName)
    {
        Guard.AgainstNull(categoryName, nameof(categoryName));
        CategoryName = categoryName;
    }
}

/// <summary>
/// Indicates that a test method or class requires UI automation capabilities.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresUIAutomationAttribute : Attribute
{
    public bool IsRequired { get; set; } = true;

    public RequiresUIAutomationAttribute(bool isRequired = true)
    {
        IsRequired = isRequired;
    }
}

/// <summary>
/// Indicates that a test method or class should be run in a specific apartment state.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApartmentStateAttribute : Attribute
{
    public System.Threading.ApartmentState State { get; }

    public ApartmentStateAttribute(System.Threading.ApartmentState state)
    {
        State = state;
    }
}

/// <summary>
/// Provides timeout utilities for UI tests.
/// </summary>
public static class TestTimeouts
{
    /// <summary>
    /// Default timeout for UI operations (in milliseconds).
    /// </summary>
    public const int DefaultTimeout = 10000;

    /// <summary>
    /// Short timeout for quick UI operations (in milliseconds).
    /// </summary>
    public const int ShortTimeout = 2000;

    /// <summary>
    /// Long timeout for extended UI operations (in milliseconds).
    /// </summary>
    public const int LongTimeout = 30000;

    /// <summary>
    /// Gets a timeout value based on the operation type.
    /// </summary>
    /// <param name="operationType">The type of UI operation.</param>
    /// <returns>Timeout value in milliseconds.</returns>
    public static int GetTimeout(string operationType)
    {
        return operationType.ToLowerInvariant() switch
        {
            "short" => ShortTimeout,
            "long" => LongTimeout,
            _ => DefaultTimeout
        };
    }
}