// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;

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

    [Fact]
    public void GetWildcardRegex_MatchesLaterPatternWithoutTimingOut()
    {
        var regex = WildcardHelper.GetWildcardRegex(["*a*a*a*a*a*a*a*b", "*x"]);
        var input = new string('a', 1000) + "x";

        // A timeout in the first alternative must not prevent the matching
        // alternative from being considered. No elapsed-time assertion is needed.
        Assert.True(WildcardHelper.IsMatch(regex, input));
    }

    [Fact]
    public void GetWildcardRegex_HandlesLargeNumberOfPatterns()
    {
        var patterns = Enumerable.Range(0, 300).Select(i => $"Some.Namespace.Source.{i}.*").ToArray();

        var regex = WildcardHelper.GetWildcardRegex(patterns);

        Assert.True(WildcardHelper.IsMatch(regex, "Some.Namespace.Source.42.Foo"));
        Assert.False(WildcardHelper.IsMatch(regex, "Some.Other.Namespace"));
    }

    [Fact]
    public void GetWildcardRegex_HandlesRealWorldSourcePatternCombination()
    {
        var patterns = new[]
        {
            "SomeApplication.App",
            "AWSSDK.*",
            "System.Net.Http",
            "OpenTelemetry.Instrumentation.AWSLambda",
        };

        var regex = WildcardHelper.GetWildcardRegex(patterns);

        Assert.True(WildcardHelper.IsMatch(regex, "SomeApplication.App"));
        Assert.True(WildcardHelper.IsMatch(regex, "AWSSDK.DynamoDB"));
        Assert.False(WildcardHelper.IsMatch(regex, "Unrelated.Source"));
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
    [InlineData(new[] { "a.?" }, "a.b", true)]
    [InlineData(new[] { "a.?" }, "a.bc", false)]
    [InlineData(new[] { "a.?" }, "a.", false)]
    [InlineData(new[] { "a.?.*" }, "a.b.c", true)]
    [InlineData(new[] { "a.?.*" }, "a.bc.c", false)]
    [InlineData(new[] { "a*b*c" }, "a.b.b.c.c", true)]
    [InlineData(new[] { "a*b*c" }, "A.B.C", true)]
    [InlineData(new[] { "a*b*c" }, "a.b.c.d", false)]
    [InlineData(new[] { "a*b*c" }, "a.c.b", false)]
    [InlineData(new[] { "a*b*b" }, "ab", false)]
    [InlineData(new[] { "a*b*b" }, "abb", true)]
    [InlineData(new[] { "*ab*ab*" }, "aab.aab", true)]
    [InlineData(new[] { "*ab*ab*" }, "aab.aa", false)]
    [InlineData(new[] { "*a?c*c" }, "abcc", true)]
    [InlineData(new[] { "*a?c*c" }, "abc", false)]
    [InlineData(new[] { "a***b**c" }, "a.b.c", true)]
    [InlineData(new[] { "a***b**c" }, "a.c", false)]
    [InlineData(new[] { "**" }, "", true)]
    [InlineData(new[] { "*?*" }, "", false)]
    [InlineData(new[] { "a*b*c", "a*d" }, "a.b.d", true)]
    [InlineData(new[] { "a*b*c", "a*d" }, "a.b.e", false)]
    [InlineData(new[] { "a*b?c" }, "a\nb\nc", false)]
    [InlineData(new[] { "a*b*" }, "a.b\n\n", false)]
    [InlineData(new[] { "a*b*" }, "a.b\n", true)]
    [InlineData(new[] { "a*\nb*c" }, "a.x\nb.c", true)]
    [InlineData(new[] { "*\n*\n*" }, "a\nb\nc", true)]
    [InlineData(new[] { "a*b*c" }, "a\rb\rc", true)]
    [InlineData(new[] { "*a[0].*b(c)*" }, "x.a[0].y.b(c).z", true)]
    public void WildcardRegex_ShouldMatch(string[] patterns, string matchWith, bool isMatch)
    {
        var regex = WildcardHelper.GetWildcardRegex(patterns);

        var result = regex.IsMatch(matchWith);

        Assert.True(result == isMatch);
    }

    [Fact]
    public void GetWildcardRegex_IsCultureInvariantWhenMatchingMultiplePatterns()
    {
        using (CultureSwitcher.UseCulture("tr-TR"))
        {
            string[] patterns = ["FILE*", "Other*"];

            var regex = WildcardHelper.GetWildcardRegex(patterns);

            Assert.True(WildcardHelper.IsMatch(regex, "file.api"));
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("a", false)]
    [InlineData("a.*", true)]
    [InlineData("a.?", true)]
    public void Verify_ContainsWildcard(string? pattern, bool expected)
        => Assert.Equal(expected, WildcardHelper.ContainsWildcard(pattern));

    [Theory]
    [InlineData("", false, null)]
    [InlineData("a*", true, "a")]
    [InlineData("*", true, "")]
    [InlineData("a", false, null)]
    [InlineData("a.*.b", false, null)]
    [InlineData("a*b*", false, null)]
    [InlineData("a?*", false, null)]
    public void TryGetWildcardPrefix_ReturnsExpectedResult(string pattern, bool expectedResult, string? expectedPrefix)
    {
        var result = WildcardHelper.TryGetWildcardPrefix(pattern, out var prefix);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedPrefix, prefix);
    }
}
