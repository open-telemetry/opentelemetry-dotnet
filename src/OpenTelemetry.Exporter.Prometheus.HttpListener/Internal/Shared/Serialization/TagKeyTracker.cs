// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Keeps track of the keys of the tags of the points of a metric as they are written, so that it can be determined
/// whether two of the keys are the same once they have been escaped, which can make the series of different points the same.
/// </summary>
/// <remarks>
/// Almost all of the points of a metric have the same keys, which are the same strings, so each key is only compared
/// with the key that was in the same position for the previous point. This type is not thread-safe.
/// </remarks>
internal sealed class TagKeyTracker
{
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private readonly string?[] previousKeys = new string?[16];

    /// <summary>
    /// Gets the distinct keys of the tags that were observed.
    /// </summary>
    public IReadOnlyCollection<string> Keys => this.keys;

    /// <summary>
    /// Forgets the keys, ready for the points of the next metric.
    /// </summary>
    public void Reset()
    {
        this.keys.Clear();
        Array.Clear(this.previousKeys, 0, this.previousKeys.Length);
    }

    /// <summary>
    /// Records the key of a tag of a point.
    /// </summary>
    /// <param name="index">The index of the tag in its point.</param>
    /// <param name="key">The key of the tag.</param>
    public void Observe(int index, string key)
    {
        if (index < this.previousKeys.Length)
        {
            if (ReferenceEquals(this.previousKeys[index], key))
            {
                return;
            }

            this.previousKeys[index] = key;
        }

        _ = this.keys.Add(key);
    }
}
