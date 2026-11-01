// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Security;
using System.Security.Permissions;

namespace OpenTelemetry.Exporter.Zipkin.Tests;

public class EnvironmentVariablesConfigurationTests
{
    [Fact]
    [Obsolete("Exercises the obsolete Zipkin exporter.")]
    public void DefaultOptions_WhenEnvironmentAccessDenied_UseDefaults()
    {
        ZipkinExporterOptions options;
        new EnvironmentPermission(PermissionState.None).PermitOnly();
        try
        {
            options = new ZipkinExporterOptions();
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        Assert.Equal(new Uri("http://localhost:9411/api/v2/spans"), options.Endpoint);
    }
}
#endif
