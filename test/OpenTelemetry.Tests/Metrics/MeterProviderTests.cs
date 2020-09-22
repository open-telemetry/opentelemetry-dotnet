// <copyright file="MeterProviderTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MeterProviderTests : IDisposable
    {
        public MeterProviderTests()
        {
            MeterProvider.Reset();
        }

        [Fact]
        public void MeterProvider_Default()
        {
            Assert.NotNull(MeterProvider.Default);
            var defaultMeter = MeterProvider.Default.GetMeter(string.Empty);
            Assert.NotNull(defaultMeter);
            Assert.Same(defaultMeter, MeterProvider.Default.GetMeter("named meter"));

            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<NoopCounterMetric<double>>(counter);
        }

        [Fact]
        public void MeterProvider_SetDefault()
        {
            var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
            MeterProvider.SetDefault(meterProvider);

            var defaultMeter = MeterProvider.Default.GetMeter(string.Empty);
            Assert.NotNull(defaultMeter);
            Assert.IsType<MeterSdk>(defaultMeter);

            Assert.NotSame(defaultMeter, MeterProvider.Default.GetMeter("named meter"));

            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<DoubleCounterMetricSdk>(counter);
        }

        [Fact]
        public void MeterProvider_SetDefaultNull()
        {
            Assert.Throws<ArgumentNullException>(() => MeterProvider.SetDefault(null));
        }

        [Fact]
        public void MeterProvider_SetDefaultTwice_Throws()
        {
            var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
            MeterProvider.SetDefault(meterProvider);
            Assert.Throws<InvalidOperationException>(() => MeterProvider.SetDefault(meterProvider));
        }

        [Fact]
        public void MeterProvider_UpdateDefault_CachedTracer()
        {
            var defaultMeter = MeterProvider.Default.GetMeter(string.Empty);
            var noOpCounter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<NoopCounterMetric<double>>(noOpCounter);

            var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
            MeterProvider.SetDefault(meterProvider);
            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<DoubleCounterMetricSdk>(counter);

            var newdefaultMeter = MeterProvider.Default.GetMeter(string.Empty);
            Assert.NotSame(defaultMeter, newdefaultMeter);
            Assert.IsType<MeterSdk>(newdefaultMeter);
        }

        public void Dispose()
        {
            MeterProvider.Reset();
        }
    }
}
