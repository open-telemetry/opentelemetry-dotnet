// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// The limit rules shared by the span, event and link attribute readers.
/// </summary>
internal struct AttributeReaderCore
{
    internal const string StatusCodeKey = "otel.status_code";
    internal const string StatusDescriptionKey = "otel.status_description";

    private readonly SpanLimitTotals? totals;
    private readonly Activity span;
    private readonly int countLimit;
    private readonly bool captureStatusTags;
    private Activity.Enumerator<KeyValuePair<string, object?>> enumerator;
    private int keptCount;
    private int droppedCount;
    private bool canMarkDropped;
    private bool completed;

    internal AttributeReaderCore(
        SpanLimitTotals? totals,
        Activity span,
        Activity.Enumerator<KeyValuePair<string, object?>> enumerator,
        int countLimit,
        bool captureStatusTags)
    {
        this.totals = totals;
        this.span = span;
        this.enumerator = enumerator;
        this.countLimit = countLimit;
        this.captureStatusTags = captureStatusTags;
    }

    internal object? StatusCodeTagValue { get; private set; }

    internal string? StatusDescriptionTagValue { get; private set; }

    internal readonly bool IsCompleted => this.completed;

    internal int DroppedCount
    {
        get
        {
            this.EnsureCompleted();
            return this.droppedCount;
        }
    }

    // Finds the status tags of a span without a reader, so nothing is counted. As in the reader,
    // the last value of each tag wins.
    internal static void FindStatusTags(Activity span, out object? statusCode, out string? statusDescription)
    {
        statusCode = null;
        statusDescription = null;

        foreach (ref readonly var tag in span.EnumerateTagObjects())
        {
            switch (tag.Key)
            {
                case StatusCodeKey:
                    statusCode = tag.Value;
                    break;
                case StatusDescriptionKey:
                    statusDescription = tag.Value as string;
                    break;
                default:
                    break;
            }
        }
    }

    internal bool TryRead(out KeyValuePair<string, object?> attribute)
    {
        this.canMarkDropped = false;

        if (!this.completed)
        {
            while (this.enumerator.MoveNext())
            {
                ref readonly var tag = ref this.enumerator.Current;

                if (this.captureStatusTags && this.TryCaptureStatusTag(in tag))
                {
                    continue;
                }

                if (this.keptCount < this.countLimit)
                {
                    this.keptCount++;
                    this.canMarkDropped = true;
                    attribute = tag;
                    return true;
                }

                this.droppedCount++;
            }

            this.Complete();
        }

        attribute = default;
        return false;
    }

    internal void MarkDropped()
    {
        if (!this.canMarkDropped)
        {
            throw new InvalidOperationException("MarkDropped can only be called once for each attribute returned by TryRead.");
        }

        this.canMarkDropped = false;
        this.keptCount--;
        this.droppedCount++;
    }

    internal void EnsureCompleted()
    {
        if (this.completed)
        {
            return;
        }

        this.canMarkDropped = false;

        // The loop stopped early. Attributes it did not read were not written, so they are
        // dropped, and status tags may still follow.
        while (this.enumerator.MoveNext())
        {
            ref readonly var tag = ref this.enumerator.Current;

            if (!this.captureStatusTags || !this.TryCaptureStatusTag(in tag))
            {
                this.droppedCount++;
            }
        }

        this.Complete();
    }

    private bool TryCaptureStatusTag(in KeyValuePair<string, object?> tag)
    {
        switch (tag.Key)
        {
            case StatusCodeKey:
                this.StatusCodeTagValue = tag.Value;
                return true;
            case StatusDescriptionKey:
                this.StatusDescriptionTagValue = tag.Value as string;
                return true;
            default:
                return false;
        }
    }

    private void Complete()
    {
        this.completed = true;
        this.totals?.RecordDroppedAttributes(this.span, this.droppedCount);
    }
}
