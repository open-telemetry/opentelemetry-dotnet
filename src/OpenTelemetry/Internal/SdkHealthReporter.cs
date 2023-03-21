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

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Internal
{
    internal class SdkHealthReporter
    {
        private const string ProviderIdKey = "provider.id";

        private const string ProviderNameKey = "provider.name";

        private readonly string providerId;

        private readonly string providerName;

        internal SdkHealthReporter(string providerId, string providerName)
        {
            this.providerId = providerId;
            this.providerName = providerName;
        }

        private static Meter InternalMeter { get; } = new Meter("OpenTelemetry.Sdk");

        private static Counter<long> BatchExportProcessorDroppedCount { get; } = InternalMeter.CreateCounter<long>("dotnet.sdk.batchprocessor.dropped_count");

        internal void ReportBatchProcessorDroppedCount(long droppedCount, ref TagList droppedCountTags)
        {
            droppedCountTags.Add(ProviderIdKey, this.providerId);
            droppedCountTags.Add(ProviderNameKey, this.providerName);
            BatchExportProcessorDroppedCount.Add(droppedCount, droppedCountTags);
        }
    }
}
