// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads the attributes of a span with the span attribute limits applied, and resolves the span
/// status.
/// </summary>
/// <remarks>
/// The <c>otel.status_code</c> and <c>otel.status_description</c> tags are not returned and do
/// not count towards the limit. Attributes beyond the count limit are counted as dropped. The
/// value length limit is exposed for the writer to apply.
/// </remarks>
internal ref struct SpanAttributeReader
{
    private readonly Activity activity;
    private AttributeReaderCore core;

    internal SpanAttributeReader(Activity activity, AttributeReaderCore core, int? valueLengthLimit)
    {
        this.activity = activity;
        this.core = core;
        this.ValueLengthLimit = valueLengthLimit;
    }

    /// <summary>
    /// Gets the maximum length the writer should truncate values to, or <see langword="null"/>
    /// for no limit.
    /// </summary>
    public readonly int? ValueLengthLimit { get; }

    /// <summary>
    /// Gets the number of attributes dropped. If reading stopped early, the remaining attributes
    /// are read and counted first.
    /// </summary>
    public int DroppedCount => this.core.DroppedCount;

    /// <summary>
    /// Gets the resolved span status: <see cref="Activity.Status"/> unless it is
    /// <see cref="ActivityStatusCode.Unset"/>, otherwise the <c>otel.status_code</c> tag.
    /// </summary>
    /// <remarks>
    /// Reading the status does not finish the reader, so it can be read at any point.
    /// </remarks>
    public readonly ActivityStatusCode Status
    {
        get
        {
            if (this.activity.Status != ActivityStatusCode.Unset)
            {
                return this.activity.Status;
            }

            this.GetStatusTags(out var statusCode, out _);
            return ParseStatusCodeTag(statusCode);
        }
    }

    /// <summary>
    /// Gets the status description when the resolved status is
    /// <see cref="ActivityStatusCode.Error"/>; otherwise <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Reading the description does not finish the reader, so it can be read at any point.
    /// </remarks>
    public readonly string? StatusDescription
    {
        get
        {
            if (this.activity.Status != ActivityStatusCode.Unset)
            {
                return this.activity.Status == ActivityStatusCode.Error ? this.activity.StatusDescription : null;
            }

            this.GetStatusTags(out var statusCode, out var statusDescription);
            return ParseStatusCodeTag(statusCode) == ActivityStatusCode.Error
                ? statusDescription
                : null;
        }
    }

    /// <summary>
    /// Reads the next attribute within the limit.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns><see langword="true"/> if an attribute was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(out KeyValuePair<string, object?> attribute)
        => this.core.TryRead(out attribute);

    /// <summary>
    /// Marks the attribute just read as dropped because the exporter could not write it. It is
    /// counted as dropped and does not use up the limit.
    /// </summary>
    public void MarkDropped()
        => this.core.MarkDropped();

    private static ActivityStatusCode ParseStatusCodeTag(object? value)
    {
        // Error is checked first because it is the code most likely to be set. Unset and
        // unrecognised values both resolve to Unset.
        var code = value as string;
        return string.Equals("ERROR", code, StringComparison.OrdinalIgnoreCase)
            ? ActivityStatusCode.Error
            : string.Equals("OK", code, StringComparison.OrdinalIgnoreCase)
                ? ActivityStatusCode.Ok
                : ActivityStatusCode.Unset;
    }

    private readonly void GetStatusTags(out object? statusCode, out string? statusDescription)
    {
        if (this.core.IsCompleted)
        {
            statusCode = this.core.StatusCodeTagValue;
            statusDescription = this.core.StatusDescriptionTagValue;
            return;
        }

        // The status tags can follow attributes not read yet. They are looked up separately, so
        // the reader keeps its position and nothing is counted as dropped.
        AttributeReaderCore.FindStatusTags(this.activity, out statusCode, out statusDescription);
    }
}
