// <copyright file="MeterFactoryBaseTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Configuration;
using Xunit;

namespace OpenTelemetry.Metrics.Config.Test
{
    public class MeterFactoryBaseTests : IDisposable
    {
        public MeterFactoryBaseTests()
        {
            MeterFactoryBase.Default.Reset();
        }

        [Fact]
        public void MeterFactory_Default()
        {
            Assert.NotNull(MeterFactoryBase.Default);
            var defaultMeter = MeterFactoryBase.Default.GetMeter(string.Empty);
            Assert.NotNull(defaultMeter);
            Assert.Same(defaultMeter, MeterFactoryBase.Default.GetMeter("named meter"));

            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<NoOpCounterMetric<double>>(counter);
        }

        [Fact]
        public void MeterFactory_SetDefault()
        {
            var factory = MeterFactory.Create(b => { });
            MeterFactoryBase.SetDefault(factory);

            var defaultMeter = MeterFactoryBase.Default.GetMeter(string.Empty);
            Assert.NotNull(defaultMeter);
            Assert.IsType<MeterSdk>(defaultMeter);

            Assert.NotSame(defaultMeter, MeterFactoryBase.Default.GetMeter("named meter"));

            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<DoubleCounterMetricSdk>(counter);
        }

        [Fact]
        public void MeterFactory_SetDefaultNull()
        {
            Assert.Throws<ArgumentNullException>(() => MeterFactoryBase.SetDefault(null));
        }

        [Fact]
        public void MeterFactory_SetDefaultTwice_Throws()
        {
            MeterFactoryBase.SetDefault(MeterFactory.Create(b => { }));
            Assert.Throws<InvalidOperationException>(() => MeterFactoryBase.SetDefault(MeterFactory.Create(b => { })));
        }

        [Fact]
        public void MeterFactory_UpdateDefault_CachedTracer()
        {
            var defaultMeter = MeterFactoryBase.Default.GetMeter(string.Empty);
            var noOpCounter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<NoOpCounterMetric<double>>(noOpCounter);

            MeterFactoryBase.SetDefault(MeterFactory.Create(b => { }));
            var counter = defaultMeter.CreateDoubleCounter("ctr");
            Assert.IsType<DoubleCounterMetricSdk>(counter);

            var newdefaultMeter = MeterFactoryBase.Default.GetMeter(string.Empty);
            Assert.NotSame(defaultMeter, newdefaultMeter);
            Assert.IsType<MeterSdk>(newdefaultMeter);
        }

        public void Dispose()
        {
            MeterFactoryBase.Default.Reset();
        }
    }
}
