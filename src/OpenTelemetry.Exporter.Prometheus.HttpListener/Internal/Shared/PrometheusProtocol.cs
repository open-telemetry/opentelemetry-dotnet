// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

#if NET8_0_OR_GREATER
using SupportedEscapingSchemes = System.Collections.Immutable.ImmutableHashSet<string>;
using SupportedVersions = System.Collections.Immutable.ImmutableHashSet<System.Version>;
#else
using SupportedEscapingSchemes = System.Collections.Generic.HashSet<string>;
using SupportedVersions = System.Collections.Generic.HashSet<System.Version>;
#endif

namespace OpenTelemetry.Exporter.Prometheus;

internal readonly struct PrometheusProtocol : IEquatable<PrometheusProtocol>
{
    public const string AllowUtf8Escaping = "allow-utf-8";
    public const string UnderscoresEscaping = "underscores";

    public const string OpenMetricsMediaType = "application/openmetrics-text";
    public const string PrometheusTextMediaType = "text/plain";

    public static readonly Version PrometheusV0 = new(0, 0, 4);
    public static readonly Version PrometheusV1 = new(1, 0, 0);
    public static readonly Version OpenMetricsV0 = new(0, 0, 1);
    public static readonly Version OpenMetricsV1 = new(1, 0, 0);

    public static readonly PrometheusProtocol Fallback = new(PrometheusTextMediaType, null, PrometheusV0, false);

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
        PrometheusV0,
        PrometheusV1,
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

    public bool Equals(PrometheusProtocol other)
        => this.IsOpenMetrics == other.IsOpenMetrics &&
           this.MediaType == other.MediaType &&
           this.Escaping == other.Escaping &&
           this.Version == other.Version;

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is PrometheusProtocol other && this.Equals(other);

    public override int GetHashCode()
    {
#if NET
        return HashCode.Combine(this.MediaType, this.Escaping, this.IsOpenMetrics, this.Version);
#else
        var hashCode = this.MediaType.GetHashCode();

        hashCode = (hashCode * 397) ^ (this.Escaping?.GetHashCode() ?? 0);
        hashCode = (hashCode * 397) ^ this.IsOpenMetrics.GetHashCode();
        hashCode = (hashCode * 397) ^ this.Version.GetHashCode();

        return hashCode;
#endif
    }

    public override string ToString() => GetContentType(this);

    [Conditional("DEBUG")]
    public void Validate()
    {
        // The values used to create a PrometheusProtocol should all be known and fixed values, not arbitrary values.
        // Otherwise the number of different buffers used to write metrics to could be unbounded, which could lead to
        // excessive memory usage when used to key the buffer dictionaries used in PrometheusCollectionManager.
        Debug.Assert(this.MediaType is OpenMetricsMediaType or PrometheusTextMediaType, "The specified media type is not a known value.");
        Debug.Assert(this.Escaping is null || SupportedEscapingSchemes.Contains(this.Escaping), "The specified escaping is not a known value.");
        Debug.Assert(SupportedOpenMetricsVersions.Contains(this.Version) || SupportedPrometheusVersions.Contains(this.Version), "The specified version is not a known value.");
    }
}
