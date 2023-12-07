// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Xunit;

namespace OpenTelemetry.Tests;

public class SdkTests
{
    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("1.5.0", "1.5.0")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("1.0-beta.1", "1.0-beta.1")]
    [InlineData("1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-alpha.1.40")]
    [InlineData("1.5.0-rc.1+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-rc.1")]
    [InlineData("8.0", "8.0")]
    [InlineData("8", "8")]
    [InlineData("8.0.1.18-alpha1", "8.0.1.18-alpha1")]
    public void ParseAssemblyInformationalVersionTests(string? informationalVersion, string expectedVersion)
    {
        var actualVersion = Sdk.ParseAssemblyInformationalVersion(informationalVersion);

        Assert.Equal(expectedVersion, actualVersion);
    }
}
