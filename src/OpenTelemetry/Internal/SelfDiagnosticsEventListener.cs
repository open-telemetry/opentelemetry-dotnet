// <copyright file="SelfDiagnosticsEventListener.cs" company="OpenTelemetry Authors">
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
    /// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
    /// and record the events to a local file via the SelfDiagnosticsRecorder class.
    /// </summary>
    internal class SelfDiagnosticsEventListener : EventListener
    {
        private const string EventSourceNamePrefix = "OpenTelemetry-";
        private readonly EventLevel logLevel;
        private readonly SelfDiagnosticsRecorder eventRecorder;
        private bool disposedValue;

        public SelfDiagnosticsEventListener(EventLevel logLevel, SelfDiagnosticsRecorder eventRecorder)
        {
            this.logLevel = logLevel;
            this.eventRecorder = eventRecorder;
        }

        public override void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
#if NET452
                this.EnableEvents(eventSource, this.logLevel, (EventKeywords)(-1));
#else
                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
#endif
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.eventRecorder.RecordEvent(eventData);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.eventRecorder.Dispose();
                    base.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}
