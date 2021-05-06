// <copyright file="MeasurementProcessor.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class MeasurementProcessor
        : BaseProcessor<MeasurementContext>
    {
        public override void OnStart(MeasurementContext data)
        {
            Console.WriteLine($"Start: {this.AsString(data)}");

            // // Replace datapoint
            // var oldArray = data.Point!.Tags.ToArray();
            // var newArray = new KeyValuePair<string, object?>[oldArray.Length + 1];
            // newArray[0] = new KeyValuePair<string, object?>("newLabel", "newValue");
            // oldArray.CopyTo(newArray, 1);
            // var dp = new DataPoint<int>(10000, newArray);
            // data.Point = dp;
        }

        public override void OnEnd(MeasurementContext data)
        {
            Console.WriteLine($"End: {this.AsString(data)}");
        }

        public string AsString(MeasurementContext data)
        {
            var tags = string.Join(",", data.Point!.Tags.ToArray());
            return $"{data.Instrument.Meter.Name}:{data.Instrument.Name}[{tags}] = {data.Point!.ValueAsString()}";
        }
    }
}
