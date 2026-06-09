// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// OTel specification-defined environment variable names read by the SDK via
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
/// <remarks>
/// Canonical definitions exist in the core <c>OpenTelemetry</c> package as internal constants.
/// These are duplicated here because the core SDK's internal types are not accessible
/// from this package without an <c>InternalsVisibleTo</c> entry. Once the declarative config
/// API stabilises, the core SDK could expose these as public constants, or they could be moved
/// to the shared directory and this file should be removed.
/// </remarks>
internal static class OtelEnvironmentVariables
{
    /// <summary>
    /// Disables the SDK entirely when set to <c>true</c>.
    /// </summary>
    internal const string SdkDisabled = "OTEL_SDK_DISABLED";

    /// <summary>
    /// Comma-separated <c>key=value</c> (URL-encoded) resource attributes.
    /// </summary>
    internal const string ResourceAttributes = "OTEL_RESOURCE_ATTRIBUTES";

    /// <summary>
    /// Path to the declarative configuration YAML file.
    /// </summary>
    internal const string ConfigFile = "OTEL_CONFIG_FILE";
}
