// <copyright file="PrometheusHeadersParser.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenTelemetry.Exporter.Prometheus;

internal static class PrometheusHeadersParser
{
    private const string OpenMetricsMediaType = "application/openmetrics-text";

    internal static bool AcceptsOpenMetrics(string contentType)
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
