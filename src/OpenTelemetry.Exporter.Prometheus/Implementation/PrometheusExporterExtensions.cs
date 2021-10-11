// <copyright file="PrometheusExporterExtensions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using static OpenTelemetry.Exporter.Prometheus.PrometheusMetricsFormatHelper;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Helper to write metrics collection from exporter in Prometheus format.
    /// </summary>
    /// <remarks>
    /// Format reference: <see href="https://prometheus.io/docs/instrumenting/exposition_formats/" />.
    /// </remarks>
    internal static class PrometheusExporterExtensions
    {
        private const string PrometheusCounterType = "counter";
        private const string PrometheusGaugeType = "gauge";
        private const string PrometheusHistogramType = "histogram";
        private const string PrometheusHistogramBucketLabelPositiveInfinity = "+Inf";
        private const string PrometheusHistogramBucketLabelLessThan = "le";
#if NETCOREAPP3_1_OR_GREATER
        // Max sizes taken from: https://github.com/dotnet/runtime/blob/6c09f34947026ed988fd3013fdaa624a5fab0f26/src/libraries/System.Text.Json/src/System/Text/Json/JsonConstants.cs#L81-L83
        private const int MaxLongCharSize = 20;
        private const int MaxDoubleCharSize = 128;
#endif

        private static readonly ConcurrentDictionary<string, MetricInfo> MetricInfoCache = new ConcurrentDictionary<string, MetricInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly byte[] PrometheusCounterTypeUtf8 = Encoding.UTF8.GetBytes(PrometheusCounterType);
        private static readonly byte[] PrometheusGaugeTypeUtf8 = Encoding.UTF8.GetBytes(PrometheusGaugeType);
        private static readonly byte[] PrometheusHistogramTypeUtf8 = Encoding.UTF8.GetBytes(PrometheusHistogramType);
        private static readonly byte[] PrometheusHistogramBucketLabelPositiveInfinityUtf8 = Encoding.UTF8.GetBytes(PrometheusHistogramBucketLabelPositiveInfinity);
        private static readonly byte[] PrometheusHistogramBucketLabelLessThanUtf8 = Encoding.UTF8.GetBytes(PrometheusHistogramBucketLabelLessThan);

        private static readonly byte[] TypeHeadingUtf8 = Encoding.UTF8.GetBytes("# TYPE ");
        private static readonly byte[] HelpHeadingUtf8 = Encoding.UTF8.GetBytes("# HELP ");
        private static readonly byte[] HistogramSumSuffixUtf8 = Encoding.UTF8.GetBytes("_sum");
        private static readonly byte[] HistogramCountSuffixUtf8 = Encoding.UTF8.GetBytes("_count");
        private static readonly byte[] HistogramBucketSuffixUtf8 = Encoding.UTF8.GetBytes("_bucket");
        private static readonly byte[] SpaceUtf8 = new byte[] { (byte)' ' };
        private static readonly byte[] NewLineUtf8 = new byte[] { (byte)'\n' };
        private static readonly byte[] OpeningBraceUtf8 = new byte[] { (byte)'{' };
        private static readonly byte[] ClosingBraceUtf8 = new byte[] { (byte)'}' };
        private static readonly byte[] EqualUtf8 = new byte[] { (byte)'=' };
        private static readonly byte[] CommaUtf8 = new byte[] { (byte)',' };
        private static readonly byte[] DoubleQuoteUtf8 = new byte[] { (byte)'"' };
        private static readonly byte[] NaNUtf8 = Encoding.UTF8.GetBytes("Nan");
        private static readonly byte[] PositiveInfinityUtf8 = Encoding.UTF8.GetBytes("+Inf");
        private static readonly byte[] NegativeInfinityUtf8 = Encoding.UTF8.GetBytes("-Inf");

        private static readonly WriteMetricsFunc WriteLongMetrics =
            async (Stream stream, Metric metric, Func<DateTimeOffset> getUtcNowDateTimeOffset, byte[] buffer, MetricInfo metricInfo) =>
            {
                BatchMetricPoint.Enumerator enumerator = metric.GetMetricPoints().GetEnumerator();

                if (TryGetMetric(ref enumerator, out var state))
                {
                    await WriteMetric(stream, getUtcNowDateTimeOffset, buffer, metricInfo, metricInfo.NameUtf8, state.Item1, state.Item2, state.Item3).ConfigureAwait(false);
                }

                static bool TryGetMetric(ref BatchMetricPoint.Enumerator enumerator, out (string[], object[], long) state)
                {
                    if (!enumerator.MoveNext())
                    {
                        state = default;
                        return false;
                    }

                    ref MetricPoint metricPoint = ref enumerator.Current;
                    state = (metricPoint.Keys, metricPoint.Values, metricPoint.LongValue);
                    return true;
                }
            };

        private static readonly WriteMetricsFunc WriteDoubleMetrics =
            async (Stream stream, Metric metric, Func<DateTimeOffset> getUtcNowDateTimeOffset, byte[] buffer, MetricInfo metricInfo) =>
            {
                BatchMetricPoint.Enumerator enumerator = metric.GetMetricPoints().GetEnumerator();

                if (TryGetMetric(ref enumerator, out var state))
                {
                    await WriteMetric(stream, getUtcNowDateTimeOffset, buffer, metricInfo, metricInfo.NameUtf8, state.Item1, state.Item2, state.Item3).ConfigureAwait(false);
                }

                static bool TryGetMetric(ref BatchMetricPoint.Enumerator enumerator, out (string[], object[], double) state)
                {
                    if (!enumerator.MoveNext())
                    {
                        state = default;
                        return false;
                    }

                    ref MetricPoint metricPoint = ref enumerator.Current;
                    state = (metricPoint.Keys, metricPoint.Values, metricPoint.DoubleValue);
                    return true;
                }
            };

        private static readonly WriteMetricsFunc WriteHistogramMetrics =
            async (Stream stream, Metric metric, Func<DateTimeOffset> getUtcNowDateTimeOffset, byte[] buffer, MetricInfo metricInfo) =>
            {
                /*
                 *  For Histogram we emit one row for Sum, Count and as
                 *  many rows as number of buckets.
                 *  myHistogram_sum{tag1="value1",tag2="value2"} 258330 1629860660991
                 *  myHistogram_count{tag1="value1",tag2="value2"} 355 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="0"} 0 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="5"} 2 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="10"} 4 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="25"} 6 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="50"} 12 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="75"} 19 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="100"} 26 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="250"} 65 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="500"} 128 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="1000"} 241 1629860660991
                 *  myHistogram_bucket{tag1="value1",tag2="value2",le="+Inf"} 355 1629860660991
                */

                BatchMetricPoint.Enumerator enumerator = metric.GetMetricPoints().GetEnumerator();

                if (TryGetMetric(ref enumerator, out var state))
                {
                    await WriteMetric(stream, getUtcNowDateTimeOffset, buffer, metricInfo, metricInfo.HistogramSumUtf8, state.keys, state.values, state.sum).ConfigureAwait(false);

                    await WriteMetric(stream, getUtcNowDateTimeOffset, buffer, metricInfo, metricInfo.HistogramCountUtf8, state.keys, state.values, state.count).ConfigureAwait(false);

                    if (state.explicitBounds != null)
                    {
                        long totalCount = 0;
                        for (int i = 0; i < state.explicitBounds.Length + 1; i++)
                        {
                            totalCount += state.bucketCounts[i];

                            byte[] bucketValueUtf8 = i == state.explicitBounds.Length
                                ? PrometheusHistogramBucketLabelPositiveInfinityUtf8
                                : metricInfo.GetBucketUtf8(state.explicitBounds[i]);

                            await WriteMetric(
                                stream,
                                getUtcNowDateTimeOffset,
                                buffer,
                                metricInfo,
                                metricInfo.HistogramBucketUtf8,
                                state.keys,
                                state.values,
                                totalCount,
                                additionalKvp: new KeyValuePair<byte[], byte[]>(PrometheusHistogramBucketLabelLessThanUtf8, bucketValueUtf8)).ConfigureAwait(false);
                        }
                    }
                }

                static bool TryGetMetric(
                    ref BatchMetricPoint.Enumerator enumerator,
                    out (string[] keys, object[] values, double sum, long count, double[] explicitBounds, long[] bucketCounts) state)
                {
                    if (!enumerator.MoveNext())
                    {
                        state = default;
                        return false;
                    }

                    ref MetricPoint metricPoint = ref enumerator.Current;
                    state = (metricPoint.Keys, metricPoint.Values, metricPoint.DoubleValue, metricPoint.LongValue, metricPoint.ExplicitBounds, metricPoint.BucketCounts);
                    return true;
                }
            };

        private delegate Task WriteMetricsFunc(
            Stream stream,
            Metric metric,
            Func<DateTimeOffset> getUtcNowDateTimeOffset,
            byte[] buffer,
            MetricInfo metricInfo);

        /// <summary>
        /// Serialize metrics to prometheus format.
        /// </summary>
        /// <param name="exporter"><see cref="PrometheusExporter"/>.</param>
        /// <param name="stream">Stream to write to.</param>
        /// <param name="getUtcNowDateTimeOffset">Optional function to resolve the current date &amp; time.</param>
        /// <returns><see cref="Task"/> to await the operation.</returns>
        public static async Task WriteMetricsCollection(
            this PrometheusExporter exporter,
            Stream stream,
            Func<DateTimeOffset> getUtcNowDateTimeOffset)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                foreach (var metric in exporter.Metrics)
                {
                    if (!MetricInfoCache.TryGetValue(metric.Name, out MetricInfo metricInfo))
                    {
                        byte[] metricTypeUtf8;
                        WriteMetricsFunc writeMetricFunc;

                        switch (metric.MetricType)
                        {
                            case MetricType.LongSum:
                                {
                                    metricTypeUtf8 = PrometheusCounterTypeUtf8;
                                    writeMetricFunc = WriteLongMetrics;
                                    break;
                                }

                            case MetricType.DoubleSum:
                                {
                                    metricTypeUtf8 = PrometheusCounterTypeUtf8;
                                    writeMetricFunc = WriteDoubleMetrics;
                                    break;
                                }

                            case MetricType.LongGauge:
                                {
                                    metricTypeUtf8 = PrometheusGaugeTypeUtf8;
                                    writeMetricFunc = WriteLongMetrics;
                                    break;
                                }

                            case MetricType.DoubleGauge:
                                {
                                    metricTypeUtf8 = PrometheusGaugeTypeUtf8;
                                    writeMetricFunc = WriteDoubleMetrics;
                                    break;
                                }

                            case MetricType.Histogram:
                                {
                                    metricTypeUtf8 = PrometheusHistogramTypeUtf8;
                                    writeMetricFunc = WriteHistogramMetrics;
                                    break;
                                }

                            default:
                                continue;
                        }

                        metricInfo = MetricInfo.CreateMetricInfo(metric, metricTypeUtf8, writeMetricFunc);
                        MetricInfoCache.TryAdd(metric.Name, metricInfo);
                    }

                    if (metricInfo.HelpUtf8 != null)
                    {
                        await stream.WriteAsync(metricInfo.HelpUtf8, 0, metricInfo.HelpUtf8.Length).ConfigureAwait(false);
                    }

                    await stream.WriteAsync(metricInfo.TypeUtf8, 0, metricInfo.TypeUtf8.Length).ConfigureAwait(false);

                    await metricInfo.WriteMetricFunc(stream, metric, getUtcNowDateTimeOffset, buffer, metricInfo).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void WriteHelp(Stream stream, byte[] nameUtf8, byte[] descriptionUtf8)
        {
            // Lines with a # as the first non-whitespace character are comments.
            // They are ignored unless the first token after # is either HELP or TYPE.
            // Those lines are treated as follows: If the token is HELP, at least one
            // more token is expected, which is the metric name. All remaining tokens
            // are considered the docstring for that metric name. HELP lines may contain
            // any sequence of UTF-8 characters (after the metric name), but the backslash
            // and the line feed characters have to be escaped as \\ and \n, respectively.
            // Only one HELP line may exist for any given metric name.

            stream.Write(HelpHeadingUtf8, 0, HelpHeadingUtf8.Length);
            stream.Write(nameUtf8, 0, nameUtf8.Length);
            stream.WriteByte((byte)' ');
            stream.Write(descriptionUtf8, 0, descriptionUtf8.Length);
            stream.WriteByte((byte)'\n');
        }

        private static void WriteType(Stream stream, byte[] nameUtf8, byte[] typeUtf8)
        {
            // If the token is TYPE, exactly two more tokens are expected. The first is the
            // metric name, and the second is either counter, gauge, histogram, summary, or
            // untyped, defining the type for the metric of that name. Only one TYPE line
            // may exist for a given metric name. The TYPE line for a metric name must appear
            // before the first sample is reported for that metric name. If there is no TYPE
            // line for a metric name, the type is set to untyped.

            stream.Write(TypeHeadingUtf8, 0, TypeHeadingUtf8.Length);
            stream.Write(nameUtf8, 0, nameUtf8.Length);
            stream.WriteByte((byte)' ');
            stream.Write(typeUtf8, 0, typeUtf8.Length);
            stream.WriteByte((byte)'\n');
        }

        private static async Task WriteMetric<T>(
            Stream stream,
            Func<DateTimeOffset> getUtcNowDateTimeOffset,
            byte[] buffer,
            MetricInfo metricInfo,
            byte[] nameUtf8,
            string[] keys,
            object[] values,
            T value,
            KeyValuePair<byte[], byte[]>? additionalKvp = null)
        {
            // The remaining lines describe samples (one per line) using the following syntax (EBNF):
            // metric_name [
            //   "{" label_name "=" `"` label_value `"` { "," label_name "=" `"` label_value `"` } [ "," ] "}"
            // ] value [ timestamp ]

            var unixNow = getUtcNowDateTimeOffset().ToUnixTimeMilliseconds();

            // metric_name and label_name carry the usual Prometheus expression language restrictions.
            await stream.WriteAsync(nameUtf8, 0, nameUtf8.Length).ConfigureAwait(false);

            // label_value can be any sequence of UTF-8 characters, but the backslash
            // (\, double-quote ("}, and line feed (\n) characters have to be escaped
            // as \\, \", and \n, respectively.

            int numberOfKeys = keys?.Length ?? 0;
            if (numberOfKeys > 0 || additionalKvp.HasValue)
            {
                await stream.WriteAsync(OpeningBraceUtf8, 0, 1).ConfigureAwait(false);
                int i = 0;
                while (i < keys.Length)
                {
                    await WriteKeyValuePair(stream, metricInfo.GetKeyUtf8(keys[i]), metricInfo.GetValueUtf8(values[i]), i > 0).ConfigureAwait(false);
                    i++;
                }

                if (additionalKvp.HasValue)
                {
                    await WriteKeyValuePair(stream, additionalKvp.Value.Key, additionalKvp.Value.Value, i > 0).ConfigureAwait(false);
                }

                await stream.WriteAsync(ClosingBraceUtf8, 0, 1).ConfigureAwait(false);
            }

            // value is a float represented as required by Go's ParseFloat() function. In addition to
            // standard numerical values, Nan, +Inf, and -Inf are valid values representing not a number,
            // positive infinity, and negative infinity, respectively.
            await stream.WriteAsync(SpaceUtf8, 0, 1).ConfigureAwait(false);
            await WriteValue(stream, buffer, value).ConfigureAwait(false);
            await stream.WriteAsync(SpaceUtf8, 0, 1).ConfigureAwait(false);

            // The timestamp is an int64 (milliseconds since epoch, i.e. 1970-01-01 00:00:00 UTC, excluding
            // leap seconds), represented as required by Go's ParseInt() function.
            await WriteNow(stream, buffer, unixNow).ConfigureAwait(false);

            // Prometheus' text-based format is line oriented. Lines are separated
            // by a line feed character (\n). The last line must end with a line
            // feed character. Empty lines are ignored.
            await stream.WriteAsync(NewLineUtf8, 0, 1).ConfigureAwait(false);
        }

        private static Task WriteValue<T>(Stream stream, byte[] buffer, T value)
        {
            if (value is long longValue)
            {
#if NETCOREAPP3_1_OR_GREATER
                Span<char> longValueCharacters = stackalloc char[MaxLongCharSize];
                bool result = longValue.TryFormat(longValueCharacters, out int charsWritten, "G", CultureInfo.InvariantCulture);
                Debug.Assert(result, "result was not true");
                int numberOfBytes = Encoding.UTF8.GetBytes(longValueCharacters.Slice(0, charsWritten), buffer);
                return stream.WriteAsync(buffer, 0, numberOfBytes);
#else
                string longValueString = longValue.ToString(CultureInfo.InvariantCulture);
                byte[] longValueUtf8 = Encoding.UTF8.GetBytes(longValueString);
                return stream.WriteAsync(longValueUtf8, 0, longValueUtf8.Length);
#endif
            }

            if (value is double doubleValue)
            {
                if (double.IsNaN(doubleValue))
                {
                    return stream.WriteAsync(NaNUtf8, 0, NaNUtf8.Length);
                }

                if (double.IsPositiveInfinity(doubleValue))
                {
                    return stream.WriteAsync(PositiveInfinityUtf8, 0, PositiveInfinityUtf8.Length);
                }

                if (double.IsNegativeInfinity(doubleValue))
                {
                    return stream.WriteAsync(NegativeInfinityUtf8, 0, NegativeInfinityUtf8.Length);
                }

#if NETCOREAPP3_1_OR_GREATER
                Span<char> doubleValueCharacters = stackalloc char[MaxDoubleCharSize];
                bool result = doubleValue.TryFormat(doubleValueCharacters, out int charsWritten, "G", CultureInfo.InvariantCulture);
                Debug.Assert(result, "result was not true");
                int numberOfBytes = Encoding.UTF8.GetBytes(doubleValueCharacters.Slice(0, charsWritten), buffer);
                return stream.WriteAsync(buffer, 0, numberOfBytes);
#else
                string doubleValueString = doubleValue.ToString(CultureInfo.InvariantCulture);
                byte[] doubleValueUtf8 = Encoding.UTF8.GetBytes(doubleValueString);
                return stream.WriteAsync(doubleValueUtf8, 0, doubleValueUtf8.Length);
#endif
            }

            Debug.Fail("This code should not be reached.");
            string stringValue = value.ToString();
            byte[] valueUtf8 = Encoding.UTF8.GetBytes(stringValue);
            return stream.WriteAsync(valueUtf8, 0, valueUtf8.Length);
        }

        private static Task WriteNow(Stream stream, byte[] buffer, long unixNow)
        {
#if NETCOREAPP3_1_OR_GREATER
            Span<char> nowCharacters = stackalloc char[MaxLongCharSize];
            bool result = unixNow.TryFormat(nowCharacters, out int charsWritten, "G", CultureInfo.InvariantCulture);
            Debug.Assert(result, "result was not true");
            int numberOfBytes = Encoding.UTF8.GetBytes(nowCharacters.Slice(0, charsWritten), buffer);
            return stream.WriteAsync(buffer, 0, numberOfBytes);
#else
            string now = unixNow.ToString(CultureInfo.InvariantCulture);
            byte[] nowUtf8 = Encoding.UTF8.GetBytes(now);
            return stream.WriteAsync(nowUtf8, 0, nowUtf8.Length);
#endif
        }

        private static async Task WriteKeyValuePair(Stream stream, byte[] keyUtf8, byte[] valueUtf8, bool writeCommaFirst)
        {
            if (writeCommaFirst)
            {
                await stream.WriteAsync(CommaUtf8, 0, 1).ConfigureAwait(false);
            }

            await stream.WriteAsync(keyUtf8, 0, keyUtf8.Length).ConfigureAwait(false);

            await stream.WriteAsync(EqualUtf8, 0, 1).ConfigureAwait(false);

            await stream.WriteAsync(DoubleQuoteUtf8, 0, 1).ConfigureAwait(false);
            await stream.WriteAsync(valueUtf8, 0, valueUtf8.Length).ConfigureAwait(false);
            await stream.WriteAsync(DoubleQuoteUtf8, 0, 1).ConfigureAwait(false);
        }

        private class MetricInfo
        {
            public byte[] NameUtf8;
            public byte[] TypeUtf8;
            public byte[] HelpUtf8;
            public byte[] HistogramSumUtf8;
            public byte[] HistogramCountUtf8;
            public byte[] HistogramBucketUtf8;
            public WriteMetricsFunc WriteMetricFunc;

            private readonly ConcurrentDictionary<string, byte[]> keyCache = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<object, byte[]> valueCache = new ConcurrentDictionary<object, byte[]>();
            private ConcurrentDictionary<double, byte[]> bucketCache;

            public static MetricInfo CreateMetricInfo(
                Metric metric,
                byte[] metricTypeUtf8,
                WriteMetricsFunc writeMetricFunc)
            {
                MetricInfo metricInfo = new MetricInfo
                {
                    NameUtf8 = Encoding.UTF8.GetBytes(GetSafeMetricName(metric.Name)),
                    WriteMetricFunc = writeMetricFunc,
                };

                using (MemoryStream stream = new MemoryStream(2048))
                {
                    WriteType(stream, metricInfo.NameUtf8, metricTypeUtf8);

                    metricInfo.TypeUtf8 = stream.ToArray();

                    if (!string.IsNullOrEmpty(metric.Description))
                    {
                        byte[] descriptionUtf8 = Encoding.UTF8.GetBytes(GetSafeMetricDescription(metric.Description));

                        stream.Position = 0;
                        stream.SetLength(0);

                        WriteHelp(stream, metricInfo.NameUtf8, descriptionUtf8);

                        metricInfo.HelpUtf8 = stream.ToArray();
                    }

                    if (metric.MetricType == MetricType.Histogram)
                    {
                        stream.Position = 0;
                        stream.SetLength(0);

                        stream.Write(metricInfo.NameUtf8, 0, metricInfo.NameUtf8.Length);
                        stream.Write(HistogramSumSuffixUtf8, 0, HistogramSumSuffixUtf8.Length);

                        metricInfo.HistogramSumUtf8 = stream.ToArray();

                        stream.Position = 0;
                        stream.SetLength(0);

                        stream.Write(metricInfo.NameUtf8, 0, metricInfo.NameUtf8.Length);
                        stream.Write(HistogramCountSuffixUtf8, 0, HistogramCountSuffixUtf8.Length);

                        metricInfo.HistogramCountUtf8 = stream.ToArray();

                        stream.Position = 0;
                        stream.SetLength(0);

                        stream.Write(metricInfo.NameUtf8, 0, metricInfo.NameUtf8.Length);
                        stream.Write(HistogramBucketSuffixUtf8, 0, HistogramBucketSuffixUtf8.Length);

                        metricInfo.HistogramBucketUtf8 = stream.ToArray();

                        metricInfo.bucketCache = new ConcurrentDictionary<double, byte[]>();
                    }
                }

                return metricInfo;
            }

            public byte[] GetKeyUtf8(string key)
            {
                if (!this.keyCache.TryGetValue(key, out byte[] keyUtf8))
                {
                    string escapedKey = GetSafeLabelName(key);
                    keyUtf8 = Encoding.UTF8.GetBytes(escapedKey);
                    if (this.keyCache.Count < 128)
                    {
                        this.keyCache.TryAdd(key, keyUtf8);
                    }
                }

                return keyUtf8;
            }

            public byte[] GetValueUtf8(object value)
            {
                if (!this.valueCache.TryGetValue(value, out byte[] valueUtf8))
                {
                    string escapedValue = GetSafeLabelValue(value?.ToString() ?? "null");
                    valueUtf8 = Encoding.UTF8.GetBytes(escapedValue);
                    if (this.valueCache.Count < 128)
                    {
                        this.valueCache.TryAdd(value, valueUtf8);
                    }
                }

                return valueUtf8;
            }

            public byte[] GetBucketUtf8(double bucketValue)
            {
                if (!this.bucketCache.TryGetValue(bucketValue, out byte[] bucketValueUtf8))
                {
                    bucketValueUtf8 = Encoding.UTF8.GetBytes(bucketValue.ToString(CultureInfo.InvariantCulture));
                    this.bucketCache.TryAdd(bucketValue, bucketValueUtf8);
                }

                return bucketValueUtf8;
            }
        }
    }
}
