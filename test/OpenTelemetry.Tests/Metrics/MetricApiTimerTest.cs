// <copyright file="MetricApiTimerTest.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTimerTest
    {
        [Fact]
        public void TestTimer()
        {
            using var meter = new Meter("BasicAllTest", "0.0.1");

            var timer = meter.CreateTimer<int>("timer");

            var tags = new KeyValuePair<string, object?>("location", "here2");

            // Example 1
            using (var mark1 = timer.Start(tags))
            {
                this.DoOperation();
            }

            // Example 2
            var mark2 = timer.Start(tags);
            this.DoOperation();
            timer.Stop(mark2);

            // Example 3
            var mark3 = timer.Start();
            this.DoOperation();
            timer.Stop(mark3, tags);
        }

        private void DoOperation()
        {
            Task.Delay(1000).Wait();
        }
    }
}
