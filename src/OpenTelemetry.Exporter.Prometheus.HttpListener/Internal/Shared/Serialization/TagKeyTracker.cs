// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Keeps track of the keys of the tags of the points of a metric, and the types of their values, as they are written, so that
/// it can be determined whether two keys are the same once they have been escaped, or whether a key has values of different
/// types (such as 1 and "1") or values that can be written as the same label value even though they are different
/// (such as two arrays with the same items), which can make the series of different points the same.
/// </summary>
/// <remarks>
/// Almost all of the points of a metric have the same keys with values of the same types, so each key is only compared
/// with the key that was in the same position for the previous point. This type is not thread-safe.
/// </remarks>
internal sealed class TagKeyTracker
{
    private readonly Dictionary<string, Type?> keys = new(StringComparer.Ordinal);
    private readonly string?[] previousKeys = new string?[16];
    private readonly Type?[] previousTypes = new Type?[16];

    /// <summary>
    /// Gets the distinct keys of the tags that were observed.
    /// </summary>
    public IReadOnlyCollection<string> Keys => this.keys.Keys;

    /// <summary>
    /// Gets a value indicating whether any key was observed with values of different types, or with values
    /// of a type for which different values can be written as the same label value.
    /// </summary>
    public bool HasAmbiguousValues { get; private set; }

    /// <summary>
    /// Forgets the keys, ready for the points of the next metric.
    /// </summary>
    public void Reset()
    {
        this.keys.Clear();
        this.HasAmbiguousValues = false;
        Array.Clear(this.previousKeys, 0, this.previousKeys.Length);
        Array.Clear(this.previousTypes, 0, this.previousTypes.Length);
    }

    /// <summary>
    /// Records the key of a tag of a point.
    /// </summary>
    /// <param name="index">The index of the tag in its point.</param>
    /// <param name="key">The key of the tag.</param>
    /// <param name="value">The value of the tag.</param>
    public void Observe(int index, string key, object? value)
    {
        var type = value?.GetType();

        if (index < this.previousKeys.Length)
        {
            if (ReferenceEquals(this.previousKeys[index], key) && this.previousTypes[index] == type)
            {
                return;
            }

            this.previousKeys[index] = key;
            this.previousTypes[index] = type;
        }

        // Only strings and primitives are always written as different label values when they are different, and
        // this is only reached the first time that a type is seen for a position, so the check is not often made.
        if (type is not null && type != typeof(string) && !type.IsPrimitive)
        {
            this.HasAmbiguousValues = true;
        }

        if (!this.keys.TryGetValue(key, out var existing))
        {
            this.keys[key] = type;
        }
        else if (existing != type)
        {
            this.HasAmbiguousValues = true;
        }
    }
}
