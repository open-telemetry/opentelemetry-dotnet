// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// CompositeMetricReader that does not deal with adding metrics and recording measurements.
/// </summary>
internal sealed partial class CompositeMetricReader : MetricReader
{
    public readonly DoublyLinkedListNode Head;
    private DoublyLinkedListNode tail;
    private bool disposed;
    private int count;

    public CompositeMetricReader(IEnumerable<MetricReader> readers)
    {
        Guard.ThrowIfNull(readers);

        using var iter = readers.GetEnumerator();
        if (!iter.MoveNext())
        {
            throw new ArgumentException($"'{iter}' is null or empty", nameof(readers));
        }

        this.Head = new DoublyLinkedListNode(iter.Current);
        this.tail = this.Head;
        this.count++;

        while (iter.MoveNext())
        {
            this.AddReader(iter.Current);
        }
    }

    public CompositeMetricReader AddReader(MetricReader reader)
    {
        Guard.ThrowIfNull(reader);

        var node = new DoublyLinkedListNode(reader)
        {
            Previous = this.tail,
        };
        this.tail.Next = node;
        this.tail = node;
        this.count++;

        return this;
    }

    public Enumerator GetEnumerator() => new(this.Head);

    // CompositeMetricReader delegates the work to its underlying readers,
    // so CompositeMetricReader.ProcessMetrics should never be called.

    /// <inheritdoc/>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override bool OnCollect(int timeoutMilliseconds)
    {
        var result = true;
        var initialTimeoutMilliseconds = timeoutMilliseconds;
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        this.CollectObservableInstruments();

        for (var current = this.Head; current != null; current = current.Next)
        {
            var currentTimeoutMilliseconds = timeoutMilliseconds;

            if (timestamp is { } startedAt)
            {
                currentTimeoutMilliseconds = Stopwatch.Remaining(initialTimeoutMilliseconds, startedAt);
            }

            // Collect observable instruments once at the composite level, then
            // let child readers process the same snapshot.
            result = current.Value.CollectFromComposite(currentTimeoutMilliseconds) && result;
        }

        return result;
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;
        this.CollectObservableInstruments();
        var initialTimeoutMilliseconds = timeoutMilliseconds;
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        for (var current = this.Head; current != null; current = current.Next)
        {
            var currentTimeoutMilliseconds = timeoutMilliseconds;

            if (timestamp is { } startedAt)
            {
                currentTimeoutMilliseconds = Stopwatch.Remaining(initialTimeoutMilliseconds, startedAt);
            }

            // Notify all the readers, even if we run overtime
            result = current.Value.ShutdownFromComposite(currentTimeoutMilliseconds) && result;
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                for (var current = this.Head; current != null; current = current.Next)
                {
                    try
                    {
                        current.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Dispose), ex);
                    }
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    public struct Enumerator
    {
        private DoublyLinkedListNode? node;

        internal Enumerator(DoublyLinkedListNode node)
        {
            this.node = node;
            this.Current = null;
        }

        [AllowNull]
        public MetricReader Current { get; private set; }

        public bool MoveNext()
        {
            if (this.node != null)
            {
                this.Current = this.node.Value;
                this.node = this.node.Next;
                return true;
            }

            return false;
        }
    }

    internal sealed class DoublyLinkedListNode
    {
        public readonly MetricReader Value;

        public DoublyLinkedListNode(MetricReader value)
        {
            this.Value = value;
        }

        public DoublyLinkedListNode? Previous { get; set; }

        public DoublyLinkedListNode? Next { get; set; }
    }
}
