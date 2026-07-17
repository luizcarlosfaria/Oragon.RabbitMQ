// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;

namespace Oragon.RabbitMQ;

/// <summary>
/// Non-destructive diagnostics for queue argument snapshots.
/// </summary>
public static class QueueArgumentDiagnostics
{
    /// <summary>
    /// Compares expected and actual queue arguments.
    /// </summary>
    /// <param name="expected">Expected queue arguments.</param>
    /// <param name="actual">Actual queue arguments.</param>
    /// <returns>Detected differences.</returns>
    public static IReadOnlyList<QueueArgumentDifference> Compare(
        IReadOnlyDictionary<string, object> expected,
        IReadOnlyDictionary<string, object> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var differences = new List<QueueArgumentDifference>();
        foreach (KeyValuePair<string, object> expectedArgument in expected)
        {
            if (actual == null || !actual.TryGetValue(expectedArgument.Key, out object actualValue))
            {
                differences.Add(new QueueArgumentDifference(
                    expectedArgument.Key,
                    expectedArgument.Value,
                    null,
                    QueueArgumentDifferenceType.Missing));
                continue;
            }

            if (!ValuesEqual(expectedArgument.Value, actualValue))
            {
                differences.Add(new QueueArgumentDifference(
                    expectedArgument.Key,
                    expectedArgument.Value,
                    actualValue,
                    QueueArgumentDifferenceType.Different));
            }
        }

        return differences;
    }

    private static bool ValuesEqual(object expected, object actual)
    {
        if (expected == null || actual == null)
        {
            return expected == actual;
        }

        if (IsNumber(expected) && IsNumber(actual))
        {
            decimal expectedNumber = Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
            decimal actualNumber = Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
            return expectedNumber == actualNumber;
        }

        if (expected is string expectedString && actual is byte[] actualBytes)
        {
            return string.Equals(expectedString, Encoding.UTF8.GetString(actualBytes), StringComparison.Ordinal);
        }

        if (expected is byte[] expectedBytes && actual is string actualString)
        {
            return string.Equals(Encoding.UTF8.GetString(expectedBytes), actualString, StringComparison.Ordinal);
        }

        return expected.Equals(actual);
    }

    private static bool IsNumber(object value) =>
        value is byte
        || value is sbyte
        || value is short
        || value is ushort
        || value is int
        || value is uint
        || value is long
        || value is ulong
        || value is float
        || value is double
        || value is decimal;
}

/// <summary>
/// Queue argument difference kind.
/// </summary>
public enum QueueArgumentDifferenceType
{
    /// <summary>
    /// The argument was expected but not found.
    /// </summary>
    Missing,

    /// <summary>
    /// The argument exists with a different value.
    /// </summary>
    Different,
}

/// <summary>
/// Queue argument diagnostic difference.
/// </summary>
public sealed record QueueArgumentDifference(
    string Key,
    object ExpectedValue,
    object ActualValue,
    QueueArgumentDifferenceType Type);
