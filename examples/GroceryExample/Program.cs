// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;

#pragma warning disable CS0618

namespace GroceryExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create Metric Pipeline

            var sdk = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
                .SetPushInterval(TimeSpan.FromMilliseconds(1000))
                .SetProcessor(new MyMetricProcessor())
                .SetExporter(new MyMetricExporter())
                .Build()
                ;

            var store = new GroceryStore("Portland");

            store.ProcessOrder("customerA", ("potato", 2), ("tomato", 3));
            store.ProcessOrder("customerB", ("tomato", 10));
            store.ProcessOrder("customerC", ("potato", 2));
            store.ProcessOrder("customerA", ("tomato", 1));

            // Wait for stuff to run
            Task.Delay(5000).Wait();

            // Shutdown Metric Pipeline
            // sdk.Shutdown();
        }
    }
}
