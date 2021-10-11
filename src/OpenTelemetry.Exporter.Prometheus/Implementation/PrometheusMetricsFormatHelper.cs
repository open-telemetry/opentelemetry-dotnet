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
using System.Linq;
using System.Text;

namespace OpenTelemetry.Exporter.Prometheus
{
    internal static class PrometheusMetricsFormatHelper
    {
        public const string ContentType = "text/plain; version = 0.0.4";

        private static readonly char[] FirstCharacterNameCharset =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '_', ':',
        };

        private static readonly char[] NameCharset =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            '_', ':',
        };

        private static readonly char[] FirstCharacterLabelCharset =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '_',
        };

        private static readonly char[] LabelCharset =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            '_',
        };

        public static string GetSafeMetricName(string name) => GetSafeName(name, FirstCharacterNameCharset, NameCharset);

        public static string GetSafeLabelName(string name) => GetSafeName(name, FirstCharacterLabelCharset, LabelCharset);

        public static string GetSafeLabelValue(string value)
        {
            // label_value can be any sequence of UTF-8 characters, but the backslash
            // (\), double-quote ("), and line feed (\n) characters have to be escaped
            // as \\, \", and \n, respectively.

            var result = value.Replace("\\", "\\\\");
            result = result.Replace("\n", "\\n");
            result = result.Replace("\"", "\\\"");

            return result;
        }

        public static string GetSafeMetricDescription(string description)
        {
            // HELP lines may contain any sequence of UTF-8 characters(after the metric name), but the backslash
            // and the line feed characters have to be escaped as \\ and \n, respectively.Only one HELP line may
            // exist for any given metric name.
            var result = description.Replace(@"\", @"\\");
            result = result.Replace("\n", @"\n");

            return result;
        }

        private static string GetSafeName(string name, char[] firstCharNameCharset, char[] charNameCharset)
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

            var sb = new StringBuilder();
            var firstChar = name[0];

            sb.Append(firstCharNameCharset.Contains(firstChar)
                ? firstChar
                : GetSafeChar(char.ToLowerInvariant(firstChar), firstCharNameCharset));

            for (var i = 1; i < name.Length; ++i)
            {
                sb.Append(GetSafeChar(name[i], charNameCharset));
            }

            return sb.ToString();

            static char GetSafeChar(char c, char[] charset) => charset.Contains(c) ? c : '_';
        }
    }
}
