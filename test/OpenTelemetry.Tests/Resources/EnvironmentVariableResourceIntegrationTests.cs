// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Tests;

public sealed class EnvironmentVariableResourceIntegrationTests
{
    [Fact]
    public void TracerProvider_PopulatesResourceFromEnvironmentVariables()
    {
        // End-to-end smoke for the env-var > IConfiguration > Resource chain used by
        // ResourceBuilderExtensions.AddEnvironmentVariableDetector. Drives real OTEL
        // spec variables through ResourceBuilder.CreateDefault and reads the live
        // Resource off the built TracerProvider. Catches any regression that breaks
        // the pipeline between Environment and the SDK's exported resource.
        using (EnvironmentVariableScope.Create([
            ("OTEL_SERVICE_NAME", "e2e-env-var-service"),
            ("OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=test,region=eu-west")]))
        {
            using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();

            var attributes = tracerProvider.GetResource().Attributes;

            Assert.Contains(new KeyValuePair<string, object>("service.name", "e2e-env-var-service"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("deployment.environment", "test"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("region", "eu-west"), attributes);
        }
    }
}
