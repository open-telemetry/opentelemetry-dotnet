// <copyright file="WildcardHelperTests.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class WildcardHelperTests
{
    [Theory]
    [InlineData(new[] { "a" }, "a", true)]
    [InlineData(new[] { "a.*" }, "a.b", true)]
    [InlineData(new[] { "a" }, "a.b", false)]
    [InlineData(new[] { "a", "x.*" }, "x.y", true)]
    [InlineData(new[] { "a", "x.*" }, "a.b", false)]
    [InlineData(new[] { "a", "x", "y" }, "abbbt", false)]
    [InlineData(new[] { "a", "x", "y" }, "ccxccc", false)]
    [InlineData(new[] { "a", "x", "y" }, "wecgy", false)]
    public void WildcardRegex_ShouldMatch(string[] patterns, string matchWith, bool isMatch)
    {
        var regex = WildcardHelper.GetWildcardRegex(patterns);

        var result = regex.IsMatch(matchWith);

        Assert.True(result == isMatch);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("a", false)]
    [InlineData("a.*", true)]
    [InlineData("a.?", true)]
    public void Verify_ContainsWildcard(string pattern, bool expected)
    {
        Assert.Equal(expected, WildcardHelper.ContainsWildcard(pattern));
    }
}
