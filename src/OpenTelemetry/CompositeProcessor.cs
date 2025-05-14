// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Represents a chain of <see cref="BaseProcessor{T}"/>s.
/// </summary>
/// <typeparam name="T">The type of object to be processed.</typeparam>
public class CompositeProcessor<T> : BaseProcessor<T>
{
    internal readonly DoublyLinkedListNode Head;
    private DoublyLinkedListNode tail;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeProcessor{T}"/> class.
    /// </summary>
    /// <param name="processors">Processors to add to the composite processor chain.</param>
    public CompositeProcessor(IEnumerable<BaseProcessor<T>> processors)
    {
        Guard.ThrowIfNull(processors);

        using var iter = processors.GetEnumerator();
        if (!iter.MoveNext())
        {
            throw new ArgumentException($"'{iter}' is null or empty", nameof(processors));
        }

        this.Head = new DoublyLinkedListNode(iter.Current);
        this.tail = this.Head;

        while (iter.MoveNext())
        {
            this.AddProcessor(iter.Current);
        }
    }

    /// <summary>
    /// Adds a processor to the composite processor chain.
    /// </summary>
    /// <param name="processor"><see cref="BaseProcessor{T}"/>.</param>
    /// <returns>The current instance to support call chaining.</returns>
    public CompositeProcessor<T> AddProcessor(BaseProcessor<T> processor)
    {
        Guard.ThrowIfNull(processor);

        var node = new DoublyLinkedListNode(processor)
        {
            Previous = this.tail,
        };
        this.tail.Next = node;
        this.tail = node;

        return this;
    }

    /// <inheritdoc/>
    public override void OnEnd(T data)
    {
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.OnEnd(data);
        }
    }

    /// <inheritdoc/>
    public override void OnStart(T data)
    {
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.OnStart(data);
        }
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            cur.Value.SetParentProvider(parentProvider);
        }
    }

    internal IReadOnlyList<BaseProcessor<T>> ToReadOnlyList()
    {
        var list = new List<BaseProcessor<T>>();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            list.Add(cur.Value);
        }

        return list;
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var result = true;
        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            if (sw == null)
            {
                result = cur.Value.ForceFlush() && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // notify all the processors, even if we run overtime
                result = cur.Value.ForceFlush((int)Math.Max(timeout, 0)) && result;
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
                result = cur.Value.Shutdown() && result;
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                // notify all the processors, even if we run overtime
                result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
            }
        }

        return result;
    }

    /// <inheritdoc/>
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
                        cur.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    internal sealed class DoublyLinkedListNode
    {
        public readonly BaseProcessor<T> Value;

        public DoublyLinkedListNode(BaseProcessor<T> value)
        {
            this.Value = value;
        }

        public DoublyLinkedListNode? Previous { get; set; }

        public DoublyLinkedListNode? Next { get; set; }
    }
}
