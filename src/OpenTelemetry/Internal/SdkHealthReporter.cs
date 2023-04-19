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
        private static readonly Dictionary<int, Func<Measurement<long>>> BatchExportProcessorDroppedCountCallbacks = new();

        private static readonly Meter InternalMeter = new Meter("OpenTelemetry.Sdk");

        private static readonly ObservableCounter<long> BatchExportProcessorDroppedCountObs = InternalMeter.CreateObservableCounter("otel.dotnet.sdk.batchprocessor.dropped_count", GetBatchExportProcessorDroppedCounts);

        internal SdkHealthReporter(string providerId, string providerName)
        {
            this.ProviderId = providerId;
            this.ProviderName = providerName;
        }

        internal string ProviderId { get; }

        internal string ProviderName { get; }

        internal static void AddBatchExportProcessorDroppedCountCallback(int batchExportProcessorId, Func<Measurement<long>> droppedCountCallBack)
        {
            lock (BatchExportProcessorDroppedCountCallbacks)
            {
                BatchExportProcessorDroppedCountCallbacks.Add(batchExportProcessorId, droppedCountCallBack);
            }
        }

        internal static void RemoveBatchExportProcessorDroppedCountCallback(int batchExportProcessorId)
        {
            lock (BatchExportProcessorDroppedCountCallbacks)
            {
                BatchExportProcessorDroppedCountCallbacks.Remove(batchExportProcessorId);
            }
        }

        internal static IEnumerable<Measurement<long>> GetBatchExportProcessorDroppedCounts()
        {
            lock (BatchExportProcessorDroppedCountObs)
            {
                foreach (var kvp in BatchExportProcessorDroppedCountCallbacks)
                {
                    yield return kvp.Value.Invoke();
                }
            }
        }
    }
}
