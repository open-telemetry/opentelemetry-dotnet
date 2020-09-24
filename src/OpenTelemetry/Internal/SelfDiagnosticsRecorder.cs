// <copyright file="SelfDiagnosticsRecorder.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// SelfDiagnosticsRecorder class records the events from event sources to a local file,
    /// which is provided as a stream object by SelfDiagnosticsConfigRefresher class.
    /// The file size is bound to a upper limit. Once the write position reaches the end, it will
    /// be reset to the beginning of the file.
    /// </summary>
    internal class SelfDiagnosticsRecorder : IDisposable
    {
        private readonly SelfDiagnosticsConfigRefresher configRefresher;
        private bool disposedValue;

        public SelfDiagnosticsRecorder(SelfDiagnosticsConfigRefresher configRefresher)
        {
            this.configRefresher = configRefresher;
        }

        public void RecordEvent(EventWrittenEventArgs eventData)
        {
            // TODO: retrieve the file stream object from configRefresher and write to it
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.configRefresher.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}
