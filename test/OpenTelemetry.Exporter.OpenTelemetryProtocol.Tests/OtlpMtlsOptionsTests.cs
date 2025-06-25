// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpMtlsOptionsTests
{
    [Fact]
    public void DefaultValues_AreValid()
    {
        var options = new OtlpMtlsOptions();

        Assert.Null(options.ClientCertificatePath);
        Assert.Null(options.ClientKeyPath);
        Assert.Null(options.CaCertificatePath);
        Assert.True(options.EnableCertificateChainValidation);
        Assert.False(options.IsEnabled);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new OtlpMtlsOptions
        {
            ClientCertificatePath = "/path/to/client.crt",
            ClientKeyPath = "/path/to/client.key",
            CaCertificatePath = "/path/to/ca.crt",
            EnableCertificateChainValidation = false,
        };

        Assert.Equal("/path/to/client.crt", options.ClientCertificatePath);
        Assert.Equal("/path/to/client.key", options.ClientKeyPath);
        Assert.Equal("/path/to/ca.crt", options.CaCertificatePath);
        Assert.False(options.EnableCertificateChainValidation);
        Assert.True(options.IsEnabled);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenNoClientCertificateProvided()
    {
        var options = new OtlpMtlsOptions();
        Assert.False(options.IsEnabled);
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenClientCertificateFilePathProvided()
    {
        var options = new OtlpMtlsOptions { ClientCertificatePath = "/path/to/client.crt" };
        Assert.True(options.IsEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsEnabled_ReturnsFalse_WhenClientCertificateFilePathIsEmpty(string filePath)
    {
        var options = new OtlpMtlsOptions { ClientCertificatePath = filePath };
        Assert.False(options.IsEnabled);
    }
}

#endif
