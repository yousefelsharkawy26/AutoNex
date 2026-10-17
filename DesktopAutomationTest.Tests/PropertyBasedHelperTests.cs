using System;
using System.Linq;
using DesktopAutomationTest.Core;
using DesktopAutomationTest.Core.TestData;
using DesktopAutomationTest.Tests.Helpers;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace DesktopAutomationTest.Tests;

/// <summary>
/// Tests for <see cref="PropertyBasedHelper"/>, verifying that the FsCheck arbitraries
/// and property builders behave as documented.
/// </summary>
public class PropertyBasedHelperTests
{
    private const int SampleCount = 50;

    [Fact]
    public void UserArbitrary_GeneratesValidUsers()
    {
        var samples = PropertyBasedHelper.Sample(PropertyBasedHelper.UserArbitrary, SampleCount);

        Assert.Equal(SampleCount, samples.Count);
        Assert.All(samples, user =>
        {
            Assert.False(string.IsNullOrWhiteSpace(user.FirstName));
            Assert.False(string.IsNullOrWhiteSpace(user.LastName));
            Assert.InRange(user.Age, 0, 120);
            Assert.Contains("@", user.Email);
            Assert.Contains(".", user.Email);
            Assert.DoesNotContain(" ", user.Email);
        });
    }

    [Fact]
    public void AutomationIdArbitrary_GeneratesWhitespaceFreeIds()
    {
        var samples = PropertyBasedHelper.Sample(PropertyBasedHelper.AutomationIdArbitrary, SampleCount);

        Assert.All(samples, id =>
        {
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.DoesNotContain(' ', id);
            Assert.All(id, c => Assert.False(char.IsWhiteSpace(c)));
        });
    }

    [Fact]
    public void ValidFirstName_GeneratesBoundedNames()
    {
        var samples = PropertyBasedHelper.Sample(PropertyBasedHelper.ValidFirstName, SampleCount);

        Assert.All(samples, name =>
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.InRange(name.Length, 1, 50);
        });
    }

    [Fact]
    public void ValidAge_GeneratesAgesInHumanRange()
    {
        var samples = PropertyBasedHelper.Sample(PropertyBasedHelper.ValidAge, SampleCount);

        Assert.All(samples, age => Assert.InRange(age, 0, 120));
    }

    [Fact]
    public void ValidEmail_GeneratesWellFormedEmails()
    {
        var samples = PropertyBasedHelper.Sample(PropertyBasedHelper.ValidEmail, SampleCount);

        Assert.All(samples, email =>
        {
            Assert.False(string.IsNullOrWhiteSpace(email));
            Assert.Contains("@", email);
            Assert.Contains(".", email);
            Assert.DoesNotContain(" ", email);
        });
    }

    [Fact]
    public void ToProperty_RunsAndPassesForAHoldingPredicate()
    {
        // Act & Assert - a property that always holds must not throw.
        PropertyBasedHelper.Check(
            PropertyBasedHelper.ToProperty<int>("always even", n => (n * 2) % 2 == 0));
    }

    [Fact]
    public void ToProperty_ThrowsWhenPredicateDoesNotHold()
    {
        var property = PropertyBasedHelper.ToProperty<int>("always positive", n => n > 0);

        Assert.ThrowsAny<Exception>(() => PropertyBasedHelper.Check(property));
    }

    [Fact]
    public void DoesNotThrow_PassesForNonThrowingAction()
    {
        PropertyBasedHelper.Check(
            PropertyBasedHelper.DoesNotThrow<int>(n => { var _ = n * 2; }));
    }

    [Fact]
    public void DoesNotThrow_ThrowsWhenActionThrows()
    {
        var property = PropertyBasedHelper.DoesNotThrow<int>(n => throw new InvalidOperationException());

        Assert.ThrowsAny<Exception>(() => PropertyBasedHelper.Check(property));
    }

    [Fact]
    public void Throws_PassesForActionsThatThrow()
    {
        PropertyBasedHelper.Check(
            PropertyBasedHelper.Throws<int, ArgumentException>(
                _ => throw new ArgumentException("boom")));
    }

    [Fact]
    public void Throws_ThrowsWhenActionDoesNotThrow()
    {
        var property = PropertyBasedHelper.Throws<int, ArgumentException>(_ => { });

        Assert.ThrowsAny<Exception>(() => PropertyBasedHelper.Check(property));
    }

    // --- Property-based tests driven by our arbitraries -------------------------
    // FsCheck.Xunit 3.x has no [Property(ArbitrarySources=...)] hook, so our arbitraries
    // are bound explicitly via Prop.ForAll and executed with PropertyBasedHelper.Check.

    [Fact]
    public void Guard_AgainstNullOrEmpty_ThrowsExactlyWhenWhitespaceOnly()
    {
        PropertyBasedHelper.Check(
            FsCheck.Fluent.Prop.ForAll(
                PropertyBasedHelper.ValidString,
                value =>
                {
                    var thrown = Record.Exception(() => Guard.AgainstNullOrEmpty(value, "value"));
                    return string.IsNullOrWhiteSpace(value) == (thrown is ArgumentException);
                }));
    }

    [Fact]
    public void Guard_AgainstNull_ThrowsOnlyForNull()
    {
        PropertyBasedHelper.Check(
            FsCheck.Fluent.Prop.ForAll(
                PropertyBasedHelper.ValidString,
                value =>
                {
                    object boxed = value;
                    return Record.Exception(() => Guard.AgainstNull(boxed, "value")) is null;
                }));
    }

    [Fact]
    public void Guard_AgainstOutOfRange_ThrowsOutsideInclusiveBounds()
    {
        PropertyBasedHelper.Check(
            FsCheck.Fluent.Prop.ForAll(
                PropertyBasedHelper.Default<int>(),
                age =>
                {
                    var thrown = Record.Exception(() => Guard.AgainstOutOfRange(age, "age", 0, 120));
                    var expected = age < 0 || age > 120;
                    return expected == (thrown is ArgumentOutOfRangeException);
                }));
    }

    [Fact]
    public void UserBuilder_WithFirstName_PreservesEveryOtherProperty()
    {
        PropertyBasedHelper.Check(
            FsCheck.Fluent.Prop.ForAll(
                PropertyBasedHelper.Pair(
                    PropertyBasedHelper.ValidFirstName,
                    PropertyBasedHelper.UserArbitrary),
                pair =>
                {
                    var updated = UserBuilder.FromUser(pair.Second).WithFirstName(pair.First).Build();
                    return updated.FirstName == pair.First
                        && updated.LastName == pair.Second.LastName
                        && updated.Age == pair.Second.Age
                        && updated.Email == pair.Second.Email;
                }));
    }

    [Fact]
    public void UserBuilder_WithAge_PreservesEveryOtherProperty()
    {
        PropertyBasedHelper.Check(
            FsCheck.Fluent.Prop.ForAll(
                PropertyBasedHelper.Pair(
                    PropertyBasedHelper.UserArbitrary,
                    PropertyBasedHelper.Default<int>()),
                pair =>
                {
                    var updated = UserBuilder.FromUser(pair.First).WithAge(pair.Second).Build();
                    return updated.Age == pair.Second
                        && updated.FirstName == pair.First.FirstName
                        && updated.LastName == pair.First.LastName
                        && updated.Email == pair.First.Email;
                }));
    }
}