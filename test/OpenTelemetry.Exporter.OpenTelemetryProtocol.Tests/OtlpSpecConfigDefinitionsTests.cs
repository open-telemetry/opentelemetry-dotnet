// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpSpecConfigDefinitionsTests
{
    [Fact]
    public void CertificateRevocationModeEnvVarName_HasCorrectValue()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_CERTIFICATE_REVOCATION_MODE", OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName);
    }

    [Fact]
    public void CertificateRevocationFlagEnvVarName_HasCorrectValue()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_CERTIFICATE_REVOCATION_FLAG", OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName);
    }

    [Fact]
    public void ClientKeyPasswordEnvVarName_HasCorrectValue()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_CLIENT_KEY_PASSWORD", OtlpSpecConfigDefinitions.ClientKeyPasswordEnvVarName);
    }

    [Fact]
    public void AllEnvironmentVariableNames_AreUnique()
    {
        var envVars = new[]
        {
            OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName,
            OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName,
            OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName,
            OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName,
            OtlpSpecConfigDefinitions.CertificateEnvVarName,
            OtlpSpecConfigDefinitions.ClientKeyEnvVarName,
            OtlpSpecConfigDefinitions.ClientCertificateEnvVarName,
            OtlpSpecConfigDefinitions.ClientKeyPasswordEnvVarName,
            OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName,
            OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName,
        };

        var uniqueVars = envVars.Distinct().ToArray();

        Assert.Equal(envVars.Length, uniqueVars.Length);
    }

    [Fact]
    public void CertificateRevocationEnvironmentVariables_FollowNamingConvention()
    {
        // All certificate-related environment variables should follow the OTEL_EXPORTER_OTLP_CERTIFICATE prefix
        Assert.StartsWith("OTEL_EXPORTER_OTLP_CERTIFICATE", OtlpSpecConfigDefinitions.CertificateRevocationModeEnvVarName, StringComparison.Ordinal);
        Assert.StartsWith("OTEL_EXPORTER_OTLP_CERTIFICATE", OtlpSpecConfigDefinitions.CertificateRevocationFlagEnvVarName, StringComparison.Ordinal);
    }
}

#endif
