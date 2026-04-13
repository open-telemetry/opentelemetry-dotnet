// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Api.FuzzTests;

internal static class FuzzTestHelpers
{
    public static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
        static (carrier, name) => carrier.TryGetValue(name, out var value) ? [value] : [];

    public static readonly Func<IDictionary<string, string[]>, string, IEnumerable<string>> ArrayGetter =
        static (carrier, name) => carrier.TryGetValue(name, out var value) ? value : [];

    public static readonly Func<IDictionary<string, string[]>, string, IEnumerable<string>> SinglePassArrayGetter =
        static (carrier, name) => carrier.TryGetValue(name, out var value) ? EnumerateOnce(value) : [];

    public static readonly Action<IDictionary<string, string>, string, string> Setter =
        static (carrier, name, value) => carrier[name] = value;

    public static Dictionary<string, string[]> CloneCarrier(Dictionary<string, string[]> carrier)
    {
        var clone = new Dictionary<string, string[]>(carrier.Count, StringComparer.Ordinal);

        foreach (var pair in carrier)
        {
            clone[pair.Key] = [.. pair.Value];
        }

        return clone;
    }

    public static bool CarriersEqual(Dictionary<string, string[]> left, Dictionary<string, string[]> right) =>
        left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out var value) && pair.Value.SequenceEqual(value));

    public static bool IsAllowedException(Exception ex) => ex is ArgumentException;

    private static IEnumerable<string> EnumerateOnce(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            yield return value;
        }
    }
}
