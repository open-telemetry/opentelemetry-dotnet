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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    [EventSource(Name = "OpenTelemetry-Exporter-OpenTelemetryProtocol")]
    internal class OpenTelemetryProtocolExporterEventSource : EventSource
    {
        public static readonly OpenTelemetryProtocolExporterEventSource Log = new();

        [NonEvent]
        public void FailedToReachCollector(Uri collectorUri, Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                var rawCollectorUri = collectorUri.ToString();
                this.FailedToReachCollector(rawCollectorUri, ex.ToInvariantString());
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

        [Event(2, Message = "Exporter failed send data to collector to {0} endpoint. Data will not be sent. Exception: {1}", Level = EventLevel.Error)]
        public void FailedToReachCollector(string rawCollectorUri, string ex)
        {
            this.WriteEvent(2, rawCollectorUri, ex);
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

        [Event(8, Message = "Unsupported value for protocol '{0}' is configured, default protocol 'grpc' will be used.", Level = EventLevel.Warning)]
        public void UnsupportedProtocol(string protocol)
        {
            this.WriteEvent(8, protocol);
        }

        [Event(9, Message = "Could not translate LogRecord due to Exception: '{0}'. Log will not be exported.", Level = EventLevel.Warning)]
        public void CouldNotTranslateLogRecord(string exceptionMessage)
        {
            this.WriteEvent(9, exceptionMessage);
        }
    }
}
