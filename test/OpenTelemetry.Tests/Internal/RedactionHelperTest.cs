// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Internal;

public class RedactionHelperTest
{
    [Theory]
    [InlineData("?a", "?a")]
    [InlineData("?a=b", "?a=Redacted")]
    [InlineData("?a=b&", "?a=Redacted&")]
    [InlineData("?c=b&", "?c=Redacted&")]
    [InlineData("?c=a", "?c=Redacted")]
    [InlineData("?a=b&c", "?a=Redacted&c")]
    [InlineData("?a=b&c=1&", "?a=Redacted&c=Redacted&")]
    [InlineData("?a=b&c=1&a1", "?a=Redacted&c=Redacted&a1")]
    [InlineData("?a=b&c=1&a1=", "?a=Redacted&c=Redacted&a1=Redacted")]
    [InlineData("?a=b&c=11&a1=&", "?a=Redacted&c=Redacted&a1=Redacted&")]
    [InlineData("?c&c&c&", "?c&c&c&")]
    [InlineData("?a&a&a&a", "?a&a&a&a")]
    [InlineData("?&&&&&&&", "?&&&&&&&")]
    [InlineData("?c", "?c")]
    [InlineData("?=c", "?=Redacted")]
    [InlineData("?=c&=", "?=Redacted&=Redacted")]
    public void QueryStringIsRedacted(string input, string expected)
    {
        Assert.Equal(expected, RedactionHelper.GetRedactedQueryString(input));
    }
}
