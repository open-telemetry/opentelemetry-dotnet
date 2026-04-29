// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusHeadersParser
{
    private const string OpenMetricsMediaType = "application/openmetrics-text";
    private const string OpenMetricsVersion = "1.0.0";
    private const string PrometheusTextMediaType = "text/plain";

    internal static bool AcceptsOpenMetrics(string? contentType)
    {
        var value = contentType.AsSpan();
        double? bestOpenMetricsQuality = null;
        double? bestPrometheusQuality = null;

        while (value.Length > 0)
        {
            var headerValue = TrimWhitespace(SplitNext(ref value, ','));
            var mediaType = TrimWhitespace(SplitNext(ref headerValue, ';'));
            var quality = 1.0;
            var hasValidQuality = true;
            var hasSupportedOpenMetricsVersion = true;

            while (headerValue.Length > 0)
            {
                var parameter = TrimWhitespace(SplitNext(ref headerValue, ';'));

                if (!parameter.StartsWith("q=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (parameter.StartsWith("version=".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        hasSupportedOpenMetricsVersion = IsSupportedOpenMetricsVersion(parameter.Slice("version=".Length));
                    }

                    continue;
                }

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
                    hasValidQuality = false;
                }
            }

            if (!hasValidQuality)
            {
                continue;
            }

            if (mediaType.Equals(OpenMetricsMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase) && hasSupportedOpenMetricsVersion)
            {
                bestOpenMetricsQuality =
                    bestOpenMetricsQuality is not { } comparison || quality > comparison ?
                    quality :
                    bestOpenMetricsQuality ?? quality;
            }
            else if (mediaType.Equals(PrometheusTextMediaType.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                bestPrometheusQuality =
                    bestPrometheusQuality is not { } comparison || quality > comparison ?
                    quality :
                    bestPrometheusQuality ?? quality;
            }
        }

        return bestOpenMetricsQuality is { } openMetricsQuality &&
               (bestPrometheusQuality is not { } prometheusQuality || openMetricsQuality >= prometheusQuality);
    }

    private static bool IsSupportedOpenMetricsVersion(ReadOnlySpan<char> value)
        => TrimQuotes(value).Equals(OpenMetricsVersion.AsSpan(), StringComparison.Ordinal);

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
