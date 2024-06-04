// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Resources;

internal static class ResourceSemanticConventions
{
    public const string AttributeServiceName = "service.name";
    public const string AttributeServiceNamespace = "service.namespace";
    public const string AttributeServiceInstance = "service.instance.id";
    public const string AttributeServiceVersion = "service.version";

    public const string AttributeTelemetrySdkName = "telemetry.sdk.name";
    public const string AttributeTelemetrySdkLanguage = "telemetry.sdk.language";
    public const string AttributeTelemetrySdkVersion = "telemetry.sdk.version";
}
