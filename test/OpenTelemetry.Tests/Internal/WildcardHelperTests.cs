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
    [InlineData(new[] { @"AWSSDK\*" }, "AWSSDK*", true)]
    [InlineData(new[] { @"AWSSDK\*" }, "AWSSDK.Foo", false)]
    [InlineData(new[] { @"AWSSDK\?" }, "AWSSDK?", true)]
    [InlineData(new[] { @"AWSSDK\?" }, "AWSSDK.Foo", false)]
    [InlineData(new[] { @"AWSSDK\\*" }, @"AWSSDK\Foo", true)]
    [InlineData(new[] { @"AWSSDK\\*" }, "AWSSDKFoo", false)]
    [InlineData(new[] { @"AWSSDK\*.Sub*" }, "AWSSDK*.Sub1", true)]
    [InlineData(new[] { @"AWSSDK\*.Sub*" }, "AWSSDKFoo.Sub1", false)]
    [InlineData(new[] { @"C:\Folder\*" }, @"C:\Folder*", true)]
    [InlineData(new[] { @"C:\Folder\*" }, @"C:\FolderFoo", false)]
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
    [InlineData(@"a\*", false)]
    [InlineData(@"a\?", false)]
    [InlineData(@"a\\*", true)]
    [InlineData(@"a\\\*", false)]
    [InlineData(@"a\\?", true)]
    [InlineData(@"a\*.b*", true)]
    [InlineData(@"a\*.b\?", false)]
    [InlineData(@"C:\Folder\*", false)]
    [InlineData(@"C:\Folder\\*", true)]
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
    [InlineData(@"a\*", false, null)]
    [InlineData(@"a\\*", true, @"a\")]
    [InlineData(@"a\*b*", true, "a*b")]
    [InlineData(@"a\?b*", true, "a?b")]
    [InlineData(@"a\\\*", false, null)]
    [InlineData(@"a\\\\*", true, @"a\\")]
    public void TryGetWildcardPrefix_ReturnsExpectedResult(string pattern, bool expectedResult, string? expectedPrefix)
    {
        var result = WildcardHelper.TryGetWildcardPrefix(pattern, out var prefix);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedPrefix, prefix);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("a", "a")]
    [InlineData(@"a\*", "a*")]
    [InlineData(@"a\?", "a?")]
    [InlineData(@"a\\", @"a\")]
    [InlineData(@"C:\MyApp\*", @"C:\MyApp*")]
    [InlineData(@"C:\MyApp\\*", @"C:\MyApp\*")]
    [InlineData(@"a\b", @"a\b")]
    public void Verify_Unescape(string? pattern, string? expected)
        => Assert.Equal(expected, WildcardHelper.Unescape(pattern!));
}
