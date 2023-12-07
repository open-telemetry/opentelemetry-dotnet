// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

internal static class AttributesExtensions
{
    public static object GetValue(this IEnumerable<KeyValuePair<string, object>> attributes, string key)
    {
        return attributes.FirstOrDefault(kvp => kvp.Key == key).Value;
    }
}
