// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

#if NET
using SupportedVersions = System.Collections.Immutable.ImmutableHashSet<System.Version>;
#else
using SupportedVersions = System.Collections.Generic.HashSet<System.Version>;
#endif

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusHeadersParser
{
    internal static PrometheusProtocol Negotiate(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return PrometheusProtocol.Fallback;
        }

        var value = contentType.AsSpan();

        var preferredProtocol = PrometheusProtocol.Fallback;
        var preferredQuality = 0.0;

        SupportedVersions supportedVersions;

        while (value.Length > 0)
        {
            var headerValue = TrimWhitespace(SplitNext(ref value, ','));
            var requestedMediaType = TrimWhitespace(SplitNext(ref headerValue, ';'));
            string mediaType;

            bool isOpenMetrics;

            if (requestedMediaType.Equals(PrometheusProtocol.PrometheusTextMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                isOpenMetrics = false;
                mediaType = PrometheusProtocol.PrometheusTextMediaType;
                supportedVersions = PrometheusProtocol.SupportedPrometheusVersions;
            }
            else
            {
                if (!requestedMediaType.Equals(PrometheusProtocol.OpenMetricsMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // Unsupported media type
                    continue;
                }

                isOpenMetrics = true;
                mediaType = PrometheusProtocol.OpenMetricsMediaType;
                supportedVersions = PrometheusProtocol.SupportedOpenMetricsVersions;
            }

            string? escaping = null;
            Version? version = null;

            var quality = 1.0;

            var valid = true;

            while (headerValue.Length > 0)
            {
                var parameter = TrimWhitespace(SplitNext(ref headerValue, ';'));

                if (parameter.StartsWith("q=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
#if NET
                    var qualityValue = parameter.Slice(2);
#else
                    var qualityValue = parameter.Slice(2).ToString();
#endif

                    if (double.TryParse(
                        qualityValue,
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var parsedQuality) &&
                        parsedQuality is > 0 and <= 1)
                    {
                        quality = parsedQuality;
                    }
                    else
                    {
                        valid = false;
                        break;
                    }
                }
                else if (parameter.StartsWith("version=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    version = GetVersion(parameter.Slice("version=".Length), supportedVersions);

                    if (version is null)
                    {
                        // Unsupported version
                        valid = false;
                        break;
                    }
                }
                else if (parameter.StartsWith("escaping=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    escaping = GetEscaping(parameter.Slice("escaping=".Length));

                    if (escaping is null)
                    {
                        // Unsupported escaping scheme
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid)
            {
                continue;
            }

            // Use the oldest version if no version preference was specified. Per the OpenMetrics
            // specification's negotiation rules (https://prometheus.io/docs/specs/om/open_metrics_spec/#protocol-negotiation),
            // "the standard" begins at 1.0.0 (0.0.1 predates the standard being ratified), so servers
            // MUST default to OpenMetrics 1.0.0 for an unversioned "application/openmetrics-text" entry.
            version ??= isOpenMetrics ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV0;

            if (version.Major is not > 0)
            {
                // From https://prometheus.io/docs/instrumenting/content_negotiation/#content-type-response:
                // "The Content-Type header MUST include [...] For text formats version 1.0.0 and above, the escaping scheme parameter."
                escaping = null;
            }
            else
            {
                // A request that does not negotiate an escaping scheme is treated the same as one
                // negotiating "underscores", as required by the specification's table at
                // https://github.com/open-telemetry/opentelemetry-specification/blob/51700bd58c79c057468b66c3fd8d075444d6140c/specification/metrics/sdk_exporters/prometheus.md#interaction-with-translation-strategy.
                // The exporter's translation strategy does not change what is negotiated here; it
                // is applied when names are constructed, before this scheme is layered on top.
                escaping ??= PrometheusProtocol.UnderscoresEscaping;
            }

            var protocol = new PrometheusProtocol(
                mediaType,
                escaping,
                version,
                isOpenMetrics);

            protocol.Validate();

            // Strict comparison preserves the first supported protocol when quality factors tie.
            if (quality > preferredQuality)
            {
                preferredProtocol = protocol;
                preferredQuality = quality;
            }
        }

        // Use the first supported protocol that was parsed that has the highest quality factor
        return preferredProtocol;
    }

    private static Version? GetVersion(ReadOnlySpan<char> value, SupportedVersions supportedVersions)
    {
        var trimmed = TrimQuotes(value);

#if NET
        return Version.TryParse(trimmed, out var version) && supportedVersions.Contains(version)
#else
        return Version.TryParse(trimmed.ToString(), out var version) && supportedVersions.Contains(version)
#endif
            ? version
            : null;
    }

    private static string? GetEscaping(ReadOnlySpan<char> value) => TrimQuotes(value) switch
    {
        PrometheusProtocol.AllowUtf8Escaping => PrometheusProtocol.AllowUtf8Escaping,
        PrometheusProtocol.DotsEscaping => PrometheusProtocol.DotsEscaping,
        PrometheusProtocol.UnderscoresEscaping => PrometheusProtocol.UnderscoresEscaping,
        PrometheusProtocol.ValuesEscaping => PrometheusProtocol.ValuesEscaping,
        _ => null,
    };

    private static ReadOnlySpan<char> SplitNext(ref ReadOnlySpan<char> span, char character)
    {
        var index = span.IndexOf(character);
        ReadOnlySpan<char> part;

        if (index == -1)
        {
            part = span;
            span = span.Slice(span.Length);
        }
        else
        {
            part = span.Slice(0, index);
            span = span.Slice(index + 1);
        }

        return part;
    }

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> value)
    {
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return value.Slice(start, end - start + 1);
    }

    private static ReadOnlySpan<char> TrimQuotes(ReadOnlySpan<char> value) =>
        value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"'
            ? value.Slice(1, value.Length - 2)
            : value;
}
