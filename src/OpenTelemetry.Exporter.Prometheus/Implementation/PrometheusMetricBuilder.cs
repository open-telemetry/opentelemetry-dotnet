// <copyright file="PrometheusMetricBuilder.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTelemetry.Exporter.Prometheus.Implementation
{
    internal class PrometheusMetricBuilder
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

        private readonly ICollection<PrometheusMetricValueBuilder> values = new List<PrometheusMetricValueBuilder>();

        private string name;
        private string description;
        private string type;

        public PrometheusMetricBuilder WithName(string name)
        {
            this.name = name;
            return this;
        }

        public PrometheusMetricBuilder WithDescription(string description)
        {
            this.description = description;
            return this;
        }

        public PrometheusMetricBuilder WithType(string type)
        {
            this.type = type;
            return this;
        }

        public PrometheusMetricValueBuilder AddValue()
        {
            var val = new PrometheusMetricValueBuilder();

            this.values.Add(val);

            return val;
        }

        public void Write(StreamWriter writer)
        {
            // https://prometheus.io/docs/instrumenting/exposition_formats/

            if (string.IsNullOrEmpty(this.name))
            {
                throw new InvalidOperationException("Metric name should not be empty");
            }

            this.name = GetSafeMetricName(this.name);

            if (!string.IsNullOrEmpty(this.description))
            {
                // Lines with a # as the first non-whitespace character are comments.
                // They are ignored unless the first token after # is either HELP or TYPE.
                // Those lines are treated as follows: If the token is HELP, at least one
                // more token is expected, which is the metric name. All remaining tokens
                // are considered the docstring for that metric name. HELP lines may contain
                // any sequence of UTF-8 characters (after the metric name), but the backslash
                // and the line feed characters have to be escaped as \\ and \n, respectively.
                // Only one HELP line may exist for any given metric name.

                writer.Write("# HELP ");
                writer.Write(this.name);
                writer.Write(GetSafeMetricDescription(this.description));
                writer.Write("\n");
            }

            if (!string.IsNullOrEmpty(this.type))
            {
                // If the token is TYPE, exactly two more tokens are expected. The first is the
                // metric name, and the second is either counter, gauge, histogram, summary, or
                // untyped, defining the type for the metric of that name. Only one TYPE line
                // may exist for a given metric name. The TYPE line for a metric name must appear
                // before the first sample is reported for that metric name. If there is no TYPE
                // line for a metric name, the type is set to untyped.

                writer.Write("# TYPE ");
                writer.Write(this.name);
                writer.Write(" ");
                writer.Write(this.type);
                writer.Write("\n");
            }

            // The remaining lines describe samples (one per line) using the following syntax (EBNF):
            // metric_name [
            //   "{" label_name "=" `"` label_value `"` { "," label_name "=" `"` label_value `"` } [ "," ] "}"
            // ] value [ timestamp ]
            // In the sample syntax:

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            foreach (var m in this.values)
            {
                // metric_name and label_name carry the usual Prometheus expression language restrictions.
                writer.Write(m.Name != null ? GetSafeMetricName(m.Name) : this.name);

                // label_value can be any sequence of UTF-8 characters, but the backslash
                // (\, double-quote ("}, and line feed (\n) characters have to be escaped
                // as \\, \", and \n, respectively.

                if (m.Labels.Count > 0)
                {
                    writer.Write(@"{");
                    writer.Write(string.Join(",", m.Labels.Select(x => GetLabelAndValue(x.Item1, x.Item2))));
                    writer.Write(@"}");
                }

                // value is a float represented as required by Go's ParseFloat() function. In addition to
                // standard numerical values, Nan, +Inf, and -Inf are valid values representing not a number,
                // positive infinity, and negative infinity, respectively.
                writer.Write(" ");
                writer.Write(m.Value.ToString(CultureInfo.InvariantCulture));
                writer.Write(" ");

                // The timestamp is an int64 (milliseconds since epoch, i.e. 1970-01-01 00:00:00 UTC, excluding
                // leap seconds), represented as required by Go's ParseInt() function.
                writer.Write(now);

                // Prometheus' text-based format is line oriented. Lines are separated
                // by a line feed character (\n). The last line must end with a line
                // feed character. Empty lines are ignored.
                writer.Write("\n");
            }

            static string GetLabelAndValue(string label, string value)
            {
                var safeKey = GetSafeLabelName(label);
                var safeValue = GetSafeLabelValue(value);
                return $"{safeKey}=\"{safeValue}\"";
            }
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

        private static string GetSafeMetricName(string name) => GetSafeName(name, FirstCharacterNameCharset, NameCharset);

        private static string GetSafeLabelName(string name) => GetSafeName(name, FirstCharacterLabelCharset, LabelCharset);

        private static string GetSafeLabelValue(string value)
        {
            // label_value can be any sequence of UTF-8 characters, but the backslash
            // (\), double-quote ("), and line feed (\n) characters have to be escaped
            // as \\, \", and \n, respectively.

            var result = value.Replace("\\", "\\\\");
            result = result.Replace("\n", "\\n");
            result = result.Replace("\"", "\\\"");

            return result;
        }

        private static string GetSafeMetricDescription(string description)
        {
            // HELP lines may contain any sequence of UTF-8 characters(after the metric name), but the backslash
            // and the line feed characters have to be escaped as \\ and \n, respectively.Only one HELP line may
            // exist for any given metric name.
            var result = description.Replace(@"\", @"\\");
            result = result.Replace("\n", @"\n");

            return result;
        }

        internal class PrometheusMetricValueBuilder
        {
            public readonly ICollection<Tuple<string, string>> Labels = new List<Tuple<string, string>>();
            public double Value;
            public string Name;

            public PrometheusMetricValueBuilder WithLabel(string name, string value)
            {
                this.Labels.Add(new Tuple<string, string>(name, value));
                return this;
            }

            public PrometheusMetricValueBuilder WithValue(long metricValue)
            {
                this.Value = metricValue;
                return this;
            }

            public PrometheusMetricValueBuilder WithValue(double metricValue)
            {
                this.Value = metricValue;
                return this;
            }

            public PrometheusMetricValueBuilder WithName(string name)
            {
                this.Name = name;
                return this;
            }
        }
    }
}
