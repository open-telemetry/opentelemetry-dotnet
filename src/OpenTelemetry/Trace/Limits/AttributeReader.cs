// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads the attributes of a span event or link with the corresponding attribute limits applied.
/// </summary>
/// <remarks>
/// Obtained from <see cref="SpanEventReader.CreateAttributeReader"/> or
/// <see cref="SpanLinkReader.CreateAttributeReader"/>. Status tags are ordinary attributes on events and links.
/// </remarks>
internal ref struct AttributeReader
{
    private AttributeReaderCore core;

    internal AttributeReader(AttributeReaderCore core, int? valueLengthLimit)
    {
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
    /// are counted first.
    /// </summary>
    public int DroppedCount => this.core.DroppedCount;

    /// <summary>
    /// Reads the next attribute within the limit.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns><see langword="true"/> if an attribute was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(out KeyValuePair<string, object?> attribute)
        => this.core.TryRead(out attribute);

    /// <summary>
    /// Marks the attribute just read as dropped because the exporter could not write it. It is
    /// recorded as dropped immediately and does not use up the limit.
    /// </summary>
    public void MarkDropped()
        => this.core.MarkDropped();
}
