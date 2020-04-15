// <copyright file="PushControllerTest.cs" company="OpenTelemetry Authors">
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

using System.Linq;
using System.Collections.Generic;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Trace;
using Xunit;
using OpenTelemetry.Metrics.Export;
using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics.Test
{
    public class PushControllerTest
    {
        [Fact]
        public void PushControllerPushesMetricAtConfiguredInterval()
        {
            var controllerPushIntervalInMsec = 100;
            var waitIntervalInMsec = (controllerPushIntervalInMsec * 3) + 200;
            var testExporter = new TestMetricExporter();
            var testProcessor = new TestMetricProcessor();
            var meterFactory = MeterFactory.Create(
                mb =>
                {
                    mb.SetMetricProcessor(testProcessor);
                    mb.SetMetricExporter(testExporter);
                    mb.SetMetricPushInterval(TimeSpan.FromMilliseconds(controllerPushIntervalInMsec));
                }
                );
            var meter1 = meterFactory.GetMeter("library1");
            var meter2 = meterFactory.GetMeter("library2");                       

            var meter1Counter1 = meter1.CreateInt64Counter("testCounter1");
            var meter1Counter2 = meter1.CreateInt64Counter("testCounter2");
            var meter2Counter1 = meter2.CreateInt64Counter("testCounter1");
            var meter2Counter2 = meter2.CreateInt64Counter("testCounter2");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            var ls = meter1.GetLabelSet(labels1);
            var context = default(SpanContext);

            meter1Counter1.Add(context, 100, ls);
            meter1Counter2.Add(context, 200, ls);
            meter2Counter1.Add(context, 300, ls);
            meter2Counter2.Add(context, 400, ls);

            meter1Counter1.Add(context, 100, ls);
            meter1Counter2.Add(context, 200, ls);
            meter2Counter1.Add(context, 300, ls);
            meter2Counter2.Add(context, 400, ls);

            Task.Delay(waitIntervalInMsec).Wait();
            
            Assert.Equal(1, testExporter.LongMetrics.Count(m => m.MetricName == "testCounter1"
            && m.MetricNamespace == "library1"
            && ((m.Data as SumData<long>).Sum == 200)));

            Assert.Equal(1, testExporter.LongMetrics.Count(m => m.MetricName == "testCounter2"
            && m.MetricNamespace == "library1"
            && ((m.Data as SumData<long>).Sum == 400)));

            Assert.Equal(1, testExporter.LongMetrics.Count(m => m.MetricName == "testCounter1"
            && m.MetricNamespace == "library2"
            && ((m.Data as SumData<long>).Sum == 600)));

            Assert.Equal(1, testExporter.LongMetrics.Count(m => m.MetricName == "testCounter2"
            && m.MetricNamespace == "library2"
            && ((m.Data as SumData<long>).Sum == 800)));
        }
    }
}
