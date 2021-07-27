// <copyright file="OpenTelemetryProtocolExporterEventSource.cs" company="OpenTelemetry Authors">
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
using System.Security;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    [EventSource(Name = "OpenTelemetry-Exporter-OpenTelemetryProtocol")]
    internal class OpenTelemetryProtocolExporterEventSource : EventSource
    {
        public static readonly OpenTelemetryProtocolExporterEventSource Log = new OpenTelemetryProtocolExporterEventSource();

        [NonEvent]
        public void MissingPermissionsToReadEnvironmentVariable(SecurityException ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.MissingPermissionsToReadEnvironmentVariable(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void FailedToConvertToProtoDefinitionError(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedToConvertToProtoDefinitionError(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void FailedToReachCollector(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedToReachCollector(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void ExportMethodException(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.ExportMethodException(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Exporter failed to convert SpanData content into gRPC proto definition. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToConvertToProtoDefinitionError(string ex)
        {
            this.WriteEvent(1, ex);
        }

        [Event(2, Message = "Exporter failed send data to collector. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToReachCollector(string ex)
        {
            this.WriteEvent(2, ex);
        }

        [Event(3, Message = "Could not translate activity from class '{0}' and method '{1}', span will not be recorded.", Level = EventLevel.Informational)]
        public void CouldNotTranslateActivity(string className, string methodName)
        {
            this.WriteEvent(3, className, methodName);
        }

        [Event(4, Message = "Unknown error in export method: {0}", Level = EventLevel.Error)]
        public void ExportMethodException(string ex)
        {
            this.WriteEvent(4, ex);
        }

        [Event(5, Message = "Could not translate metric from class '{0}' and method '{1}', metric will not be recorded.", Level = EventLevel.Informational)]
        public void CouldNotTranslateMetric(string className, string methodName)
        {
            this.WriteEvent(5, className, methodName);
        }

        [Event(6, Message = "Failed to parse environment variable: '{0}', value: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToParseEnvironmentVariable(string name, string value)
        {
            this.WriteEvent(6, name, value);
        }

        [Event(7, Message = "Missing permissions to read environment variable: '{0}'", Level = EventLevel.Warning)]
        public void MissingPermissionsToReadEnvironmentVariable(string exception)
        {
            this.WriteEvent(7, exception);
        }
    }
}
