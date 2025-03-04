// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusMetric
{
    /* Counter becomes counter
       Gauge becomes gauge
       Histogram becomes histogram
       UpDownCounter becomes gauge
     * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/data-model.md#otlp-metric-points-to-prometheus
    */
    private static readonly PrometheusType[] MetricTypes = new PrometheusType[]
    {
        PrometheusType.Untyped, PrometheusType.Counter, PrometheusType.Gauge, PrometheusType.Summary, PrometheusType.Histogram, PrometheusType.Histogram, PrometheusType.Histogram, PrometheusType.Histogram, PrometheusType.Gauge,
    };

    public PrometheusMetric(string name, string unit, PrometheusType type, bool disableTotalNameSuffixForCounters)
    {
        // The metric name is
        // required to match the regex: `[a-zA-Z_:]([a-zA-Z0-9_:])*`. Invalid characters
        // in the metric name MUST be replaced with the `_` character. Multiple
        // consecutive `_` characters MUST be replaced with a single `_` character.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L230-L233
        var sanitizedName = SanitizeMetricName(name);
        var openMetricsName = SanitizeOpenMetricsName(sanitizedName);

        string? sanitizedUnit = null;
        if (!string.IsNullOrEmpty(unit))
        {
            sanitizedUnit = GetUnit(unit);

            // The resulting unit SHOULD be added to the metric as
            // [OpenMetrics UNIT metadata](https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md#metricfamily)
            // and as a suffix to the metric name. The unit suffix comes before any type-specific suffixes.
            // https://github.com/open-telemetry/opentelemetry-specification/blob/3dfb383fe583e3b74a2365c5a1d90256b273ee76/specification/compatibility/prometheus_and_openmetrics.md#metric-metadata-1
            if (!sanitizedName.EndsWith(sanitizedUnit, StringComparison.Ordinal))
            {
                sanitizedName += $"_{sanitizedUnit}";
                openMetricsName += $"_{sanitizedUnit}";
            }
        }

        // If the metric name for monotonic Sum metric points does not end in a suffix of `_total` a suffix of `_total` MUST be added by default, otherwise the name MUST remain unchanged.
        // Exporters SHOULD provide a configuration option to disable the addition of `_total` suffixes.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L286
        // Note that we no longer append '_ratio' for units that are '1', see: https://github.com/open-telemetry/opentelemetry-specification/issues/4058
        if (type == PrometheusType.Counter && !sanitizedName.EndsWith("_total", StringComparison.Ordinal) && !disableTotalNameSuffixForCounters)
        {
            sanitizedName += "_total";
        }

        // For counters requested using OpenMetrics format, the MetricFamily name MUST be suffixed with '_total', regardless of the setting to disable the 'total' suffix.
        // https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md#counter-1
        if (type == PrometheusType.Counter && !openMetricsName.EndsWith("_total", StringComparison.Ordinal))
        {
            openMetricsName += "_total";
        }

        // In OpenMetrics format, the UNIT, TYPE and HELP metadata must be suffixed with the unit (handled above), and not the '_total' suffix, as in the case for counters.
        // https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md#unit
        var openMetricsMetadataName = type == PrometheusType.Counter
            ? SanitizeOpenMetricsName(openMetricsName)
            : sanitizedName;

        this.Name = sanitizedName;
        this.OpenMetricsName = openMetricsName;
        this.OpenMetricsMetadataName = openMetricsMetadataName;
        this.Unit = sanitizedUnit;
        this.Type = type;
    }

    public string Name { get; }

    public string OpenMetricsName { get; }

    public string OpenMetricsMetadataName { get; }

    public string? Unit { get; }

    public PrometheusType Type { get; }

    public static PrometheusMetric Create(Metric metric, bool disableTotalNameSuffixForCounters)
    {
        return new PrometheusMetric(metric.Name, metric.Unit, GetPrometheusType(metric), disableTotalNameSuffixForCounters);
    }

    internal static string SanitizeMetricName(string metricName)
    {
        StringBuilder? sb = null;
        var lastCharUnderscore = false;

        for (var i = 0; i < metricName.Length; i++)
        {
            var c = metricName[i];

            if (i == 0 && char.IsNumber(c))
            {
                sb ??= CreateStringBuilder(metricName);
                sb.Append('_');
                lastCharUnderscore = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c) && c != ':')
            {
                if (!lastCharUnderscore)
                {
                    lastCharUnderscore = true;
                    sb ??= CreateStringBuilder(metricName);
                    sb.Append('_');
                }
            }
            else
            {
                sb ??= CreateStringBuilder(metricName);
                sb.Append(c);
                lastCharUnderscore = false;
            }
        }

        return sb?.ToString() ?? metricName;

        static StringBuilder CreateStringBuilder(string name) => new StringBuilder(name.Length);
    }

    internal static string RemoveAnnotations(string unit)
    {
        // UCUM standard says the curly braces shouldn't be nested:
        // https://ucum.org/ucum#section-Character-Set-and-Lexical-Rules
        // What should happen if they are nested isn't defined.
        // Right now the remove annotations code doesn't attempt to balance multiple start and end braces.
        StringBuilder? sb = null;

        var hasOpenBrace = false;
        var startOpenBraceIndex = 0;
        var lastWriteIndex = 0;

        for (var i = 0; i < unit.Length; i++)
        {
            var c = unit[i];
            if (c == '{')
            {
                if (!hasOpenBrace)
                {
                    hasOpenBrace = true;
                    startOpenBraceIndex = i;
                }
            }
            else if (c == '}')
            {
                if (hasOpenBrace)
                {
                    sb ??= new StringBuilder();
                    sb.Append(unit, lastWriteIndex, startOpenBraceIndex - lastWriteIndex);
                    hasOpenBrace = false;
                    lastWriteIndex = i + 1;
                }
            }
        }

        if (lastWriteIndex == 0)
        {
            return unit;
        }

        Debug.Assert(sb != null, "sb was null");

        sb!.Append(unit, lastWriteIndex, unit.Length - lastWriteIndex);

        return sb.ToString();
    }

    private static string SanitizeOpenMetricsName(string metricName)
    {
        if (metricName.EndsWith("_total", StringComparison.Ordinal))
        {
            return metricName.Substring(0, metricName.Length - 6);
        }

        return metricName;
    }

    private static string GetUnit(string unit)
    {
        // Dropping the portions of the Unit within brackets (e.g. {packet}). Brackets MUST NOT be included in the resulting unit. A "count of foo" is considered unitless in Prometheus.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L238
        var updatedUnit = RemoveAnnotations(unit);

        // Converting "foo/bar" to "foo_per_bar".
        // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L240C3-L240C41
        if (TryProcessRateUnits(updatedUnit, out var updatedPerUnit))
        {
            updatedUnit = updatedPerUnit;
        }
        else
        {
            // Converting from abbreviations to full words (e.g. "ms" to "milliseconds").
            // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L237
            updatedUnit = MapUnit(updatedUnit.AsSpan());
        }

        return updatedUnit;
    }

    private static bool TryProcessRateUnits(string updatedUnit, [NotNullWhen(true)] out string? updatedPerUnit)
    {
        updatedPerUnit = null;

        for (int i = 0; i < updatedUnit.Length; i++)
        {
            if (updatedUnit[i] == '/')
            {
                // Only convert rate expressed units if it's a valid expression.
                if (i == updatedUnit.Length - 1)
                {
                    return false;
                }

                updatedPerUnit = MapUnit(updatedUnit.AsSpan(0, i)) + "_per_" + MapPerUnit(updatedUnit.AsSpan(i + 1, updatedUnit.Length - i - 1));
                return true;
            }
        }

        return false;
    }

    private static PrometheusType GetPrometheusType(Metric metric)
    {
        int metricType = (int)metric.MetricType >> 4;
        return MetricTypes[metricType];
    }

    // The map to translate OTLP units to Prometheus units
    // OTLP metrics use the c/s notation as specified at https://ucum.org/ucum.html
    // (See also https://github.com/open-telemetry/semantic-conventions/blob/main/docs/general/metrics.md#instrument-units)
    // Prometheus best practices for units: https://prometheus.io/docs/practices/naming/#base-units
    // OpenMetrics specification for units: https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md#units-and-base-units
    private static string MapUnit(ReadOnlySpan<char> unit)
    {
        return unit switch
        {
            // Time
            "d" => "days",
            "h" => "hours",
            "min" => "minutes",
            "s" => "seconds",
            "ms" => "milliseconds",
            "us" => "microseconds",
            "ns" => "nanoseconds",

            // Bytes
            "By" => "bytes",
            "KiBy" => "kibibytes",
            "MiBy" => "mebibytes",
            "GiBy" => "gibibytes",
            "TiBy" => "tibibytes",
            "KBy" => "kilobytes",
            "MBy" => "megabytes",
            "GBy" => "gigabytes",
            "TBy" => "terabytes",
            "B" => "bytes",
            "KB" => "kilobytes",
            "MB" => "megabytes",
            "GB" => "gigabytes",
            "TB" => "terabytes",

            // SI
            "m" => "meters",
            "V" => "volts",
            "A" => "amperes",
            "J" => "joules",
            "W" => "watts",
            "g" => "grams",

            // Misc
            "Cel" => "celsius",
            "Hz" => "hertz",
            "1" => string.Empty,
            "%" => "percent",
            "$" => "dollars",
            _ => unit.ToString(),
        };
    }

    // The map that translates the "per" unit
    // Example: s => per second (singular)
    private static string MapPerUnit(ReadOnlySpan<char> perUnit)
    {
        return perUnit switch
        {
            "s" => "second",
            "m" => "minute",
            "h" => "hour",
            "d" => "day",
            "w" => "week",
            "mo" => "month",
            "y" => "year",
            _ => perUnit.ToString(),
        };
    }
}
