// <copyright file="DefaultMeter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Net.NetworkInformation;
    using OpenTelemetry.Metrics.Implementation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;

    /// <summary>
    /// No-op implementation of a meter interface.
    /// </summary>
    public class DefaultMeter : IMeter
    {
        private static CounterDoubleBuilder counterDoubleBuilder = new CounterDoubleBuilder();
        private static CounterLongBuilder counterLongBuilder = new CounterLongBuilder();
        private static GaugeDoubleBuilder gaugeDoubleBuilder = new GaugeDoubleBuilder();
        private static GaugeLongBuilder gaugeLongBuilder = new GaugeLongBuilder();
        private static MeasureBuilder measureBuilder = new MeasureBuilder();

        /// <inheritdoc/>
        public ICounterDoubleBuilder GetCounterDoubleBuilder(string name) => counterDoubleBuilder;

        /// <inheritdoc/>
        public ICounterLongBuilder GetCounterLongBuilder(string name) => counterLongBuilder;

        /// <inheritdoc/>
        public IGaugeDoubleBuilder GetGaugeDoubleBuilder(string name) => gaugeDoubleBuilder;

        /// <inheritdoc/>
        public IGaugeLongBuilder GetGaugeLongBuilder(string name) => gaugeLongBuilder;

        /// <inheritdoc/>
        public IMeasureBuilder GetMeasureBuilder(string name) => measureBuilder;

        /// <inheritdoc/>
        public void Record(IEnumerable<IMeasurement> measurements)
        {
        }

        /// <inheritdoc/>
        public void Record(IEnumerable<IMeasurement> measurements, ITagContext tagContext)
        {
        }

        /// <inheritdoc/>
        public void Record(IEnumerable<IMeasurement> measurements, ITagContext tagContext, SpanContext spanContext)
        {
        }

        private class CounterDoubleTimeSeries : ICounterDoubleTimeSeries
        {
            public void Add(double delta)
            {
            }

            public void Set(double val)
            {
            }
        }

        private class CounterDouble : ICounterDouble
        {
            private static CounterDoubleTimeSeries timeSeries = new CounterDoubleTimeSeries();

            public void Clear()
            {
            }

            public ICounterDoubleTimeSeries GetDefaultTimeSeries() => timeSeries;

            public ICounterDoubleTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => timeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class CounterDoubleBuilder : ICounterDoubleBuilder
        {
            private static CounterDouble counterDouble = new CounterDouble();

            public IMetric<ICounterDoubleTimeSeries> Build() => counterDouble;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetResource(Resource resource) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetUnit(string unit) => this;
        }

        private class CounterLongTimeSeries : ICounterLongTimeSeries
        {
            public void Add(long delta)
            {
            }

            public void Set(long val)
            {
            }
        }

        private class CounterLong : ICounterLong
        {
            private static CounterLongTimeSeries timeSeries = new CounterLongTimeSeries();

            public void Clear()
            {
            }

            public ICounterLongTimeSeries GetDefaultTimeSeries() => timeSeries;

            public ICounterLongTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => timeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class CounterLongBuilder : ICounterLongBuilder
        {
            private static CounterLong counterLong = new CounterLong();

            public IMetric<ICounterLongTimeSeries> Build() => counterLong;

            public IMetricBuilder<ICounterLongTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetResource(Resource resource) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetUnit(string unit) => this;
        }

        private class GaugeDoubleTimeSeries : IGaugeDoubleTimeSeries
        {
            public void Add(double delta)
            {
            }

            public void Set(double val)
            {
            }
        }

        private class GaugeDouble : IGaugeDouble
        {
            private static GaugeDoubleTimeSeries timeSeries = new GaugeDoubleTimeSeries();

            public void Clear()
            {
            }

            public IGaugeDoubleTimeSeries GetDefaultTimeSeries() => timeSeries;

            public IGaugeDoubleTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => timeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class GaugeDoubleBuilder : IGaugeDoubleBuilder
        {
            private static GaugeDouble gaugeDouble = new GaugeDouble();

            public IMetric<IGaugeDoubleTimeSeries> Build() => gaugeDouble;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetResource(Resource resource) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetUnit(string unit) => this;
        }

        private class GaugeLongTimeSeries : IGaugeLongTimeSeries
        {
            public void Add(long delta)
            {
            }

            public void Set(long val)
            {
            }
        }

        private class GaugeLong : IGaugeLong
        {
            private static GaugeLongTimeSeries timeSeries = new GaugeLongTimeSeries();

            public void Clear()
            {
            }

            public IGaugeLongTimeSeries GetDefaultTimeSeries() => timeSeries;

            public IGaugeLongTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => timeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class GaugeLongBuilder : IGaugeLongBuilder
        {
            private static GaugeLong counterLong = new GaugeLong();

            public IMetric<IGaugeLongTimeSeries> Build() => counterLong;

            public IMetricBuilder<IGaugeLongTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetResource(Resource resource) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetUnit(string unit) => this;
        }

        private class Measurement : IMeasurement
        {
        }

        private class Measure : IMeasure
        {
            private static IMeasurement measurement = new Measurement();

            public IMeasurement CreateDoubleMeasurement(double value) => measurement;

            public IMeasurement CreateLongMeasurement(long value) => measurement;
        }

        private class MeasureBuilder : IMeasureBuilder
        {
            private static Measure measure = new Measure();

            public IMeasure Build() => measure;

            public IMeasureBuilder SetDescription(string description) => this;

            public IMeasureBuilder SetType(MeasureType type) => this;

            public IMeasureBuilder SetUnit(string unit) => this;
        }
    }
}
