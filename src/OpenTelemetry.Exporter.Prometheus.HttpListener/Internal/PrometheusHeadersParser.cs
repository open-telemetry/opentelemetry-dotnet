// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

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

        const int SupportedProtocols = 4;
        var preferences = new List<(PrometheusProtocol Protocol, double Quality)>(SupportedProtocols);

        var supportedEscapingSchemes = PrometheusProtocol.SupportedEscapingSchemes;
        HashSet<Version> supportedVersions;

        while (value.Length > 0)
        {
            var headerValue = TrimWhitespace(SplitNext(ref value, ','));
            var mediaType = TrimWhitespace(SplitNext(ref headerValue, ';')).ToString();

            bool isOpenMetrics;

            if (string.Equals(mediaType, PrometheusProtocol.PrometheusTextMediaType, StringComparison.OrdinalIgnoreCase))
            {
                isOpenMetrics = false;
                mediaType = PrometheusProtocol.PrometheusTextMediaType;
                supportedVersions = PrometheusProtocol.SupportedPrometheusVersions;
            }
            else
            {
                if (!string.Equals(mediaType, PrometheusProtocol.OpenMetricsMediaType, StringComparison.OrdinalIgnoreCase))
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
                    if (double.TryParse(
                        parameter.Slice(2).ToString(),
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

            if (version is null)
            {
                // Use the oldest version if no version preference was specified
                version = isOpenMetrics ? PrometheusProtocol.OpenMetricsV0 : PrometheusProtocol.PrometheusVersion0;
            }
            else if (version.Major is not > 0)
            {
                // From https://prometheus.io/docs/instrumenting/content_negotiation/#content-type-response:
                // "The Content-Type header MUST include [...] For text formats version 1.0.0 and above, the escaping scheme parameter."
                escaping = null;
            }
            else
            {
                escaping ??= PrometheusProtocol.UnderscoresEscaping;
            }

            var protocol = new PrometheusProtocol(
                mediaType,
                escaping,
                version,
                isOpenMetrics);

            preferences.Add((protocol, quality));
        }

        // Use the first supported protocol that was parsed that has the highest quality factor
        return preferences
            .OrderByDescending((p) => p.Quality)
            .Select((p) => p.Protocol)
            .DefaultIfEmpty(PrometheusProtocol.Fallback)
            .FirstOrDefault();
    }

    private static Version? GetVersion(ReadOnlySpan<char> value, HashSet<Version> supportedVersions)
    {
        var trimmed = TrimQuotes(value);

        return Version.TryParse(trimmed.ToString(), out var version) && supportedVersions.Contains(version)
            ? version
            : null;
    }

    private static string? GetEscaping(ReadOnlySpan<char> value)
    {
        var trimmed = TrimQuotes(value);
        var escaping = trimmed.ToString();

        if (PrometheusProtocol.SupportedEscapingSchemes.Contains(escaping))
        {
            return escaping;
        }

        // TODO Support other escaping schemes, including at least "allow-utf-8".
        // For now we treat "allow-utf-8" as if it were "underscores" to avoid fallback
        // to PrometheusText0.0.4 where it would previously match to OpenMetricsText1.0.0.
        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7246.
        return string.Equals(escaping, PrometheusProtocol.AllowUtf8Escaping, StringComparison.Ordinal)
            ? PrometheusProtocol.UnderscoresEscaping
            : null;
    }

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
        value.Length >= 2 &&
        value[0] == '"' &&
        value[value.Length - 1] == '"' ? value.Slice(1, value.Length - 2) : value;
}
