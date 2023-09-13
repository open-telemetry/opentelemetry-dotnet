// <copyright file="MetricPointReclaimTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Tests.Metrics
{
    public class MetricPointReclaimTest
    {
        private const int MeasurementCount = 10;

        private static readonly Meter Meter = new(nameof(MetricPointReclaimTest), "1.0.0");

        private static readonly List<(int, int)> Data = new()
        {
            (1, 2),
            (2, 7),
            (2, 5),
            (3, 5),
            (4, 5),
            (4, 6),
            (2, 0),
            (2, 1),
            (3, 0),
            (1, 3),
            (1, 1),
            (2, 1),
            (3, 4),
            (2, 5),
            (1, 3),
            (0, 2),
            (2, 4),
            (1, 9),
            (3, 1),
            (4, 9),
            (1, 1),
            (4, 1),
            (4, 4),
            (1, 4),
        };

        [Fact]
        public void Verify_DataPointReclaimLabelConsistency()
        {
            List<Measurement<int>> measurements = new();

            for (int i = 0; i < MeasurementCount; ++i)
            {
                measurements.Add(new(i + 1, new KeyValuePair<string, object>("num", i + 1), new KeyValuePair<string, object>("aaa", i + 1))); // unsorted.
            }

            List<Metric> metrics = new();
            using var meters = Sdk.CreateMeterProviderBuilder()
            .AddMeter(Meter.Name)
            .SetMaxMetricPointsPerMetricStream((MeasurementCount / 2) + 1)
            .AddInMemoryExporter(metrics)
            .Build();

            var g = Meter.CreateObservableGauge("gauge", () =>
            {
                int skip = 0, take = 0;
                if (Data.Count > 0)
                {
                    (skip, take) = Data.ElementAt(0);
                    Data.RemoveAt(0);
                }

                return measurements.Skip(skip).Take(take);
            });

            void VerifyLabelsAndValue(Metric metric)
            {
                foreach (var datapoint in metric.GetMetricPoints())
                {
                    var tags = new Dictionary<string, object>();
                    foreach (var tag in datapoint.Tags)
                    {
                        tags.Add(tag.Key, tag.Value);
                    }

                    var expected = datapoint.GetGaugeLastValueLong();
                    if (tags.TryGetValue("num", out var result))
                    {
                        Assert.Equal(expected, Convert.ToInt64(result));
                    }
                }
            }

            while (Data.Count > 0)
            {
                meters.ForceFlush();
                metrics.ForEach(VerifyLabelsAndValue);
                metrics.Clear();
            }
        }
    }
}
