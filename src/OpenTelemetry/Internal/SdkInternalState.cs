// <copyright file="SdkInternalState.cs" company="OpenTelemetry Authors">
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
    internal sealed class SdkInternalState : IDisposable
    {
        internal static SdkInternalState Instance = new();
        private const string SdkInternalStateMeterName = "SdkInternalState";
        private readonly Meter sdkInternalStateMeter;

        private SdkInternalState()
        {
            this.sdkInternalStateMeter = new Meter(SdkInternalStateMeterName, "1.0");
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
                this.sdkInternalStateMeter.Dispose();
            }
        }
    }
}
