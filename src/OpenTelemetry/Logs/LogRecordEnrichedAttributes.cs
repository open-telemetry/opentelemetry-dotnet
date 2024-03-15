// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal sealed class LogRecordEnrichedAttributes : IReadOnlyList<KeyValuePair<string, object?>>
{
    private readonly List<KeyValuePair<string, object?>> enrichedAttributes = new();
    private IReadOnlyList<KeyValuePair<string, object?>> initialAttributes;

    public LogRecordEnrichedAttributes()
    {
        this.initialAttributes = Array.Empty<KeyValuePair<string, object?>>();
    }

    public int Count => this.initialAttributes.Count + this.enrichedAttributes.Count;

    public KeyValuePair<string, object?> this[int index]
    {
        get
        {
            Guard.ThrowIfNegative(index);

            var initialAttributes = this.initialAttributes;

            var count = initialAttributes.Count;
            if (index < count)
            {
                return initialAttributes[index];
            }

            return this.enrichedAttributes[index - count];
        }
    }

    public void Reset(IReadOnlyList<KeyValuePair<string, object?>>? initialAttributes)
    {
        this.initialAttributes = initialAttributes ?? Array.Empty<KeyValuePair<string, object?>>();
        this.enrichedAttributes.Clear();
    }

    public void Add(KeyValuePair<string, object?> attribute)
    {
        this.enrichedAttributes.Add(attribute);
    }

    public void Add(IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        Debug.Assert(attributes != null, "attributes was null");

        this.enrichedAttributes.AddRange(attributes);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (var i = 0; i < this.Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => this.GetEnumerator();
}
