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

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        using var iter = processors.GetEnumerator();
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
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
        for (var current = this.Head; current != null; current = current.Next)
        {
            current.Value.OnEnd(data);
        }
    }

    /// <inheritdoc/>
    public override void OnStart(T data)
    {
        for (var current = this.Head; current != null; current = current.Next)
        {
            current.Value.OnStart(data);
        }
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        for (var current = this.Head; current != null; current = current.Next)
        {
            current.Value.SetParentProvider(parentProvider);
        }
    }

    internal IReadOnlyList<BaseProcessor<T>> ToReadOnlyList()
    {
        var list = new List<BaseProcessor<T>>();

        for (var current = this.Head; current != null; current = current.Next)
        {
            list.Add(current.Value);
        }

        return list;
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var result = true;
        var initialTimeoutMilliseconds = timeoutMilliseconds;
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        for (var current = this.Head; current != null; current = current.Next)
        {
            var currentTimeoutMilliseconds = timeoutMilliseconds;

            if (timestamp is { } startedAt)
            {
                currentTimeoutMilliseconds = Stopwatch.Remaining(initialTimeoutMilliseconds, startedAt);
            }

            // Notify all the processors, even if we run overtime
            result = current.Value.ForceFlush(currentTimeoutMilliseconds) && result;
        }

        return result;
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;
        var initialTimeoutMilliseconds = timeoutMilliseconds;
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        for (var current = this.Head; current != null; current = current.Next)
        {
            var currentTimeoutMilliseconds = timeoutMilliseconds;

            if (timestamp is { } startedAt)
            {
                currentTimeoutMilliseconds = Stopwatch.Remaining(initialTimeoutMilliseconds, startedAt);
            }

            // Notify all the processors, even if we run overtime
            result = current.Value.Shutdown(currentTimeoutMilliseconds) && result;
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
                for (var current = this.Head; current != null; current = current.Next)
                {
                    try
                    {
                        current.Value.Dispose();
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
