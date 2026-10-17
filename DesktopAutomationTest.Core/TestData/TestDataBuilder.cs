using System;

namespace DesktopAutomationTest.Core.TestData;

/// <summary>
/// Base class for test data builders implementing the test data builder pattern.
/// </summary>
/// <typeparam name="T">The type of object to build.</typeparam>
public abstract class TestDataBuilder<T>
{
    protected T _instance;

    protected TestDataBuilder()
    {
        _instance = CreateDefault();
    }

    /// <summary>
    /// Creates a default instance of the object to build.
    /// </summary>
    /// <returns>A default instance of T.</returns>
    protected abstract T CreateDefault();

    /// <summary>
    /// Builds and returns the object.
    /// </summary>
    /// <returns>The built object.</returns>
    public T Build()
    {
        return _instance;
    }

    /// <summary>
    /// Performs an action on the builder and returns the builder for chaining.
    /// </summary>
    /// <param name="action">The action to perform on the builder.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDataBuilder<T> And(Action<TestDataBuilder<T>> action)
    {
        action(this);
        return this;
    }
}

/// <summary>
/// Example test data builder for a simple User object.
/// </summary>
public class User
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Test data builder for User objects.
/// </summary>
public class UserBuilder : TestDataBuilder<User>
{
    protected override User CreateDefault()
    {
        return new User
        {
            FirstName = "John",
            LastName = "Doe",
            Age = 30,
            Email = "john.doe@example.com"
        };
    }

    public UserBuilder WithFirstName(string firstName)
    {
        _instance.FirstName = firstName;
        return this;
    }

    public UserBuilder WithLastName(string lastName)
    {
        _instance.LastName = lastName;
        return this;
    }

    public UserBuilder WithAge(int age)
    {
        _instance.Age = age;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _instance.Email = email;
        return this;
    }

    /// <summary>
    /// Creates a builder from an existing User object.
    /// </summary>
    /// <param name="user">The user to copy values from.</param>
    /// <returns>A new UserBuilder initialized with the user's values.</returns>
    public static UserBuilder FromUser(User user)
    {
        return new UserBuilder
        {
            _instance = new User
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Age = user.Age,
                Email = user.Email
            }
        };
    }
}

/// <summary>
/// Test data builder for TestResult objects.
/// </summary>
public class TestResultBuilder : TestDataBuilder<TestResult>
{
    protected override TestResult CreateDefault()
    {
        return new TestResult
        {
            TestName = "DefaultTest",
            Passed = true,
            ExecutionTime = TimeSpan.FromMilliseconds(500),
            ErrorMessage = null,
            Timestamp = DateTime.UtcNow
        };
    }

    public TestResultBuilder WithTestName(string testName)
    {
        _instance.TestName = testName;
        return this;
    }

    public TestResultBuilder WithPassed(bool passed)
    {
        _instance.Passed = passed;
        return this;
    }

    public TestResultBuilder WithExecutionTime(TimeSpan executionTime)
    {
        _instance.ExecutionTime = executionTime;
        return this;
    }

    public TestResultBuilder WithErrorMessage(string? errorMessage)
    {
        _instance.ErrorMessage = errorMessage;
        return this;
    }

    public TestResultBuilder WithMetadata(string key, string value)
    {
        _instance.AddMetadata(key, value);
        return this;
    }
}

/// <summary>
/// Represents configuration options for launching and running an application under test.
/// </summary>
public class AppConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool MaximizeOnStartup { get; set; }
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
}

/// <summary>
/// Test data builder for AppConfig objects.
/// </summary>
public class AppConfigBuilder : TestDataBuilder<AppConfig>
{
    protected override AppConfig CreateDefault()
    {
        return new AppConfig
        {
            ExecutablePath = "SampleApp.exe",
            Arguments = string.Empty,
            LaunchTimeout = TimeSpan.FromSeconds(10),
            MaximizeOnStartup = false,
            WindowWidth = 800,
            WindowHeight = 600
        };
    }

    public AppConfigBuilder WithExecutablePath(string path)
    {
        _instance.ExecutablePath = path;
        return this;
    }

    public AppConfigBuilder WithArguments(string args)
    {
        _instance.Arguments = args;
        return this;
    }

    public AppConfigBuilder WithLaunchTimeout(TimeSpan timeout)
    {
        _instance.LaunchTimeout = timeout;
        return this;
    }

    public AppConfigBuilder WithMaximizeOnStartup(bool maximize)
    {
        _instance.MaximizeOnStartup = maximize;
        return this;
    }

    public AppConfigBuilder WithWindowSize(int width, int height)
    {
        _instance.WindowWidth = width;
        _instance.WindowHeight = height;
        return this;
    }
}
