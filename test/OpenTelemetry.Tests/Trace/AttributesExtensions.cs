// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Trace.Tests;

internal static class AttributesExtensions
{
    public static object GetValue(this IEnumerable<KeyValuePair<string, object>> attributes, string key)
    {
        return attributes.FirstOrDefault(kvp => kvp.Key == key).Value;
    }

    public static void AssertAreSame(
        this IEnumerable<KeyValuePair<string, object>> attributes,
        IEnumerable<KeyValuePair<string, object>> expectedAttributes)
    {
        var expectedKeyValuePairs = expectedAttributes as KeyValuePair<string, object>[] ?? expectedAttributes.ToArray();
        var actualKeyValuePairs = attributes as KeyValuePair<string, object>[] ?? attributes.ToArray();
        Assert.Equal(actualKeyValuePairs.Length, expectedKeyValuePairs.Length);

        foreach (var attr in actualKeyValuePairs)
        {
            Assert.Contains(attr, expectedKeyValuePairs);
        }
    }
}