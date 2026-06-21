// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusMetric
{
    private readonly string rawName;
    private readonly bool disableTotalNameSuffixForCounters;
    private readonly NameSet underscoreNames;
    private NameSet? dotsNames;
    private NameSet? valuesNames;

    public PrometheusMetric(string name, string unit, PrometheusType type, bool disableTotalNameSuffixForCounters)
    {
        this.rawName = name;
        this.disableTotalNameSuffixForCounters = disableTotalNameSuffixForCounters;
        this.Type = type;

        var sanitizedUnit = string.IsNullOrEmpty(unit) ? null : GetUnit(unit);
        this.Unit = sanitizedUnit;
        this.UnitBytes = sanitizedUnit == null ? null : ConvertToBytes(sanitizedUnit);

        // The underscores names are always required (they are also used by the legacy text
        // formats, which have no negotiated escaping) so they are computed eagerly. Other
        // escapings' name sets are computed lazily on first use, so the overhead of an
        // escaping scheme is only paid when that scheme is actually scraped from the server.
        this.underscoreNames = BuildUnderscoreNames(name, sanitizedUnit, type, disableTotalNameSuffixForCounters);
    }

    public string Name => this.underscoreNames.Name;

    public string OpenMetricsName => this.underscoreNames.OpenMetricsName;

    public string OpenMetricsMetadataName => this.underscoreNames.OpenMetricsMetadataName;

    public string? Unit { get; }

    public PrometheusType Type { get; }

    internal byte[] NameBytes => this.underscoreNames.NameBytes;

    internal byte[] OpenMetricsNameBytes => this.underscoreNames.OpenMetricsNameBytes;

    internal byte[] OpenMetricsMetadataNameBytes => this.underscoreNames.OpenMetricsMetadataNameBytes;

    internal byte[]? UnitBytes { get; }

    public static PrometheusMetric Create(Metric metric, bool disableTotalNameSuffixForCounters)
        => new(metric.Name, metric.Unit, GetPrometheusType(metric.MetricType), disableTotalNameSuffixForCounters);

    internal static string SanitizeMetricUnit(string metricUnit)
    {
        StringBuilder? sb = null;
        var lastCharUnderscore = false;

        for (var i = 0; i < metricUnit.Length; i++)
        {
            var c = metricUnit[i];

            if (!char.IsAsciiLetterOrDigit(c) && c != ':')
            {
                if (!lastCharUnderscore)
                {
                    lastCharUnderscore = true;
                    sb ??= new StringBuilder(metricUnit, 0, i, metricUnit.Length);
                    sb.Append('_');
                }
            }
            else
            {
                sb?.Append(c);
                lastCharUnderscore = false;
            }
        }

        var result = sb?.ToString() ?? metricUnit;
        return result.Trim('_');
    }

    internal static string SanitizeMetricName(string metricName)
    {
        StringBuilder? sb = null;
        var lastCharUnderscore = false;

        for (var i = 0; i < metricName.Length; i++)
        {
            var c = metricName[i];

            if (i == 0 && char.IsAsciiDigit(c))
            {
                sb ??= CreateStringBuilder(metricName);
                sb.Append('_');
                lastCharUnderscore = true;
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != ':')
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

        static StringBuilder CreateStringBuilder(string value)
        {
            return new(value.Length);
        }
    }

    internal static string EscapeOpenMetricsName(string metricName)
    {
        StringBuilder? sb = null;
        var lastCharUnderscore = false;

        for (var i = 0; i < metricName.Length; i++)
        {
            var c = metricName[i];

            if (i == 0 && char.IsAsciiDigit(c))
            {
                sb ??= CreateStringBuilder(metricName);
                sb.Append('_');
                lastCharUnderscore = true;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != ':')
            {
                if (!lastCharUnderscore)
                {
                    sb ??= CreateStringBuilder(metricName);
                    sb.Append('_');
                    lastCharUnderscore = true;
                }
            }
            else
            {
                sb ??= CreateStringBuilder(metricName);
                sb.Append(c);
                lastCharUnderscore = c == '_';
            }
        }

        return sb?.ToString() ?? metricName;

        static StringBuilder CreateStringBuilder(string value)
        {
            return new(value.Length + 1);
        }
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

        sb!.Append(unit, lastWriteIndex, unit.Length - lastWriteIndex);

        return sb.ToString();
    }

    internal static PrometheusType GetPrometheusType(MetricType openTelemetryMetricType)
    {
        var metricType = (int)openTelemetryMetricType >> 4;

        /* Counter becomes counter
           Gauge becomes gauge
           Histogram becomes histogram
           UpDownCounter becomes gauge
         * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/data-model.md#otlp-metric-points-to-prometheus
        */
        return metricType switch
        {
            0 => PrometheusType.Untyped,
            1 => PrometheusType.Counter,
            2 => PrometheusType.Gauge,
            3 => PrometheusType.Summary,
            4 or 5 or 6 or 7 => PrometheusType.Histogram,
            8 => PrometheusType.Gauge,
            _ => throw new InvalidOperationException($"Invalid {nameof(MetricType)} value."),
        };
    }

    /// <summary>
    /// Returns the metric name set for the requested escaping scheme, computing (and caching) the
    /// names lazily on first use.
    /// </summary>
    /// <param name="escaping">The escaping scheme to use.</param>
    /// <returns>The metric name set for the requested escaping scheme.</returns>
    internal NameSet GetNameSet(EscapingScheme escaping) => escaping switch
    {
        EscapingScheme.Dots => this.dotsNames ??= BuildEscapedNames(this.rawName, this.Unit, this.Type, this.disableTotalNameSuffixForCounters, EscapingScheme.Dots),
        EscapingScheme.Values => this.valuesNames ??= BuildEscapedNames(this.rawName, this.Unit, this.Type, this.disableTotalNameSuffixForCounters, EscapingScheme.Values),
        _ => this.underscoreNames,
    };

    private static byte[] ConvertToBytes(string value)
    {
        // Metric names and units are sanitized before conversion, so every character here is ASCII
        var bytes = new byte[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            bytes[i] = unchecked((byte)value[i]);
        }

        return bytes;
    }

    private static NameSet BuildUnderscoreNames(string name, string? sanitizedUnit, PrometheusType type, bool disableTotalNameSuffixForCounters)
    {
        // The metric name is
        // required to match the regex: `[a-zA-Z_:]([a-zA-Z0-9_:])*`. Invalid characters
        // in the metric name MUST be replaced with the `_` character. Multiple
        // consecutive `_` characters MUST be replaced with a single `_` character.
        // https://github.com/open-telemetry/opentelemetry-specification/blob/b2f923fb1650dde1f061507908b834035506a796/specification/compatibility/prometheus_and_openmetrics.md#L230-L233
        var sanitizedName = SanitizeMetricName(name);
        var openMetricsName = RemoveOpenMetricsCounterNameSuffix(name);

        if (sanitizedUnit != null)
        {
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

        openMetricsName = EscapeOpenMetricsName(openMetricsName);

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
            ? RemoveOpenMetricsCounterNameSuffix(openMetricsName)
            : openMetricsName;

        return new(sanitizedName, openMetricsName, openMetricsMetadataName);
    }

    private static NameSet BuildEscapedNames(
        string name,
        string? sanitizedUnit,
        PrometheusType type,
        bool disableTotalNameSuffixForCounters,
        EscapingScheme escaping)
    {
        // The escaping scheme is applied to the metric family name (the original name plus any
        // unit suffix). The type-specific '_total' suffix is a structural suffix that Prometheus
        // strips to find the family (like the '_bucket'/'_sum'/'_count' suffixes added during
        // serialization), so it is appended literally to the escaped family name rather than
        // escaped with it.
        var nameFamily = name;
        var openMetricsFamily = RemoveOpenMetricsCounterNameSuffix(name);

        if (sanitizedUnit != null)
        {
            if (!nameFamily.EndsWith(sanitizedUnit, StringComparison.Ordinal))
            {
                nameFamily += $"_{sanitizedUnit}";
            }

            if (!openMetricsFamily.EndsWith(sanitizedUnit, StringComparison.Ordinal))
            {
                openMetricsFamily += $"_{sanitizedUnit}";
            }
        }

        var escapedName = PrometheusEscaping.EscapeName(nameFamily, escaping);

        // The metadata (UNIT/TYPE/HELP) name is the escaped OpenMetrics family without the
        // '_total' suffix, as in the case for counters.
        var openMetricsMetadataName = PrometheusEscaping.EscapeName(openMetricsFamily, escaping);

        if (type == PrometheusType.Counter && !nameFamily.EndsWith("_total", StringComparison.Ordinal) && !disableTotalNameSuffixForCounters)
        {
            escapedName += "_total";
        }

        var openMetricsName = type == PrometheusType.Counter && !openMetricsFamily.EndsWith("_total", StringComparison.Ordinal)
            ? openMetricsMetadataName + "_total"
            : openMetricsMetadataName;

        return new(escapedName, openMetricsName, openMetricsMetadataName);
    }

    private static string RemoveOpenMetricsCounterNameSuffix(string metricName)
        => metricName.EndsWith("_total", StringComparison.Ordinal) ? metricName.Substring(0, metricName.Length - 6) : metricName;

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

        return SanitizeMetricUnit(updatedUnit);
    }

    private static bool TryProcessRateUnits(string updatedUnit, [NotNullWhen(true)] out string? updatedPerUnit)
    {
        updatedPerUnit = null;

        for (var i = 0; i < updatedUnit.Length; i++)
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

    // The map to translate OTLP units to Prometheus units
    // OTLP metrics use the c/s notation as specified at https://ucum.org/ucum.html
    // (See also https://github.com/open-telemetry/semantic-conventions/blob/main/docs/general/metrics.md#instrument-units)
    // Prometheus best practices for units: https://prometheus.io/docs/practices/naming/#base-units
    // OpenMetrics specification for units: https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md#units-and-base-units
    private static string MapUnit(ReadOnlySpan<char> unit) => unit switch
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

    // The map that translates the "per" unit
    // Example: s => per second (singular)
    private static string MapPerUnit(ReadOnlySpan<char> perUnit) => perUnit switch
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

    /// <summary>
    /// Represents a set of pre-computed metric family names (and their ASCII byte representations) for a single escaping scheme.
    /// </summary>
    internal sealed class NameSet
    {
        public NameSet(string name, string openMetricsName, string openMetricsMetadataName)
        {
            this.Name = name;
            this.OpenMetricsName = openMetricsName;
            this.OpenMetricsMetadataName = openMetricsMetadataName;
            this.NameBytes = ConvertToBytes(name);
            this.OpenMetricsNameBytes = ConvertToBytes(openMetricsName);
            this.OpenMetricsMetadataNameBytes = ConvertToBytes(openMetricsMetadataName);
        }

        public string Name { get; }

        public string OpenMetricsName { get; }

        public string OpenMetricsMetadataName { get; }

        public byte[] NameBytes { get; }

        public byte[] OpenMetricsNameBytes { get; }

        public byte[] OpenMetricsMetadataNameBytes { get; }
    }
}
