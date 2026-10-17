using System;

namespace DesktopAutomationTest.Core;

/// <summary>
/// Abstract base test fixture providing common test setup and teardown functionality.
/// </summary>
public abstract class BaseTestFixture : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the test fixture has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Initializes the test fixture.
    /// Called before each test.
    /// </summary>
    public virtual void Initialize()
    {
        // Perform common test initialization here
        IsInitialized = true;
    }

    /// <summary>
    /// Cleans up the test fixture.
    /// Called after each test.
    /// </summary>
    public virtual void Cleanup()
    {
        // Perform common test cleanup here
        IsInitialized = false;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            Cleanup();
        }
    }
}
