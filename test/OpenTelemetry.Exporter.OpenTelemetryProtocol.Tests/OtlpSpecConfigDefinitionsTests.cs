// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpSpecConfigDefinitionsTests
{
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
        };

        var uniqueVars = envVars.Distinct().ToArray();

        Assert.Equal(envVars.Length, uniqueVars.Length);
    }
}

#endif
