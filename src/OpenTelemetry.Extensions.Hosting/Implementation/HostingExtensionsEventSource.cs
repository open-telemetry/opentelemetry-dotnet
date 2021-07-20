// <copyright file="HostingExtensionsEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Extensions.Hosting.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Extensions-Hosting")]
    internal class HostingExtensionsEventSource : EventSource
    {
        public static HostingExtensionsEventSource Log = new HostingExtensionsEventSource();

        [NonEvent]
        public void FailedInitialize(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedInitialize(ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void FailedOpenTelemetrySDK(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedOpenTelemetrySDK(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "An exception occurred while initializing OpenTelemetry Tracing. OpenTelemetry tracing will remain disabled. Exception: '{0}'.", Level = EventLevel.Error)]
        public void FailedInitialize(string exception)
        {
            this.WriteEvent(1, exception);
        }

        [Event(2, Message = "An exception occurred while retrieving OpenTelemetry Tracer. OpenTelemetry tracing will remain disabled. Exception: '{0}'.", Level = EventLevel.Error)]
        public void FailedOpenTelemetrySDK(string exception)
        {
            this.WriteEvent(2, exception);
        }
    }
}
