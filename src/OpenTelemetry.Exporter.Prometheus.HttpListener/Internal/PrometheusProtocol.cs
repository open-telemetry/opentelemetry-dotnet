// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

#if NET8_0_OR_GREATER
using SupportedEscapingSchemes = System.Collections.Immutable.ImmutableHashSet<string>;
using SupportedVersions = System.Collections.Immutable.ImmutableHashSet<System.Version>;
#else
using SupportedEscapingSchemes = System.Collections.Generic.HashSet<string>;
using SupportedVersions = System.Collections.Generic.HashSet<System.Version>;
#endif

namespace OpenTelemetry.Exporter.Prometheus;

internal readonly struct PrometheusProtocol
{
    public const string AllowUtf8Escaping = "allow-utf-8";
    public const string UnderscoresEscaping = "underscores";

    public const string OpenMetricsMediaType = "application/openmetrics-text";
    public const string PrometheusTextMediaType = "text/plain";

    public static readonly Version PrometheusVersion0 = new(0, 0, 4);
    public static readonly Version PrometheusVersion1 = new(1, 0, 0);
    public static readonly Version OpenMetricsV0 = new(0, 0, 1);
    public static readonly Version OpenMetricsV1 = new(1, 0, 0);

    public static readonly PrometheusProtocol Fallback = new(PrometheusTextMediaType, null, PrometheusVersion0, false);

    // TODO Support other escaping schemes, including at least "allow-utf-8".
    // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7246.
    internal static readonly SupportedEscapingSchemes SupportedEscapingSchemes =
    [
        UnderscoresEscaping,
    ];

    internal static readonly SupportedVersions SupportedOpenMetricsVersions =
    [
        OpenMetricsV0,
        OpenMetricsV1,
    ];

    internal static readonly SupportedVersions SupportedPrometheusVersions =
    [
        PrometheusVersion0,
        PrometheusVersion1,
    ];

    public PrometheusProtocol(string mediaType, string? escaping, Version version, bool isOpenMetrics)
    {
        this.MediaType = mediaType;
        this.Escaping = escaping;
        this.IsOpenMetrics = isOpenMetrics;
        this.Version = version;
    }

    public readonly string MediaType { get; }

    public readonly string? Escaping { get; }

    public readonly bool IsOpenMetrics { get; }

    public readonly Version Version { get; }

    public static string GetContentType(PrometheusProtocol protocol)
    {
        var builder = new StringBuilder()
            .Append(protocol.MediaType)
            .Append("; version=")
            .Append(protocol.Version.ToString(3))
            .Append("; charset=utf-8");

        if (protocol.Escaping is not null)
        {
            builder.Append("; escaping=")
                   .Append(protocol.Escaping);
        }

        return builder.ToString();
    }
}
