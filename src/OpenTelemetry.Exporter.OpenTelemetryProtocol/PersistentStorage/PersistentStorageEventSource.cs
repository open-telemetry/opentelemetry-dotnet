// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.PersistentStorage.FileSystem;

[EventSource(Name = EventSourceName)]
internal sealed class PersistentStorageEventSource : EventSource
{
    public static PersistentStorageEventSource Log = new PersistentStorageEventSource();
#if BUILDING_INTERNAL_PERSISTENT_STORAGE
    private const string EventSourceName = "OpenTelemetry-PersistentStorage-FileSystem-Otlp";
#else
    private const string EventSourceName = "OpenTelemetry-PersistentStorage-FileSystem";
#endif

    [NonEvent]
    public void CouldNotReadFileBlob(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.CouldNotReadFileBlob(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotWriteFileBlob(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.CouldNotWriteFileBlob(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotLeaseFileBlob(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.CouldNotLeaseFileBlob(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotDeleteFileBlob(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.CouldNotDeleteFileBlob(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotCreateFileBlob(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Informational, EventKeywords.All))
        {
            this.CouldNotCreateFileBlob(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotRemoveExpiredBlob(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.CouldNotRemoveExpiredBlob(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotRemoveTimedOutTmpFile(string filePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.CouldNotRemoveTimedOutTmpFile(filePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CouldNotRemoveExpiredLease(string srcFilePath, string destFilePath, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.CouldNotRemoveExpiredLease(srcFilePath, destFilePath, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void PersistentStorageException(string className, string message, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.PersistentStorageException(className, message, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Could not read blob from file '{0}'", Level = EventLevel.Informational)]
    public void CouldNotReadFileBlob(string filePath, string ex)
    {
        this.WriteEvent(1, filePath, ex);
    }

    [Event(2, Message = "Could not write blob to file '{0}'", Level = EventLevel.Informational)]
    public void CouldNotWriteFileBlob(string filePath, string ex)
    {
        this.WriteEvent(2, filePath, ex);
    }

    [Event(3, Message = "Could not acquire a lease on file '{0}'", Level = EventLevel.Informational)]
    public void CouldNotLeaseFileBlob(string filePath, string ex)
    {
        this.WriteEvent(3, filePath, ex);
    }

    [Event(4, Message = "Could not delete file '{0}'", Level = EventLevel.Informational)]
    public void CouldNotDeleteFileBlob(string filePath, string ex)
    {
        this.WriteEvent(4, filePath, ex);
    }

    [Event(5, Message = "Could not create file blob", Level = EventLevel.Informational)]
    public void CouldNotCreateFileBlob(string ex)
    {
        this.WriteEvent(5, ex);
    }

    [Event(6, Message = "Could not remove expired blob '{0}'", Level = EventLevel.Warning)]
    public void CouldNotRemoveExpiredBlob(string filePath, string ex)
    {
        this.WriteEvent(6, filePath, ex);
    }

    [Event(7, Message = "Could not remove timed out file '{0}'", Level = EventLevel.Warning)]
    public void CouldNotRemoveTimedOutTmpFile(string filePath, string ex)
    {
        this.WriteEvent(7, filePath, ex);
    }

    [Event(8, Message = "Could not rename '{0}' to '{1}'", Level = EventLevel.Warning)]
    public void CouldNotRemoveExpiredLease(string srcFilePath, string destFilePath, string ex)
    {
        this.WriteEvent(8, srcFilePath, destFilePath, ex);
    }

    [Event(9, Message = "{0}: Error Message: {1}. Exception: {2}", Level = EventLevel.Error)]
    public void PersistentStorageException(string className, string message, string ex)
    {
        this.WriteEvent(9, className, message, ex);
    }

    [Event(10, Message = "{0}: Warning Message: {1}", Level = EventLevel.Warning)]
    public void PersistentStorageWarning(string className, string message)
    {
        this.WriteEvent(10, className, message);
    }

    [Event(11, Message = "{0}: Message: {1}", Level = EventLevel.Informational)]
    public void PersistentStorageInformation(string className, string message)
    {
        this.WriteEvent(11, className, message);
    }
}
