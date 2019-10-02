// <copyright file="OpenTelemetryEventSource.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Implementation
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Threading;
    using OpenTelemetry.Utils;

    [EventSource(Name = "OpenTelemetry-Base")]
    internal class OpenTelemetryEventSource : EventSource
    {
        public static readonly OpenTelemetryEventSource Log = new OpenTelemetryEventSource();

        [NonEvent]
        public void ExporterThrownExceptionWarning(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.ExporterThrownExceptionWarning(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Exporter failed to export items. Exception: {0}", Level = EventLevel.Warning)]
        public void ExporterThrownExceptionWarning(string ex)
        {
            this.WriteEvent(1, ex);
        }

        [NonEvent]
        public void FailedReadingEnvironmentVariableWarning(string environmentVariableName, Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.FailedReadingEnvironmentVariableWarning(environmentVariableName, ex.ToInvariantString());
            }
        }

        [Event(2, Message = "Failed to read environment variable {0}. Main library failed with security exception: {1}", Level = EventLevel.Warning)]
        public void FailedReadingEnvironmentVariableWarning(string environmentVariableName, string ex)
        {
            this.WriteEvent(3, environmentVariableName, ex);
        }
    }
}
