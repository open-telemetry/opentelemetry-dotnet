// <copyright file="MeterFactoryBaseTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class MeterFactoryBaseTests
    {
        [Fact]
        public void MeterFactoryBase_Default()
        {
            Assert.NotNull(MeterFactoryBase.Default);
            var defaultMeter = MeterFactoryBase.Default.GetMeter("");
            Assert.NotNull(defaultMeter);
            Assert.IsType<NoOpMeter>(defaultMeter);

            var namedMeter = MeterFactoryBase.Default.GetMeter("named meter");
            //  The same NoOpMeter must be returned always.
            Assert.Same(defaultMeter, namedMeter);

            var counter = defaultMeter.CreateDoubleCounter("somename");
            Assert.IsType<NoOpCounter<double>>(counter);

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            var counterHandle = counter.GetHandle(labels1);
            Assert.IsType<NoOpCounterHandle<double>>(counterHandle);
        }
    }
}
