// <copyright file="ZPagesExporterEventSource.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.ZPages.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Exporter-ZPages")]
    internal class ZPagesExporterEventSource : EventSource
    {
        public static ZPagesExporterEventSource Log = new ZPagesExporterEventSource();

        [NonEvent]
        public void FailedExport(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.FailedExport(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void CanceledExport(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.CanceledExport(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void FailedProcess(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                this.FailedProcess(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Failed to export activities: '{0}'", Level = EventLevel.Error)]
        public void FailedExport(string exception)
        {
            this.WriteEvent(1, exception);
        }

        [Event(2, Message = "Canceled to export activities: '{0}'", Level = EventLevel.Error)]
        public void CanceledExport(string exception)
        {
            this.WriteEvent(2, exception);
        }

        [Event(3, Message = "Failed to process activity: '{0}'", Level = EventLevel.Error)]
        public void FailedProcess(string exception)
        {
            this.WriteEvent(3, exception);
        }
    }
}
