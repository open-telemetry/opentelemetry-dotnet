// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal sealed class LogRecordSharedPool : ILogRecordPool
{
    public const int DefaultMaxPoolSize = 2048;

    public static LogRecordSharedPool Current = new(DefaultMaxPoolSize);

    public readonly int Capacity;
    private readonly LogRecord?[] pool;
    private long rentIndex;
    private long returnIndex;

    private LogRecordSharedPool(int capacity)
    {
        this.Capacity = capacity;
        this.pool = new LogRecord?[capacity];
    }

    public int Count => (int)(Volatile.Read(ref this.returnIndex) - Volatile.Read(ref this.rentIndex));

    // Note: It might make sense to expose this (somehow) in the future.
    // Ideal config is shared pool capacity == max batch size.
    public static void Resize(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        Current = new(capacity);
    }

    public LogRecord Rent()
    {
        while (true)
        {
            var rentSnapshot = Volatile.Read(ref this.rentIndex);
            var returnSnapshot = Volatile.Read(ref this.returnIndex);

            if (rentSnapshot >= returnSnapshot)
            {
                break; // buffer is empty
            }

            if (Interlocked.CompareExchange(ref this.rentIndex, rentSnapshot + 1, rentSnapshot) == rentSnapshot)
            {
                var slotIndex = (int)(rentSnapshot % this.Capacity);
                var logRecord = Interlocked.Exchange(ref this.pool[slotIndex], null);
                if (logRecord == null && !this.TryRentCoreRare(slotIndex, out logRecord))
                {
                    // Note: The index is already consumed (incremented above).
                    // When TryRentCoreRare fails after spinning, continuing to
                    // retry with a new index can cause a race where two threads
                    // end up with the same LogRecord instance. Instead, we break
                    // out and create a new LogRecord. See:
                    // https://github.com/open-telemetry/opentelemetry-dotnet/issues/6233
                    break;
                }

                Debug.Assert(logRecord.Source == LogRecord.LogRecordSource.FromSharedPool, "logRecord.Source was not FromSharedPool");
                logRecord.ResetReferenceCount();
                return logRecord;
            }
        }

        var newLogRecord = new LogRecord()
        {
            Source = LogRecord.LogRecordSource.FromSharedPool,
        };
        newLogRecord.ResetReferenceCount();
        return newLogRecord;
    }

    public void Return(LogRecord logRecord)
    {
        Debug.Assert(logRecord.Source == LogRecord.LogRecordSource.FromSharedPool, "logRecord.Source was not FromSharedPool");

        if (logRecord.RemoveReference() != 0)
        {
            return;
        }

        LogRecordPoolHelper.Clear(logRecord);

        while (true)
        {
            var rentSnapshot = Volatile.Read(ref this.rentIndex);
            var returnSnapshot = Volatile.Read(ref this.returnIndex);

            if (returnSnapshot - rentSnapshot >= this.Capacity)
            {
                return; // buffer is full
            }

            if (Interlocked.CompareExchange(ref this.returnIndex, returnSnapshot + 1, returnSnapshot) == returnSnapshot)
            {
                // If many threads are hammering rent/return it is possible
                // for two threads to write to the same index. In that case
                // only one of the logRecords will make it back into the
                // pool. Anything lost in the race will collected by the GC
                // and the pool will issue new instances as needed. This
                // could be abated by an Interlocked.CompareExchange here
                // but for the general use case of an exporter returning
                // records one-by-one, better to keep this fast and not pay
                // for Interlocked.CompareExchange. The race is more
                // theoretical.
                this.pool[returnSnapshot % this.Capacity] = logRecord;
                return;
            }
        }
    }

    private bool TryRentCoreRare(int slotIndex, [NotNullWhen(true)] out LogRecord? logRecord)
    {
        SpinWait wait = default;
        while (true)
        {
            if (wait.NextSpinWillYield)
            {
                // Super rare case. If many threads are hammering
                // rent/return it is possible a read was issued an index and
                // then yielded while other threads caused the pointers to
                // wrap around. When the yielded thread wakes up its read
                // index could have been stolen by another thread. To
                // prevent deadlock, bail out of read after spinning. This
                // will cause a new record to be created. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/6233
                logRecord = null;
                return false;
            }

            wait.SpinOnce();

            logRecord = Interlocked.Exchange(ref this.pool[slotIndex], null);
            if (logRecord != null)
            {
                // Rare case where the write was still working when the read came in
                return true;
            }
        }
    }
}
