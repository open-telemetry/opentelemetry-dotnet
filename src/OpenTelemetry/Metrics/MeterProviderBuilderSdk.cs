// <copyright file="MeterProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilder
    {
        private readonly List<string> meterSources = new List<string>();
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<MetricProcessor> MetricProcessors { get; } = new List<MetricProcessor>();

        internal List<MetricView> ViewConfigs { get; } = new List<MetricView>();

        public override MeterProviderBuilder AddSource(params string[] names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(names)} contains null or whitespace string.");
                }

                this.meterSources.Add(name);
            }

            return this;
        }

        internal MeterProviderBuilderSdk AddMeasurementProcessor(MeasurementProcessor processor)
        {
            this.MeasurementProcessors.Add(processor);
            return this;
        }

        internal MeterProviderBuilderSdk AddMetricProcessor(MetricProcessor processor)
        {
            this.MetricProcessors.Add(processor);
            return this;
        }

        internal MeterProviderBuilderSdk SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            this.resourceBuilder = resourceBuilder ?? throw new ArgumentNullException(nameof(resourceBuilder));
            return this;
        }

        internal MeterProviderBuilderSdk AddView(string viewName, string viewDescription, Func<Instrument, bool> selector, Func<IAggregator[]> aggregators, params IViewRule[] rules)
        {
            var viewConfig = new MetricView(viewName, viewDescription, selector, aggregators, rules);
            this.ViewConfigs.Add(viewConfig);
            return this;
        }

        internal MeterProviderBuilderSdk AddView(
            string meterName,
            string meterVersion,
            string instrumentName,
            string instrumentKind,
            Aggregator aggregator,
            object aggregatorParam,
            string viewName,
            string viewDescription,
            string[] attributeKeys,
            string[] extraDimensions)
        {
            Func<Instrument, bool> selectorFunc = (inst) =>
            {
                if (meterName != null && meterName != inst.Meter.Name)
                {
                    return false;
                }

                if (meterVersion != null && meterVersion != inst.Meter.Version)
                {
                    return false;
                }

                if (instrumentName != null && instrumentName != inst.Name)
                {
                    return false;
                }

                if (instrumentKind != null && inst.GetType().Name.StartsWith(instrumentKind))
                {
                    return false;
                }

                return true;
            };

            IAggregator[] aggs;
            var agg = this.CreateAggregator(aggregator, aggregatorParam);
            if (agg != null)
            {
                aggs = new IAggregator[] { agg };
            }
            else
            {
                aggs = new IAggregator[0];
            }

            IViewRule[] rules = null;
            if (attributeKeys != null)
            {
                var rule = new IncludeTagRule((tag) =>
                {
                    foreach (var key in attributeKeys)
                    {
                        if (tag == key)
                        {
                            return true;
                        }
                    }

                    return false;
                });

                rules = new IViewRule[]
                {
                    rule,
                };
            }

            var viewConfig = new MetricView(viewName, viewDescription, selectorFunc, () => aggs, rules);
            this.ViewConfigs.Add(viewConfig);
            return this;
        }

        internal MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.resourceBuilder.Build(),
                this.meterSources,
                this.ViewConfigs.ToArray(),
                this.MeasurementProcessors.ToArray(),
                this.MetricProcessors.ToArray());
        }

        internal IAggregator CreateAggregator(Aggregator aggregator, object aggregatorParam)
        {
            IAggregator agg = null;

            switch (aggregator)
            {
                default:
                case Aggregator.NONE:
                    break;

                case Aggregator.GAUGE:
                    agg = new GaugeMetricAggregator();
                    break;

                case Aggregator.SUM:
                    agg = new SumMetricAggregator(false, false);
                    break;

                case Aggregator.SUM_MONOTONIC:
                    agg = new SumMetricAggregator(false, true);
                    break;

                case Aggregator.SUM_DELTA:
                    agg = new SumMetricAggregator(true, false);
                    break;

                case Aggregator.SUM_DELTA_MONOTONIC:
                    agg = new SumMetricAggregator(true, true);
                    break;

                case Aggregator.SUMMARY:
                    agg = new SummaryMetricAggregator();
                    break;

                case Aggregator.HISTOGRAM:
                case Aggregator.HISTOGRAM_DELTA:
                    {
                        var delta = aggregator == Aggregator.HISTOGRAM_DELTA;

                        double[] bounds;
                        if (aggregatorParam is double[] b)
                        {
                            bounds = b;
                        }
                        else
                        {
                            bounds = new double[] { 0.0 };
                        }

                        agg = new HistogramMetricAggregator(delta, bounds);
                        break;
                    }
            }

            return agg;
        }
    }
}
