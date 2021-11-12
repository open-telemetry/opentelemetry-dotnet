// <copyright file="PrometheusMetricsFormatHelper.cs" company="OpenTelemetry Authors">
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
#if NETCOREAPP3_1_OR_GREATER
using System.Buffers;
#endif
using System.Diagnostics;
#if NET461
using System.Text;
#endif

namespace OpenTelemetry.Exporter.Prometheus
{
    internal static class PrometheusMetricsFormatHelper
    {
#if NETCOREAPP3_1_OR_GREATER
        private static readonly SpanAction<char, (string, Func<char, bool, bool>)> CreateName
            = (Span<char> data, (string Name, Func<char, bool, bool> IsCharacterAllowedFunc) state) =>
            {
                for (var i = 0; i < state.Name.Length; i++)
                {
                    var c = state.Name[i];
                    data[i] = state.IsCharacterAllowedFunc(c, i == 0)
                        ? c
                        : '_';
                }
            };
#endif

        private static readonly Func<char, bool, bool> IsCharacterAllowedForName =
            (char c, bool isFirstChar) =>
            {
                return (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (!isFirstChar && c >= '0' && c <= '9')
                    || c == '_'
                    || c == ':';
            };

        private static readonly Func<char, bool, bool> IsCharacterAllowedForLabel
            = (char c, bool isFirstChar) =>
            {
                return (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (!isFirstChar && c >= '0' && c <= '9')
                    || c == '_';
            };

        public static string GetSafeMetricName(string name) => GetSafeName(name, IsCharacterAllowedForName);

        public static string GetSafeLabelName(string name) => GetSafeName(name, IsCharacterAllowedForLabel);

        public static string GetSafeLabelValue(string value)
        {
            // label_value can be any sequence of UTF-8 characters, but the backslash
            // (\), double-quote ("), and line feed (\n) characters have to be escaped
            // as \\, \", and \n, respectively.

            return value
                .Replace("\\", @"\\")
                .Replace("\n", @"\n")
                .Replace("\"", @"\""");
        }

        public static string GetSafeMetricDescription(string description)
        {
            // HELP lines may contain any sequence of UTF-8 characters(after the metric name), but the backslash
            // and the line feed characters have to be escaped as \\ and \n, respectively.Only one HELP line may
            // exist for any given metric name.
            return description
                .Replace("\\", @"\\")
                .Replace("\n", @"\n");
        }

        private static string GetSafeName(string name, Func<char, bool, bool> isCharacterAllowedFunc)
        {
            // https://prometheus.io/docs/concepts/data_model/#metric-names-and-labels
            //
            // Metric names and labels
            // Every time series is uniquely identified by its metric name and a set of key-value pairs, also known as labels.
            // The metric name specifies the general feature of a system that is measured (e.g. http_requests_total - the total number of HTTP requests received). It may contain ASCII letters and digits, as well as underscores and colons. It must match the regex [a-zA-Z_:][a-zA-Z0-9_:]*.
            // Note: The colons are reserved for user defined recording rules. They should not be used by exporters or direct instrumentation.
            // Labels enable Prometheus's dimensional data model: any given combination of labels for the same metric name identifies a particular dimensional instantiation of that metric (for example: all HTTP requests that used the method POST to the /api/tracks handler). The query language allows filtering and aggregation based on these dimensions. Changing any label value, including adding or removing a label, will create a new time series.
            // Label names may contain ASCII letters, numbers, as well as underscores. They must match the regex [a-zA-Z_][a-zA-Z0-9_]*. Label names beginning with __ are reserved for internal use.
            // Label values may contain any Unicode characters.

            Debug.Assert(!string.IsNullOrEmpty(name), "name is empty or null");

#if NETCOREAPP3_1_OR_GREATER
            return string.Create(name.Length, (name, isCharacterAllowedFunc), CreateName);
#else
            var sb = new StringBuilder(name.Length);

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                sb.Append(isCharacterAllowedFunc(c, i == 0)
                    ? c
                    : '_');
            }

            return sb.ToString();
#endif
        }
    }
}
