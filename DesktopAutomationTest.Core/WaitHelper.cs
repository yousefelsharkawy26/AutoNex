using System;
using System.Threading;
using DesktopAutomationTest.Core;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Provides helper methods for waiting in UI tests.
/// </summary>
public static class WaitHelper
{
    /// <summary>
    /// Waits until the specified condition is met or the timeout expires.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="pollingInterval">The interval between condition checks.</param>
    /// <returns><c>true</c> if the condition was met; <c>false</c> if the timeout expired.</returns>
    public static bool WaitUntil(Func<bool> condition, TimeSpan timeout, TimeSpan? pollingInterval = null)
    {
        Guard.AgainstNull(condition, nameof(condition));

        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(250);
        var endTime = DateTime.Now.Add(timeout);

        while (DateTime.Now < endTime)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(interval);
        }

        return false;
    }

    /// <summary>
    /// Waits until the specified condition is met or the default timeout expires.
    /// </summary>
    /// <param name="condition">The condition to wait for.</param>
    /// <returns><c>true</c> if the condition was met; <c>false</c> if the timeout expired.</returns>
    public static bool WaitUntil(Func<bool> condition)
    {
        return WaitUntil(condition, TimeSpan.FromMilliseconds(TestTimeouts.DefaultTimeout));
    }

    /// <summary>
    /// Waits for the specified amount of time.
    /// </summary>
    /// <param name="timeout">The time to wait.</param>
    public static void WaitFor(TimeSpan timeout)
    {
        Thread.Sleep(timeout);
    }

    /// <summary>
    /// Waits for the specified amount of time in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The time to wait in milliseconds.</param>
    public static void WaitFor(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }
}