using System;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Provides guard methods for argument validation.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Throws an exception if the argument is null, empty, or consists only of white-space.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">Thrown when the argument is null, empty, or white-space.</exception>
    public static void AgainstNullOrEmpty(string argument, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Value cannot be null, empty, or white-space.", parameterName);
        }
    }

    /// <summary>
    /// Throws an exception if the argument is null.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
    public static void AgainstNull(object? argument, string parameterName)
    {
        if (argument == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    /// <summary>
    /// Throws an exception if the argument is out of range.
    /// </summary>
    /// <param name="argument">The argument to check.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="minValue">The minimum allowed value (inclusive).</param>
    /// <param name="maxValue">The maximum allowed value (inclusive).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the argument is out of range.</exception>
    public static void AgainstOutOfRange(int argument, string parameterName, int minValue, int maxValue)
    {
        if (argument < minValue || argument > maxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, argument, $"Value must be between {minValue} and {maxValue}.");
        }
    }

    /// <summary>
    /// Throws an exception if the argument is false.
    /// </summary>
    /// <param name="condition">The condition to check.</param>
    /// <param name="message">The error message if the condition is false.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is false.</exception>
    public static void Against(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
