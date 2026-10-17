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
