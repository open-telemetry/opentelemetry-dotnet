// <copyright file="PersistentStorageAbstractionsEventSource.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.PersistentStorage.Abstractions;

[EventSource(Name = EventSourceName)]
internal sealed class PersistentStorageAbstractionsEventSource : EventSource
{
    public static PersistentStorageAbstractionsEventSource Log = new PersistentStorageAbstractionsEventSource();
#if BUILDING_INTERNAL_PERSISTENT_STORAGE
    private const string EventSourceName = "OpenTelemetry-PersistentStorage-Abstractions-Otlp";
#else
    private const string EventSourceName = "OpenTelemetry-PersistentStorage-Abstractions";
#endif

    [NonEvent]
    public void PersistentStorageAbstractionsException(string className, string message, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.PersistentStorageAbstractionsException(className, message, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "{0}: {1}: {2}", Level = EventLevel.Error)]
    public void PersistentStorageAbstractionsException(string className, string message, string ex)
    {
        this.WriteEvent(1, className, message, ex);
    }
}
