// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Security;
using System.Security.Permissions;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class EnvironmentVariablesConfigurationTests
{
    [Theory]
    [InlineData("exporter")]
    [InlineData("limits")]
    [InlineData("experimental")]
    public void DefaultOptions_WhenEnvironmentAccessDenied_UseDefaults(string component)
    {
        object result;
        new EnvironmentPermission(PermissionState.None).PermitOnly();
        try
        {
            result = component switch
            {
                "exporter" => new OtlpExporterOptions(),
                "limits" => new SdkLimitOptions(),
                "experimental" => new ExperimentalOptions(),
                _ => throw new InvalidOperationException(component),
            };
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        switch (result)
        {
            case OtlpExporterOptions options:
                Assert.Equal(10000, options.TimeoutMilliseconds);
                break;
            case SdkLimitOptions options:
                Assert.Equal(128, options.AttributeCountLimit);
                break;
            case ExperimentalOptions options:
                Assert.False(options.EnableInMemoryRetry);
                Assert.False(options.EnableDiskRetry);
                break;
        }
    }
}
#endif
