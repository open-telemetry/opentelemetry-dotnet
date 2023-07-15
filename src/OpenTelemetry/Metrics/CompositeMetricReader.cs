// <copyright file="CompositeMetricReader.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
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
            throw new ArgumentException($"'{iter}' is null or empty", nameof(iter));
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

    /// <inheritdoc/>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        // CompositeMetricReader delegates the work to its underlying readers,
        // so CompositeMetricReader.ProcessMetrics should never be called.
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override bool OnCollect(int timeoutMilliseconds = Timeout.Infinite)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.Collect(Timeout.Infinite) && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // notify all the readers, even if we run overtime
                result = cur.Value.Collect((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.Shutdown(Timeout.Infinite) && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // notify all the readers, even if we run overtime
                result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                for (var cur = this.Head; cur != null; cur = cur.Next)
                {
                    try
                    {
                        cur.Value?.Dispose();
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
        private DoublyLinkedListNode node;

        internal Enumerator(DoublyLinkedListNode node)
        {
            this.node = node;
            this.Current = null;
        }

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

        public DoublyLinkedListNode Previous { get; set; }

        public DoublyLinkedListNode Next { get; set; }
    }
}
