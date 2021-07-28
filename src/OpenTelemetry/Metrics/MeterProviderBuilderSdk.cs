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
        private static IAggregator[] EmptyAggregators = new IAggregator[0];

        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<string> meterSources = new List<string>();
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<MetricProcessor> MetricProcessors { get; } = new List<MetricProcessor>();

        internal List<MetricView> ViewConfigs { get; } = new List<MetricView>();

        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            if (instrumentationFactory == null)
            {
                throw new ArgumentNullException(nameof(instrumentationFactory));
            }

            this.instrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    instrumentationFactory));

            return this;
        }

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

        internal MeterProviderBuilderSdk AddView(string viewName, string viewDescription, Func<Instrument, bool> selectorFunc, Func<Instrument, IAggregator[]> aggregatorFunc, params IViewRule[] rules)
        {
            var viewConfig = new MetricView(viewName, viewDescription, selectorFunc, aggregatorFunc, rules);
            this.ViewConfigs.Add(viewConfig);
            return this;
        }

        internal MeterProviderBuilderSdk AddView(
            string meterName,
            string meterVersion,
            string instrumentName,
            string instrumentKind,
            Aggregator aggregator,
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

            Func<Instrument, IAggregator[]> aggregatorFunc;
            switch (aggregator)
            {
                case Aggregator.NONE:
                    aggregatorFunc = (inst) =>
                    {
                        return EmptyAggregators;
                    };
                    break;

                case Aggregator.SUM:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(false, false),
                        };
                    };
                    break;

                /*
                For future release when we want more aggregators.

                case Aggregator.SUM_MONOTONIC:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(false, true),
                        };
                    };
                    break;

                case Aggregator.SUM_DELTA_MONOTONIC:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(true, true),
                        };
                    };
                    break;

                case Aggregator.SUM_DELTA:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(true, false),
                        };
                    };
                    break;

                case Aggregator.GAUGE:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new GaugeMetricAggregator(),
                        };
                    };
                    break;

                case Aggregator.HISTOGRAM:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new HistogramMetricAggregator(false, new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 }),
                        };
                    };
                    break;

                case Aggregator.HISTOGRAM_DELTA:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new HistogramMetricAggregator(true, new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 }),
                        };
                    };
                    break;

                case Aggregator.SUMMARY:
                    aggregatorFunc = (inst) =>
                    {
                        return new IAggregator[]
                        {
                            new SummaryMetricAggregator(),
                        };
                    };
                    break;
                */

                case Aggregator.DEFAULT:
                default:
                    aggregatorFunc = DefaultAggregatorFunc;
                    break;
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

            var viewConfig = new MetricView(viewName, viewDescription, selectorFunc, aggregatorFunc, rules);
            this.ViewConfigs.Add(viewConfig);
            return this;
        }

        internal MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.resourceBuilder.Build(),
                this.meterSources,
                this.ViewConfigs.ToArray(),
                this.instrumentationFactories,
                this.MeasurementProcessors.ToArray(),
                this.MetricProcessors.ToArray());
        }

        static internal IAggregator[] DefaultAggregatorFunc(Instrument instrument)
        {
            var aggregators = new List<IAggregator>();

            Type instType = instrument.GetType().GetGenericTypeDefinition();

            if (instType == typeof(Counter<>))
            {
                var agg = new SumMetricAggregator(true, false);
                aggregators.Add(agg);
            }
            else if (instType == typeof(ObservableCounter<>))
            {
                var agg = new SumMetricAggregator(false, false);
                aggregators.Add(agg);
            }
            else if (instType == typeof(ObservableGauge<>))
            {
                var agg = new GaugeMetricAggregator();
                aggregators.Add(agg);
            }
            else if (instType == typeof(Histogram<>))
            {
                var agg = new HistogramMetricAggregator(true, new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 });
                aggregators.Add(agg);
            }

            return aggregators.ToArray();
        }

        // TODO: This is copied from TracerProviderBuilderSdk. Move to common location.
        internal readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<object> Factory;

            internal InstrumentationFactory(string name, string version, Func<object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }
}
