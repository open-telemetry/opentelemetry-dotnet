// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>Provides access to the host's IConfiguration instance for OTel extensions resolving it from DI.</summary>
internal sealed class OpenTelemetryBuilderConfigurationAccessor(object configuration)
{
    // Uses object to avoid a dependency on Microsoft.Extensions.Configuration.Abstractions in this assembly;
    // callers that already reference that package cast Configuration to IConfiguration.
    internal object Configuration { get; } = configuration;
}
