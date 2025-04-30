// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class AssemblyVersionExtensionsTests
{
    [Theory]
    [InlineData("1.5.0", "1.5.0")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("1.0-beta.1", "1.0-beta.1")]
    [InlineData("1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-alpha.1.40")]
    [InlineData("1.5.0-rc.1+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-rc.1")]
    [InlineData("8.0", "8.0")]
    [InlineData("8", "8")]
    [InlineData("8.0.1.18-alpha1", "8.0.1.18-alpha1")]
    public void ParseAssemblyInformationalVersionTests(string informationalVersion, string expectedVersion)
    {
        var assembly = new TestAssembly(informationalVersion);
        var actualVersion = assembly.GetPackageVersion();

        Assert.Equal(expectedVersion, actualVersion);
    }

    private sealed class TestAssembly(string informationalVersion) : Assembly
    {
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return new Attribute[] { new AssemblyInformationalVersionAttribute(informationalVersion) };
        }
    }
}
