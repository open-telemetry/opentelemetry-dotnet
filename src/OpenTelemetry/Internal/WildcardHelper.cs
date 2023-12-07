// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OpenTelemetry;

internal static class WildcardHelper
{
    public static bool ContainsWildcard(
        [NotNullWhen(true)]
        string? value)
    {
        if (value == null)
        {
            return false;
        }

        return value.Contains('*') || value.Contains('?');
    }

    public static Regex GetWildcardRegex(IEnumerable<string> patterns)
    {
        Debug.Assert(patterns?.Any() == true, "patterns was null or empty");

        var convertedPattern = string.Join(
            "|",
            from p in patterns select "(?:" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + ')');

        return new Regex("^(?:" + convertedPattern + ")$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}