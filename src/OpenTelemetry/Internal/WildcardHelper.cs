// <copyright file="WildcardHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Text.RegularExpressions;

namespace OpenTelemetry;

internal static class WildcardHelper
{
    public static bool ContainsWildcard(string value)
    {
        if (value == null)
        {
            return false;
        }

        return value.Contains('*') || value.Contains('?');
    }

    public static Regex GetWildcardRegex(IEnumerable<string> patterns = default)
    {
        if (patterns == null)
        {
            return null;
        }

        var convertedPattern = string.Join(
            "|",
            from p in patterns select "(?:" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + ')');
        return new Regex("^(?:" + convertedPattern + ")$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
