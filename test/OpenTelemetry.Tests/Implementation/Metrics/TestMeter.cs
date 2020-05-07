// <copyright file="TestMeter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Test
{
    internal class TestMeter : MeterSdk
    {
        Action collectAction;

        internal TestMeter(string meterName, MetricProcessor metricProcessor, Action collectAction)
            : base(meterName, metricProcessor)
        {
            this.collectAction = collectAction;
        }

        public override void Collect()
        {
            this.collectAction();
        }
    }
}
