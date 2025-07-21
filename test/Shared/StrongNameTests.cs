// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

// The tests can only be strong named if they only depend on assemblies that are strong named,
// therefore if the tests are strong named then the libraries we ship are strong named.

public static class StrongNameTests
{
    [Fact]
    public static void Tests_Are_Strong_Named()
    {
        // Arrange
        var assembly = typeof(StrongNameTests).Assembly;
        var name = assembly.GetName();

        // Act
        var actual = name.GetPublicKey();

        // Assert
        Assert.NotNull(actual);
        Assert.NotEmpty(actual);

#if NET
        Assert.Equal(
            "002400000480000094000000060200000024000052534131000400000100010051C1562A090FB0C9F391012A32198B5E5D9A60E9B80FA2D7B434C9E5CCB7259BD606E66F9660676AFC6692B8CDC6793D190904551D2103B7B22FA636DCBB8208839785BA402EA08FC00C8F1500CCEF28BBF599AA64FFB1E1D5DC1BF3420A3777BADFE697856E9D52070A50C3EA5821C80BEF17CA3ACFFA28F89DD413F096F898",
            Convert.ToHexString(actual));
#endif

        // Act
        actual = name.GetPublicKeyToken();

        // Assert
        Assert.NotNull(actual);
        Assert.NotEmpty(actual);

#if NET
        Assert.Equal("7BD6737FE5B67E3C", Convert.ToHexString(actual));
#endif
    }
}
