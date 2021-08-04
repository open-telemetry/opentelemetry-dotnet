// <copyright file="BatchExportProcessorEventSource.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;
using System.Security;
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-BatchExporter")]
    internal class BatchExportProcessorEventSource : EventSource
    {
        public static BatchExportProcessorEventSource Log = new BatchExportProcessorEventSource();

        [NonEvent]
        public void MissingPermissionsToReadEnvironmentVariable(SecurityException ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.MissingPermissionsToReadEnvironmentVariable(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Failed to parse environment variable: '{0}', value: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToParseEnvironmentVariable(string name, string value)
        {
            this.WriteEvent(1, name, value);
        }

        [Event(2, Message = "Missing permissions to read environment variable: '{0}'", Level = EventLevel.Warning)]
        public void MissingPermissionsToReadEnvironmentVariable(string exception)
        {
            this.WriteEvent(2, exception);
        }
    }
}
