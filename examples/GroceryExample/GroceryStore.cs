// <copyright file="Global.asax.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

#pragma warning disable CS0618

namespace GroceryExample
{
    public class GroceryStore
    {
        private static Dictionary<string, double> priceList = new Dictionary<string, double>()
        {
            { "potato", 1.10 },
            { "tomato", 3.00 },
        };

        private string storeName;

        private CounterMetric<long> itemCounter;
        
        private CounterMetric<double> cashCounter;

        private BoundCounterMetric<double> boundCashCounter;

        public GroceryStore(string storeName)
        {
            this.storeName = storeName;

            // Setup Metrics

            Meter meter = MeterProvider.Default.GetMeter("GroceryStore", "1.0.0");

            itemCounter = meter.CreateInt64Counter("item_counter");

            cashCounter = meter.CreateDoubleCounter("cash_counter");

            var labels = new MyLabelSet(
                new KeyValuePair<string, string>("Store", "Portland"));

            boundCashCounter = cashCounter.Bind(labels);
        }

        public void ProcessOrder(string customer, params (string name, int qty)[] items)
        {
            double totalPrice = 0;

            foreach (var item in items)
            {
                totalPrice += item.qty * priceList[item.name];

                // Record Metric

                var labels = new MyLabelSet(
                    new KeyValuePair<string, string>("Store", "Portland"),
                    new KeyValuePair<string, string>("Customer", customer),
                    new KeyValuePair<string, string>("Item", item.name));

                itemCounter.Add(default(SpanContext), item.qty, labels);
            }

            // Record Metric

            var labels2 = new MyLabelSet(
                new KeyValuePair<string, string>("Store", "Portland"),
                new KeyValuePair<string, string>("Customer", customer));

            cashCounter.Add(default(SpanContext), totalPrice, labels2);

            boundCashCounter.Add(default(SpanContext), totalPrice);
        }
    }
}
