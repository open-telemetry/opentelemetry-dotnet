// <copyright file="SdkHealthReporter.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Internal
{
    internal sealed class SdkHealthReporter
    {
        private static readonly Dictionary<string, Func<Measurement<long>>> MeasurementDroppedCountCallbacks = new();

        private static readonly Meter InternalMeter = new Meter("OpenTelemetry.Sdk");

        private static readonly ObservableCounter<long> MeasurementDroppedCountObs = InternalMeter.CreateObservableCounter("otel.dotnet.sdk.measurement.dropped_count", GetMeasurementDroppedCounts);

        internal static void AddMeasurementDroppedCountCallback(string instrumentName, Func<Measurement<long>> droppedCountCallBack)
        {
            lock (MeasurementDroppedCountCallbacks)
            {
                MeasurementDroppedCountCallbacks.Add(instrumentName, droppedCountCallBack);
            }
        }

        internal static void RemoveMeasurementDroppedCountCallback(string instrumentName)
        {
            lock (MeasurementDroppedCountCallbacks)
            {
                MeasurementDroppedCountCallbacks.Remove(instrumentName);
            }
        }

        internal static IEnumerable<Measurement<long>> GetMeasurementDroppedCounts()
        {
            lock (MeasurementDroppedCountCallbacks)
            {
                foreach (var kvp in MeasurementDroppedCountCallbacks)
                {
                    yield return kvp.Value.Invoke();
                }
            }
        }
    }
}
