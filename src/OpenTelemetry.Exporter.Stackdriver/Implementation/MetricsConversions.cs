// <copyright file="MetricsConversions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Collections.Generic;
using Google.Api;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Stats;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using static Google.Api.Distribution.Types;
using static Google.Api.MetricDescriptor.Types;

namespace OpenTelemetry.Exporter.Stackdriver.Implementation
{
    /// <summary>
    /// Conversion methods from OpenTelemetry Stats API to Stackdriver Metrics API.
    /// </summary>
    internal static class MetricsConversions
    {
        /// <summary>
        /// Converts between OpenTelemetry aggregation and Stackdriver metric kind.
        /// </summary>
        /// <param name="aggregation">Stats Aggregation.</param>
        /// <returns>Stackdriver Metric Kind.</returns>
        public static MetricKind ToMetricKind(
            this IAggregation aggregation)
        {
            return aggregation.Match(
                v => MetricKind.Cumulative, // Sum
                v => MetricKind.Cumulative, // Count
                v => MetricKind.Cumulative, // Mean
                v => MetricKind.Cumulative, // Distribution
                v => MetricKind.Gauge,      // Last value
                v => MetricKind.Unspecified); // Default
        }

        /// <summary>
        /// Converts from OpenTelemetry Measure+Aggregation to Stackdriver's ValueType.
        /// </summary>
        /// <param name="measure">OpenTelemetry Measure definition.</param>
        /// <param name="aggregation">OpenTelemetry Aggregation definition.</param>
        /// <returns><see cref="ValueType"/>.</returns>
        public static ValueType ToValueType(
            this IMeasure measure, IAggregation aggregation)
        {
            var metricKind = aggregation.ToMetricKind();
            if (aggregation is IDistribution && (metricKind == MetricKind.Cumulative || metricKind == MetricKind.Gauge))
            {
                return ValueType.Distribution;
            }

            if (measure is IMeasureDouble && (metricKind == MetricKind.Cumulative || metricKind == MetricKind.Gauge))
            {
                return ValueType.Double;
            }

            if (measure is IMeasureLong && (metricKind == MetricKind.Cumulative || metricKind == MetricKind.Gauge))
            {
                return ValueType.Int64;
            }

            // TODO - zeltser - we currently don't support money and string as OpenTelemetry doesn't support them yet
            return ValueType.Unspecified;
        }

        public static LabelDescriptor ToLabelDescriptor(this string tagKey)
        {
            var labelDescriptor = new LabelDescriptor();
            
            labelDescriptor.Key = GetStackdriverLabelKey(tagKey);
            labelDescriptor.Description = Constants.LabelDescription;

            // TODO - zeltser - Now we only support string tags
            labelDescriptor.ValueType = LabelDescriptor.Types.ValueType.String;
            return labelDescriptor;
        }

        public static Google.Api.Distribution CreateDistribution(
            IDistributionData distributionData,
            IBucketBoundaries bucketBoundaries)
        {
            var bucketOptions = bucketBoundaries.ToBucketOptions();
            var distribution = new Google.Api.Distribution
            {
                BucketOptions = bucketOptions,
                BucketCounts = { CreateBucketCounts(distributionData.BucketCounts) },
                Count = distributionData.Count,
                Mean = distributionData.Mean,
                SumOfSquaredDeviation = distributionData.SumOfSquaredDeviations,
                Range = new Range { Max = distributionData.Max, Min = distributionData.Min },
            };

            return distribution;
        }

        /// <summary>
        /// Creates Stackdriver MetricDescriptor from OpenTelemetry View.
        /// </summary>
        /// <param name="metricDescriptorTypeName">Metric Descriptor full type name.</param>
        /// <param name="view">OpenTelemetry View.</param>
        /// <param name="project">Google Cloud Project Name.</param>
        /// <param name="domain">The Domain.</param>
        /// <param name="displayNamePrefix">Display Name Prefix.</param>
        /// <returns><see cref="MetricDescriptor"/>.</returns>
        public static MetricDescriptor CreateMetricDescriptor(
            string metricDescriptorTypeName,
            IView view,
            ProjectName project,
            string domain,
            string displayNamePrefix)
        {
            var metricDescriptor = new MetricDescriptor();
            var viewName = view.Name.AsString;

            metricDescriptor.Name = string.Format($"projects/{project.ProjectId}/metricDescriptors/{metricDescriptorTypeName}");
            metricDescriptor.Type = metricDescriptorTypeName;
            metricDescriptor.Description = view.Description;
            metricDescriptor.DisplayName = GetDisplayName(viewName, displayNamePrefix);

            foreach (var tagKey in view.Columns)
            {
                var labelDescriptor = tagKey.ToLabelDescriptor();
                metricDescriptor.Labels.Add(labelDescriptor);
            }

            metricDescriptor.Labels.Add(
                new LabelDescriptor
                {
                    Key = Constants.OpenTelemetryTask,
                    Description = Constants.OpenTelemetryTaskDescription,
                    ValueType = LabelDescriptor.Types.ValueType.String,
                });

            var unit = GetUnit(view.Aggregation, view.Measure);
            metricDescriptor.Unit = unit;
            metricDescriptor.MetricKind = view.Aggregation.ToMetricKind();
            metricDescriptor.ValueType = view.Measure.ToValueType(view.Aggregation);

            return metricDescriptor;
        }

        public static TypedValue CreateTypedValue(
            IAggregation aggregation,
            IAggregationData aggregationData)
        {
            return aggregationData.Match(
                v => new TypedValue { DoubleValue = v.Sum }, // Double
                v => new TypedValue { Int64Value = v.Sum }, // Long
                v => new TypedValue { Int64Value = v.Count }, // Count
                v => new TypedValue { DoubleValue = v.Count }, // Mean
                v => new TypedValue { DistributionValue = CreateDistribution(v, ((IDistribution)aggregation).BucketBoundaries) }, // Distribution
                v => new TypedValue { DoubleValue = v.LastValue }, // LastValue Double
                v => new TypedValue { Int64Value = v.LastValue }, // LastValue Long
                v => new TypedValue { BoolValue = false }); // Default
        }

        // Create a Metric using the TagKeys and TagValues.

        /// <summary>
        /// Generate Stackdriver Metric from OpenTelemetry View.
        /// </summary>
        /// <param name="view">A <see cref="IView"/>.</param>
        /// <param name="tagValues">A list of <see cref="string"/>.</param>
        /// <param name="metricDescriptor">Stackdriver Metric Descriptor.</param>
        /// <param name="domain">The domain.</param>
        /// <returns><see cref="Metric"/>.</returns>
        public static Metric GetMetric(
            IView view,
            IReadOnlyList<string> tagValues,
            MetricDescriptor metricDescriptor,
            string domain)
        {
            var metric = new Metric();
            metric.Type = metricDescriptor.Type;

            var columns = view.Columns;

            // Populate metric labels
            for (var i = 0; i < tagValues.Count; i++)
            {
                var key = columns[i];
                var value = tagValues[i];
                if (value == null)
                {
                    continue;
                }

                var labelKey = GetStackdriverLabelKey(key);
                metric.Labels.Add(labelKey, value);
            }

            metric.Labels.Add(Constants.OpenTelemetryTask, Constants.OpenTelemetryTaskValueDefault);

            // TODO - zeltser - make sure all the labels from the metric descriptor were fulfilled
            return metric;
        }

        /// <summary>
        /// Convert ViewData to a list of TimeSeries, so that ViewData can be uploaded to Stackdriver.
        /// </summary>
        /// <param name="viewData">OpenTelemetry View.</param>
        /// <param name="monitoredResource">Stackdriver Resource to which the metrics belong.</param>
        /// <param name="metricDescriptor">Stackdriver Metric Descriptor.</param>
        /// <param name="domain">The metrics domain (namespace).</param>
        /// <returns><see cref="List{T}"/>.</returns>
        public static List<TimeSeries> CreateTimeSeriesList(
            IViewData viewData,
            MonitoredResource monitoredResource,
            MetricDescriptor metricDescriptor,
            string domain)
        {
            var timeSeriesList = new List<TimeSeries>();
            if (viewData == null)
            {
                return timeSeriesList;
            }

            var view = viewData.View;
            var startTime = viewData.Start.ToTimestamp();

            // Each entry in AggregationMap will be converted into an independent TimeSeries object
            foreach (var entry in viewData.AggregationMap)
            {
                var timeSeries = new TimeSeries();
                var labels = entry.Key.Values;
                var points = entry.Value;
                
                timeSeries.Resource = monitoredResource;
                timeSeries.ValueType = view.Measure.ToValueType(view.Aggregation);
                timeSeries.MetricKind = view.Aggregation.ToMetricKind();

                timeSeries.Metric = GetMetric(view, labels, metricDescriptor, domain);

                var point = ExtractPointInInterval(viewData.Start, viewData.End, view.Aggregation, points);
                timeSeries.Points.Add(point);

                timeSeriesList.Add(timeSeries);
            }

            return timeSeriesList;
        }

        internal static string GetUnit(IAggregation aggregation, IMeasure measure)
        {
            if (aggregation is ICount)
            {
                return "1";
            }

            return measure.Unit;
        }

        internal static string GetDisplayName(string viewName, string displayNamePrefix)
        {
            return displayNamePrefix + viewName;
        }

        /// <summary>
        /// Creates Stackdriver Label name.
        /// </summary>
        /// <param name="label">OpenTelemetry label.</param>
        /// <returns>Label name that complies with Stackdriver label naming rules.</returns>
        internal static string GetStackdriverLabelKey(string label)
        {
            return label.Replace('/', '_');
        }

        private static Point ExtractPointInInterval(
            System.DateTimeOffset startTime,
            System.DateTimeOffset endTime, 
            IAggregation aggregation, 
            IAggregationData points)
        {
            return new Point
            {
                Value = CreateTypedValue(aggregation, points),
                Interval = CreateTimeInterval(startTime, endTime),
            };
        }

        /// <summary>
        /// Create a list of counts for Stackdriver from the list of counts in OpenTelemetry.
        /// </summary>
        /// <param name="bucketCounts">OpenTelemetry list of counts.</param>
        /// <returns><see cref="IEnumerable{T}"/>.</returns>
        private static IEnumerable<long> CreateBucketCounts(IReadOnlyList<long> bucketCounts)
        {
            // The first bucket (underflow bucket) should always be 0 count because the Metrics first bucket
            // is [0, first_bound) but Stackdriver distribution consists of an underflow bucket (number 0).
            var ret = new List<long>();
            ret.Add(0L);
            ret.AddRange(bucketCounts);
            return ret;
        }

        /// <summary>
        /// Converts <see cref="IBucketBoundaries"/> to Stackdriver's <see cref="BucketOptions"/>.
        /// </summary>
        /// <param name="bucketBoundaries">A <see cref="IBucketBoundaries"/> representing the bucket boundaries.</param>
        /// <returns><see cref="BucketOptions"/>.</returns>
        private static BucketOptions ToBucketOptions(this IBucketBoundaries bucketBoundaries)
        {
            // The first bucket bound should be 0.0 because the Metrics first bucket is
            // [0, first_bound) but Stackdriver monitoring bucket bounds begin with -infinity
            // (first bucket is (-infinity, 0))
            var bucketOptions = new BucketOptions
            {
                ExplicitBuckets = new BucketOptions.Types.Explicit
                {
                    Bounds = { 0.0 },
                },
            };
            bucketOptions.ExplicitBuckets.Bounds.AddRange(bucketBoundaries.Boundaries);

            return bucketOptions;
        }

        private static TimeInterval CreateTimeInterval(System.DateTimeOffset start, System.DateTimeOffset end)
        {
            return new TimeInterval { StartTime = start.ToTimestamp(), EndTime = end.ToTimestamp() };
        }
    }
}
