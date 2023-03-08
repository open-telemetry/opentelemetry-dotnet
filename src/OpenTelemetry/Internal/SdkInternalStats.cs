// <copyright file="SdkInternalStats.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Internal
{
    internal sealed class SdkInternalStats : IDisposable
    {
        internal static SdkInternalStats Instance = new();
        private const string SdkInternalStatsMeterName = "SdkInternalStats";
        private readonly Meter sdkInternalStatsMeter;

        private SdkInternalStats()
        {
            this.sdkInternalStatsMeter = new Meter(SdkInternalStatsMeterName, "1.0");
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                Instance.Dispose();
            };
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.sdkInternalStatsMeter.Dispose();
            }
        }
    }
}
