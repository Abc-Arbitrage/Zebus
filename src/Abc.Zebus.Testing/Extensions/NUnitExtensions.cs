using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Abc.Zebus.Testing.Comparison;
using JetBrains.Annotations;
using KellermanSoftware.CompareNetObjects;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Abc.Zebus.Testing.Extensions;

internal delegate void MethodThatThrows();

[DebuggerStepThrough]
internal static class NUnitExtensions
{
    public static void ShouldBeFalse(this bool condition, string? message = null)
        => Assert.That(condition, Is.False, message);

    public static void ShouldBeTrue(this bool condition, string? message = null)
        => Assert.That(condition, Is.True, message);

    public static object ShouldEqual(this object actual, object expected, string? message = null)
    {
        Assert.That(actual, Is.EqualTo(expected), message);
        return expected;
    }

    public static object ShouldEqual(this IEnumerable actual, IEnumerable expected)
    {
        Assert.That(actual, Is.EqualTo(expected).AsCollection);
        return expected;
    }

    public static void ShouldEqualOneOf<T>(this T actual, params T[] expected)
        => Assert.That(new[] { actual }, Is.SubsetOf(expected));

    public static object ShouldEqualDeeply(this object actual, object expected)
    {
        var comparer = ComparisonExtensions.CreateComparer();
        var result = comparer.Compare(actual, expected);
        if (!result.AreEqual)
            Console.WriteLine(result.DifferencesString);
        Assert.That(result.AreEqual, Is.True);
        return expected;
    }

    public static DateTime ShouldApproximateDateTime(this DateTime actual, DateTime expected)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(1).Seconds);
        return expected;
    }

    public static DateTime ShouldApproximateDateTime(this DateTime actual, DateTime expected, int milliseconds)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(milliseconds).Milliseconds);
        return expected;
    }

    public static DateTime? ShouldApproximateDateTime(this DateTime? actual, DateTime expected)
    {
        actual.ShouldNotBeNull();
        return ShouldApproximateDateTime(actual!.Value, expected);
    }

    public static object ShouldEqualEpsilon(this decimal actual, decimal expected, decimal epsilon)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(epsilon));
        return expected;
    }

    public static object? ShouldEqualEpsilon(this decimal? actual, decimal? expected, decimal epsilon)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(epsilon));
        return expected;
    }

    public static object? ShouldEqualEpsilon(this decimal actual, decimal? expected, decimal epsilon)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(epsilon));
        return expected;
    }

    public static object ShouldEqualEpsilon(this double actual, double expected, double epsilon)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(epsilon));
        return expected;
    }

    public static object? ShouldEqualEpsilon(this double? actual, double? expected, double epsilon)
    {
        Assert.That(actual, Is.EqualTo(expected).Within(epsilon));
        return expected;
    }

    public static object ShouldNotEqual(this object actual, object expected)
    {
        Assert.That(actual, Is.Not.EqualTo(expected));
        return expected;
    }

    public static void ShouldBeNull(this object? anObject, string? message = null)
        => Assert.That(anObject, Is.Null, message);

    [ContractAnnotation("anObject: null => halt")]
    public static void ShouldNotBeNull(this object? anObject, string? message = null)
        => Assert.That(anObject, Is.Not.Null, message);

    public static object ShouldBeTheSameAs(this object actual, object expected)
    {
        Assert.That(actual, Is.SameAs(expected));
        return expected;
    }

    public static object ShouldNotBeTheSameAs(this object actual, object expected)
    {
        Assert.That(actual, Is.Not.SameAs(expected));
        return expected;
    }

    public static T ShouldBe<T>(this object? actual)
    {
        Assert.That(actual, Is.InstanceOf<T>());
        return (T)actual!;
    }

    public static void ShouldNotBeOfType<T>(this object? actual)
        => Assert.That(actual, Is.Not.InstanceOf<T>());

    public static void ShouldContain(this IEnumerable actual, object expected)
    {
        foreach (var obj in actual)
        {
            var empty = Tolerance.Default;
            if (new NUnitEqualityComparer().AreEqual(obj, expected, ref empty))
                return;
        }

        Assert.Fail($"'{expected}' is not present in the enumerable");
    }

    public static void ShouldContain<T>(this IEnumerable<T> actual, Expression<Func<T, bool>> predicate)
    {
        if (!actual.Any(predicate.Compile()))
            Assert.Fail("no element found matching " + predicate);
    }

    public static void ShouldNotContain<T>(this IEnumerable<T> actual, Expression<Func<T, bool>> predicate)
    {
        if (actual.Any(predicate.Compile()))
            Assert.Fail("element found matching " + predicate);
    }

    public static void ShouldNotContain(this IEnumerable actual, object expected)
    {
        foreach (var obj in actual)
        {
            var empty = Tolerance.Default;
            if (new NUnitEqualityComparer().AreEqual(obj, expected, ref empty))
                Assert.Fail($"'{expected}' is present in the enumerable");
        }
    }

    public static void ShouldBeEquivalentTo<T>(this IEnumerable<T> collection, params T[] expected)
        => ShouldBeEquivalentTo((IEnumerable)collection, expected);

    public static void ShouldBeEquivalentTo(this IEnumerable collection, IEnumerable expected, bool compareDeeply = false)
    {
        if (compareDeeply)
        {
            var compareLogic = new CompareLogic();
            collection.ShouldBeEquivalentTo(expected, (a, b) => compareLogic.Compare(a, b).AreEqual);
        }
        else
        {
            Assert.That(collection, Is.EquivalentTo(expected));
        }
    }

    public static void ShouldBeOrdered(this IEnumerable collection)
        => Assert.That(collection, Is.Ordered);

    public static void ShouldBeEquivalentTo(this IEnumerable collection, IEnumerable expected, Func<object?, object?, bool> comparer)
        => Assert.That(collection, Is.EquivalentTo(expected).Using(new EqualityComparer(comparer)));

    public static IComparable ShouldBeGreaterThan(this IComparable arg1, IComparable arg2)
    {
        Assert.That(arg1, Is.GreaterThan(arg2));
        return arg2;
    }

    public static IComparable ShouldBeGreaterOrEqualThan(this IComparable arg1, IComparable arg2)
    {
        Assert.That(arg1, Is.GreaterThanOrEqualTo(arg2));
        return arg2;
    }

    public static IComparable ShouldBeLessOrEqualThan(this IComparable arg1, IComparable arg2, string? message = null)
    {
        Assert.That(arg1, Is.LessThanOrEqualTo(arg2), message);
        return arg2;
    }

    public static IComparable ShouldBeLessThan(this IComparable arg1, IComparable arg2)
    {
        Assert.That(arg1, Is.LessThan(arg2));
        return arg2;
    }

    public static void ShouldBeEmpty<T>(this IEnumerable<T> enumerable, string? message = null)
        => Assert.That(enumerable.Any(), Is.False, message ?? "the collection is not empty");

    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> enumerable, string? message = null)
        => Assert.That(enumerable.Any(), Is.True, message ?? "the collection is empty");

    public static void ShouldBeEmpty(this string aString)
        => Assert.That(aString, Is.Empty);

    public static void ShouldNotBeEmpty(this IEnumerable collection, string? message = null)
        => Assert.That(collection, Is.Not.Empty, message);

    public static void ShouldNotBeEmpty(this string aString)
        => Assert.That(aString, Is.Not.Empty);

    public static void ShouldContain(this string actual, string expected)
        => Assert.That(actual, Does.Contain(expected));

    public static void ShouldContainIgnoreCase(this string actual, string expected)
        => Assert.That(actual, Does.Contain(expected).IgnoreCase);

    public static void ShouldNotContain(this string actual, string expected)
    {
        try
        {
            Assert.That(actual, Does.Contain(expected));
        }
        catch (AssertionException)
        {
            return;
        }

        throw new AssertionException($"\"{actual}\" should not contain \"{expected}\".");
    }

    public static string ShouldBeEqualIgnoringCase(this string actual, string expected)
    {
        Assert.That(actual, Is.EqualTo(expected).IgnoreCase);
        return expected;
    }

    public static void ShouldStartWith(this string actual, string expected)
        => Assert.That(actual, Does.StartWith(expected));

    public static void ShouldEndWith(this string actual, string expected)
        => Assert.That(actual, Does.EndWith(expected));

    public static void ShouldBeSurroundedWith(this string actual, string expectedStartDelimiter, string expectedEndDelimiter)
    {
        Assert.That(actual, Does.StartWith(expectedStartDelimiter));
        Assert.That(actual, Does.EndWith(expectedEndDelimiter));
    }

    public static void ShouldBeSurroundedWith(this string actual, string expectedDelimiter)
    {
        Assert.That(actual, Does.StartWith(expectedDelimiter));
        Assert.That(actual, Does.EndWith(expectedDelimiter));
    }

    public static void ShouldContainErrorMessage(this Exception exception, string expected)
    {
        Assert.That(exception.Message, Does.Contain(expected));
    }

    public static Exception ShouldBeThrownBy(this Type exceptionType, MethodThatThrows method)
    {
        var exception = method.GetException();

        Assert.That(exception, Is.Not.Null, $"{exceptionType.Name} was not thrown");
        Assert.That(exception!.GetType(), Is.EqualTo(exceptionType));

        return exception!;
    }

    public static void ShouldBeBetween(this DateTime actual, DateTime inferior, DateTime superior)
    {
        Assert.That(inferior, Is.LessThanOrEqualTo(actual));
        Assert.That(superior, Is.GreaterThanOrEqualTo(actual));
    }

    public static Exception? GetException(this MethodThatThrows method)
    {
        Exception? exception = null;

        try
        {
            method();
        }
        catch (Exception e)
        {
            exception = e;
        }

        return exception;
    }

    public static void ShouldBeOfType<T>(this object? actual)
        => Assert.That(actual, Is.InstanceOf<T>());

    public static void ShouldHaveSamePropertiesAs(this object actual, object expected, params string[] ignoredProperties)
    {
        var comparer = ComparisonExtensions.CreateComparer();
        comparer.Config.MembersToIgnore.AddRange(ignoredProperties);

        var result = comparer.Compare(actual, expected);
        if (!result.AreEqual)
            Assert.Fail("Properties should be equal, invalid properties: " + result.DifferencesString);
    }

    public static void ShouldHaveApproximatePropertiesAs(this object actual, object expected, params string[] ignoredProperties)
    {
        var expectedProperties = expected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(x => x.Name);
        foreach (var actualProperty in actual.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (ignoredProperties.Contains(actualProperty.Name))
                continue;

            var actualValue = actualProperty.GetValue(actual);
            object? expectedValue = null;

            if (expectedProperties.TryGetValue(actualProperty.Name, out var expectedProperty))
                expectedValue = expectedProperty.GetValue(expected);

            if (actualValue == null && expectedValue == null)
                continue;

            if (expectedValue == null)
                Assert.Fail($"Missing field or property {actualProperty.Name} on type {expected.GetType()}");

            if (!LooseEquals(expectedValue, actualValue))
                Assert.Fail($"{actualProperty.Name} should be equal, found {actualValue}, expected {expectedValue}");
        }
    }

    public static void ShouldHaveSize<T>(this IEnumerable<T> enumerable, int size)
        => enumerable.Count().ShouldEqual(size, $"Collection should contain {size} items");

    public static TSource ExpectedSingle<TSource>(this IEnumerable<TSource> source)
    {
        var items = source.ToList();
        items.Count.ShouldEqual(1, "Collection should contain only one item");

        return items[0];
    }

    public static TSource ExpectedSingle<TSource>(this IEnumerable<TSource> source, Expression<Func<TSource, bool>> predicate)
    {
        var items = source.Where(predicate.Compile()).ToList();
        items.Count.ShouldEqual(1, "Collection should contain only one item matching " + predicate);

        return items[0];
    }

    public static TSource ExpectedFirst<TSource>(this IEnumerable<TSource> source)
    {
        var items = source.ToList();
        Assert.That(items, Is.Not.Empty);

        return items[0];
    }

    private static bool LooseEquals(object? x, object? y)
    {
        if (x == y)
            return true;

        if (x == null || y == null)
            return false;

        if (x.Equals(y))
            return true;

        if ((x is DateTime) && (y is DateTime))
            return ((DateTime)x - (DateTime)y).Duration() <= TimeSpan.FromSeconds(1);

        if ((x is int) && (y is bool))
            return ((int)x == 1) == (bool)y;

        if ((x is bool) && (y is int))
            return ((int)y == 1) == (bool)x;

        try
        {
            if (Equals(Convert.ChangeType(x, y.GetType()), y))
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }

    private class EqualityComparer : IEqualityComparer
    {
        private readonly Func<object?, object?, bool> _comparer;

        public EqualityComparer(Func<object?, object?, bool> comparer)
            => _comparer = comparer;

        bool IEqualityComparer.Equals(object? x, object? y)
            => _comparer(x, y);

        public int GetHashCode(object obj)
            => obj.GetHashCode();
    }
}
