// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// OTel specification-defined environment variable names read by the SDK via
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
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
