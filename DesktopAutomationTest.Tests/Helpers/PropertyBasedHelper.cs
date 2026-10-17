using System;
using System.Collections.Generic;
using System.Linq;
using DesktopAutomationTest.Core.TestData;
using FsCheck;
using FsCheck.Fluent;

namespace DesktopAutomationTest.Tests.Helpers;

/// <summary>
/// Reusable arbitraries and property builders for property-based testing.
/// <para>
/// FsCheck 3.x moved <c>Arb</c>, <c>Prop</c> and the <c>Gen</c> combinators into the
/// <c>FsCheck.Fluent</c> namespace, so this file needs <c>using FsCheck.Fluent;</c>.
/// There is no <c>Arb.Create&lt;T&gt;()</c>; the default arbitrary for a type comes from
/// <c>FsCheck.FSharp.ArbMap.defaults.ArbFor&lt;T&gt;()</c>.
/// </para>
/// </summary>
public static class PropertyBasedHelper
{
    private static readonly char[] NameChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    private static readonly char[] DomainChars =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    private static readonly char[] IdChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.".ToCharArray();

    /// <summary>Generates a string of 1-50 characters containing no whitespace.</summary>
    public static Arbitrary<string> ValidFirstName => StringOf(NameChars, 1, 50);

    /// <summary>Generates any non-empty, whitespace-free string.</summary>
    public static Arbitrary<string> ValidString => StringOf(IdChars, 1, 50);

    /// <summary>Generates ages within the plausible human range (0-120).</summary>
    public static Arbitrary<int> ValidAge => Gen.Choose(0, 120).ToArbitrary();

    /// <summary>Generates syntactically valid e-mail addresses.</summary>
    public static Arbitrary<string> ValidEmail =>
        Gen.Zip(
                NameGen(3, 12),
                Gen.Elements("example.com", "test.org", "sample.net", "demo.io"),
                (local, domain) => $"{new string(local.ToArray())}@{domain}")
            .ToArbitrary();

    /// <summary>Generates automation IDs: non-empty and free of whitespace.</summary>
    public static Arbitrary<string> AutomationIdArbitrary => StringOf(IdChars, 1, 40);

    /// <summary>Generates valid <see cref="User"/> instances.</summary>
    public static Arbitrary<User> UserArbitrary =>
        ValidFirstName.Generator
            .Zip(ValidFirstName.Generator, (first, last) => (first, last))
            .Zip(ValidAge.Generator, (names, age) => (names.first, names.last, age))
            .Zip(ValidEmail.Generator, (trio, email) =>
                new User { FirstName = trio.first, LastName = trio.last, Age = trio.age, Email = email })
            .ToArbitrary();

    /// <summary>Returns the default FsCheck arbitrary for <typeparamref name="T"/>.</summary>
    public static Arbitrary<T> Default<T>() => FsCheck.FSharp.ArbMap.defaults.ArbFor<T>();

    /// <summary>
    /// Combines two arbitraries into one that generates pairs. Useful because
    /// <c>Prop.ForAll</c> with two arbitraries is awkward to call unambiguously from C#.
    /// </summary>
    public static Arbitrary<(T1 First, T2 Second)> Pair<T1, T2>(Arbitrary<T1> first, Arbitrary<T2> second) =>
        first.Zip(second);

    /// <summary>Turns a predicate into an FsCheck <see cref="Property"/>.</summary>
    public static Property ToProperty<T>(Func<T, bool> predicate) =>
        Prop.ForAll(Default<T>(), predicate);

    /// <summary>Turns a predicate into a labelled <see cref="Property"/>.</summary>
    public static Property ToProperty<T>(string name, Func<T, bool> predicate) =>
        Prop.Label(ToProperty(predicate), name);

    /// <summary>Builds a property asserting the action never throws.</summary>
    public static Property DoesNotThrow<T>(Action<T> action) =>
        Prop.ForAll(Default<T>(), x =>
        {
            try
            {
                action(x);
                return true;
            }
            catch
            {
                return false;
            }
        });

    /// <summary>Builds a property asserting the action throws the expected exception type.</summary>
    public static Property Throws<T, TException>(Action<T> action)
        where TException : Exception, new() =>
        Prop.ForAll(Default<T>(), x =>
        {
            try
            {
                action(x);
                return false;
            }
            catch (TException)
            {
                return true;
            }
        });

    /// <summary>
    /// Runs a property synchronously and throws on failure. Useful for exercising
    /// properties outside of an xUnit <c>[Property]</c> method.
    /// </summary>
    public static void Check(Property property) => property.Check(Config.QuickThrowOnFailure);

    /// <summary>Samples <paramref name="count"/> values from an arbitrary.</summary>
    public static IReadOnlyList<T> Sample<T>(Arbitrary<T> arb, int count = 10) =>
        Gen.Sample(arb.Generator, count);

    private static Arbitrary<string> StringOf(IReadOnlyList<char> alphabet, int min, int max) =>
        Gen.Choose(min, max)
            .SelectMany(size => StringGen(size, alphabet))
            .ToArbitrary();

    private static Gen<List<char>> NameGen(int min, int max) =>
        Gen.Choose(min, max).SelectMany(size => Gen.ListOf(Gen.Elements(DomainChars), size));

    private static Gen<string> StringGen(int size, IReadOnlyList<char> alphabet) =>
        Gen.ListOf(Gen.Elements(alphabet.ToArray()), size)
            .Select(chars => new string(chars.ToArray()));
}