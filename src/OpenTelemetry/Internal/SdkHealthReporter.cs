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
        internal SdkHealthReporter(string providerId, string providerName)
        {
            this.ProviderId = providerId;
            this.ProviderName = providerName;
        }

        internal string ProviderId { get; }

        internal string ProviderName { get; }

        private static Meter InternalMeter { get; } = new Meter("OpenTelemetry.Sdk");

        private static Counter<long> BatchExportProcessorDroppedCount { get; } = InternalMeter.CreateCounter<long>("otel.dotnet.sdk.batchprocessor.dropped_count");

        internal void ReportBatchProcessorDroppedCount(long droppedCount, params KeyValuePair<string, object?>[] droppedCountTags)
        {
            BatchExportProcessorDroppedCount.Add(droppedCount, droppedCountTags);
        }
    }
}
