// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal.Tests;

public class WildcardHelperTests
{
    [Fact]
    public void GetWildcardRegex_DoesNotCatastrophicallyBacktrack()
    {
        var patterns = new[] { "*a*a*a*a*a*a*a*a*b" };
        var regex = WildcardHelper.GetWildcardRegex(patterns);

        var input = new string('a', 100);

        var sw = Stopwatch.StartNew();
        var isMatch = WildcardHelper.IsMatch(regex, input);
        sw.Stop();

        Assert.False(isMatch, "The pattern should not have matched.");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Non-matching input took {sw.Elapsed.TotalSeconds:F1}s.");
    }

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
    public void Verify_ContainsWildcard(string? pattern, bool expected)
        => Assert.Equal(expected, WildcardHelper.ContainsWildcard(pattern));
}
