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

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilder
    {
        private readonly List<string> meterSources = new List<string>();
        private int defaultCollectionPeriodMilliseconds = 1000;

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<KeyValuePair<MetricProcessor, int>> ExportProcessors { get; } = new List<KeyValuePair<MetricProcessor, int>>();

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

        internal MeterProviderBuilderSdk SetDefaultCollectionPeriod(int periodMilliseconds)
        {
            this.defaultCollectionPeriodMilliseconds = periodMilliseconds;
            return this;
        }

        internal MeterProviderBuilderSdk AddMeasurementProcessor(MeasurementProcessor processor)
        {
            this.MeasurementProcessors.Add(processor);
            return this;
        }

        internal MeterProviderBuilderSdk AddExporter(MetricProcessor processor)
        {
            this.ExportProcessors.Add(new KeyValuePair<MetricProcessor, int>(processor, this.defaultCollectionPeriodMilliseconds));
            return this;
        }

        internal MeterProviderBuilderSdk AddExporter(MetricProcessor processor, int periodMilliseconds)
        {
            this.ExportProcessors.Add(new KeyValuePair<MetricProcessor, int>(processor, periodMilliseconds));
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
            switch (aggregator)
            {
                default:
                case Aggregator.NONE:
                    aggs = new IAggregator[0];
                    break;

                case Aggregator.GAUGE:
                    {
                        aggs = new IAggregator[]
                        {
                            new GaugeMetricAggregator(),
                        };

                        break;
                    }

                case Aggregator.SUM:
                    {
                        var mon = false;

                        if (aggregatorParam is bool b)
                        {
                            mon = b;
                        }

                        aggs = new IAggregator[]
                        {
                            new SumMetricAggregator(true, mon),
                        };

                        break;
                    }

                case Aggregator.UPDOWN:
                    {
                        var mon = false;

                        if (aggregatorParam is bool b)
                        {
                            mon = b;
                        }

                        aggs = new IAggregator[]
                        {
                            new SumMetricAggregator(false, mon),
                        };

                        break;
                    }

                case Aggregator.SUMMARY:
                    {
                        var mon = false;

                        if (aggregatorParam is bool b)
                        {
                            mon = b;
                        }

                        aggs = new IAggregator[]
                        {
                            new SummaryMetricAggregator(mon),
                        };

                        break;
                    }

                case Aggregator.HISTOGRAM:
                    {
                        var cum = false;

                        if (aggregatorParam is bool b)
                        {
                            cum = b;
                        }

                        aggs = new IAggregator[]
                        {
                            new HistogramMetricAggregator(cum),
                        };

                        break;
                    }
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
                this.meterSources,
                this.ViewConfigs.ToArray(),
                this.MeasurementProcessors.ToArray(),
                this.ExportProcessors.ToArray());
        }
    }
}
