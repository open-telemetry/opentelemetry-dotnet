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
    using OpenTelemetry.Metrics.Implementation;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;

    /// <summary>
    /// No-op implementation of a meter interface.
    /// </summary>
    public class DefaultMeter : IMeter
    {
        private static readonly CounterDoubleBuilder CounterDoubleBuilderValue = new CounterDoubleBuilder();
        private static readonly CounterLongBuilder CounterLongBuilderValue = new CounterLongBuilder();
        private static readonly GaugeDoubleBuilder GaugeDoubleBuilderValue = new GaugeDoubleBuilder();
        private static readonly GaugeLongBuilder GaugeLongBuilderValue = new GaugeLongBuilder();
        private static readonly MeasureBuilder MeasureBuilderValue = new MeasureBuilder();

        /// <inheritdoc/>
        public ICounterDoubleBuilder GetCounterDoubleBuilder(string name) => CounterDoubleBuilderValue;

        /// <inheritdoc/>
        public ICounterLongBuilder GetCounterLongBuilder(string name) => CounterLongBuilderValue;

        /// <inheritdoc/>
        public IGaugeDoubleBuilder GetGaugeDoubleBuilder(string name) => GaugeDoubleBuilderValue;

        /// <inheritdoc/>
        public IGaugeLongBuilder GetGaugeLongBuilder(string name) => GaugeLongBuilderValue;

        /// <inheritdoc/>
        public IMeasureBuilder GetMeasureBuilder(string name) => MeasureBuilderValue;

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
            private static readonly CounterDoubleTimeSeries TimeSeries = new CounterDoubleTimeSeries();

            public void Clear()
            {
            }

            public ICounterDoubleTimeSeries GetDefaultTimeSeries() => TimeSeries;

            public ICounterDoubleTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => TimeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class CounterDoubleBuilder : ICounterDoubleBuilder
        {
            private static readonly CounterDouble CounterDouble = new CounterDouble();

            public IMetric<ICounterDoubleTimeSeries> Build() => CounterDouble;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<ICounterDoubleTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

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
            private static readonly CounterLongTimeSeries TimeSeries = new CounterLongTimeSeries();

            public void Clear()
            {
            }

            public ICounterLongTimeSeries GetDefaultTimeSeries() => TimeSeries;

            public ICounterLongTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => TimeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class CounterLongBuilder : ICounterLongBuilder
        {
            private static readonly CounterLong CounterLong = new CounterLong();

            public IMetric<ICounterLongTimeSeries> Build() => CounterLong;

            public IMetricBuilder<ICounterLongTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<ICounterLongTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

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
            private static readonly GaugeDoubleTimeSeries TimeSeries = new GaugeDoubleTimeSeries();

            public void Clear()
            {
            }

            public IGaugeDoubleTimeSeries GetDefaultTimeSeries() => TimeSeries;

            public IGaugeDoubleTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => TimeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class GaugeDoubleBuilder : IGaugeDoubleBuilder
        {
            private static readonly GaugeDouble GaugeDouble = new GaugeDouble();

            public IMetric<IGaugeDoubleTimeSeries> Build() => GaugeDouble;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<IGaugeDoubleTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

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
            private static readonly GaugeLongTimeSeries TimeSeries = new GaugeLongTimeSeries();

            public void Clear()
            {
            }

            public IGaugeLongTimeSeries GetDefaultTimeSeries() => TimeSeries;

            public IGaugeLongTimeSeries GetOrCreateTimeSeries(IEnumerable<string> labelValues) => TimeSeries;

            public void RemoveTimeSeries(IEnumerable<string> labelValues)
            {
            }

            public void SetCallback(Action metricUpdater)
            {
            }
        }

        private class GaugeLongBuilder : IGaugeLongBuilder
        {
            private static readonly GaugeLong CounterLong = new GaugeLong();

            public IMetric<IGaugeLongTimeSeries> Build() => CounterLong;

            public IMetricBuilder<IGaugeLongTimeSeries> SetComponent(string component) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetConstantLabels(IDictionary<LabelKey, string> constantLabels) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetDescription(string description) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetLabelKeys(IEnumerable<LabelKey> labelKeys) => this;

            public IMetricBuilder<IGaugeLongTimeSeries> SetUnit(string unit) => this;
        }

        private class Measurement : IMeasurement
        {
        }

        private class Measure : IMeasure
        {
            private static readonly IMeasurement Measurement = new Measurement();

            public IMeasurement CreateDoubleMeasurement(double value) => Measurement;

            public IMeasurement CreateLongMeasurement(long value) => Measurement;
        }

        private class MeasureBuilder : IMeasureBuilder
        {
            private static readonly Measure Measure = new Measure();

            public IMeasure Build() => Measure;

            public IMeasureBuilder SetDescription(string description) => this;

            public IMeasureBuilder SetType(MeasureType type) => this;

            public IMeasureBuilder SetUnit(string unit) => this;
        }
    }
}
