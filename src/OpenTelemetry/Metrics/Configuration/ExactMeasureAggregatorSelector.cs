// <copyright file="ExactMeasureAggregatorSelector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Metrics.Configuration
{
    public class ExactMeasureAggregatorSelector : AggregatorSelector
    {
        public override Aggregator<T> SelectAggregator<T>(InstrumentKind instrumentKind)
        {
            switch (instrumentKind)
            {
                case InstrumentKind.COUNTER:
                    {
                        return new CounterSumAggregator<T>();
                    }

                case InstrumentKind.GAUGE:
                    {
                        return new GaugeAggregator<T>();
                    }

                case InstrumentKind.MEASURE:
                    {
                        return new MeasureExactAggregator<T>();
                    }

                default:
                    {
                        throw new Exception("Invalid instrument");
                    }
            }
        }
    }
}
