// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

// Carries IConfiguration across the assembly boundary without introducing a dependency on
// Microsoft.Extensions.Configuration.Abstractions in OpenTelemetry.Api.ProviderBuilderExtensions.
// Callers in layers that already reference that assembly cast Configuration to IConfiguration themselves.
internal sealed class OpenTelemetryBuilderConfigurationAccessor(object configuration)
{
    internal object Configuration { get; } = configuration;
}
