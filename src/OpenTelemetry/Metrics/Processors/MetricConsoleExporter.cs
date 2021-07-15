// <copyright file="MetricConsoleExporter.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenTelemetry.Metrics
{
    public class MetricConsoleExporter : MetricProcessor
    {
        private string name;

        public MetricConsoleExporter()
            : this(string.Empty)
        {
        }

        public MetricConsoleExporter(string name)
        {
            this.name = name;
        }

        public override void OnEnd(MetricItem data)
        {
            foreach (var metric in data.Metrics)
            {
                var tags = metric.Attributes.ToArray().Select(k => $"{k.Key}={k.Value?.ToString()}");

                string valueDisplay = string.Empty;
                if (metric is ISumMetric sumMetric)
                {
                    if (sumMetric.Sum.Value is double doubleSum)
                    {
                        valueDisplay = ((double)doubleSum).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (sumMetric.Sum.Value is long longSum)
                    {
                        valueDisplay = ((long)longSum).ToString();
                    }
                }
                else if (metric is IGaugeMetric gaugeMetric)
                {
                    if (gaugeMetric.LastValue.Value is double doubleValue)
                    {
                        valueDisplay = ((double)doubleValue).ToString();
                    }
                    else if (gaugeMetric.LastValue.Value is long longValue)
                    {
                        valueDisplay = ((long)longValue).ToString();
                    }

                    // Qn: tags again ? gaugeMetric.LastValue.Tags
                }
                else if (metric is ISummaryMetric summaryMetric)
                {
                    valueDisplay = string.Format("Sum: {0} Count: {1}", summaryMetric.PopulationSum, summaryMetric.PopulationCount);
                }
                else if (metric is IHistogramMetric histogramMetric)
                {
                    valueDisplay = string.Format("Sum: {0} Count: {1}", histogramMetric.PopulationSum, histogramMetric.PopulationCount);
                }

                var kind = metric.GetType().Name;

                string time = $"{metric.StartTimeExclusive.ToLocalTime().ToString("HH:mm:ss.fff")} {metric.EndTimeInclusive.ToLocalTime().ToString("HH:mm:ss.fff")}";

                var msg = new StringBuilder($"Export[{this.name}] {time} {metric.Name} [{string.Join(";", tags)}] {kind} Value: {valueDisplay}, Details: {metric.ToDisplayString()}");

                if (!string.IsNullOrEmpty(metric.Instrument.Description))
                {
                    msg.Append($", Description: {metric.Instrument.Description}");
                }

                if (!string.IsNullOrEmpty(metric.Instrument.Unit))
                {
                    msg.Append($", Unit: {metric.Instrument.Unit}");
                }

                if (!string.IsNullOrEmpty(metric.Instrument.Meter.Name))
                {
                    msg.Append($", Meter: {metric.Instrument.Meter.Name}");

                    if (!string.IsNullOrEmpty(metric.Instrument.Meter.Version))
                    {
                        msg.Append($"/{metric.Instrument.Meter.Version}");
                    }
                }

                Console.WriteLine(msg);
            }
        }
    }
}
