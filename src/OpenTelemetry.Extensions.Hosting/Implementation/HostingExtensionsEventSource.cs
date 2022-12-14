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

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Extensions.Hosting.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Extensions-Hosting")]
    internal sealed class HostingExtensionsEventSource : EventSource
    {
        public static HostingExtensionsEventSource Log = new();

        [Event(1, Message = "OpenTelemetry TracerProvider was not found in application services. Tracing will remain disabled.", Level = EventLevel.Warning)]
        public void TracerProviderNotRegistered()
        {
            this.WriteEvent(1);
        }

        [Event(2, Message = "OpenTelemetry MeterProvider was not found in application services. Metrics will remain disabled.", Level = EventLevel.Warning)]
        public void MeterProviderNotRegistered()
        {
            this.WriteEvent(2);
        }
    }
}
