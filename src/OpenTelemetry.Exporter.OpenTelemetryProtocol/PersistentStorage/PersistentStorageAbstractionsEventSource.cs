// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
