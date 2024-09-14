// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusHeadersParser
{
    private const string OpenMetricsMediaType = "application/openmetrics-text";

    internal static bool AcceptsOpenMetrics(string? contentType)
    {
        var value = contentType.AsSpan();

        while (value.Length > 0)
        {
            var headerValue = SplitNext(ref value, ',');
            var mediaType = SplitNext(ref headerValue, ';');

            if (mediaType.Equals(OpenMetricsMediaType.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<char> SplitNext(ref ReadOnlySpan<char> span, char character)
    {
        var index = span.IndexOf(character);

        if (index == -1)
        {
            var part = span;
            span = span.Slice(span.Length);

            return part;
        }
        else
        {
            var part = span.Slice(0, index);
            span = span.Slice(index + 1);

            return part;
        }
    }
}
