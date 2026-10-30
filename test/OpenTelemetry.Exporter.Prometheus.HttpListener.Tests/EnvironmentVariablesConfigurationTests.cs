// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Security;
using System.Security.Permissions;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class EnvironmentVariablesConfigurationTests
{
    [Fact]
    public void DefaultOptions_WhenEnvironmentAccessDenied_UseDefaults()
    {
        PrometheusHttpListenerOptions options;
        new EnvironmentPermission(PermissionState.None).PermitOnly();
        try
        {
            options = new PrometheusHttpListenerOptions();
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        Assert.Equal("localhost", options.Host);
        Assert.Equal(9464, options.Port);
    }
}
#endif
