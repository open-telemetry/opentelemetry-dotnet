// <copyright file="LogRecordAttributeList.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Stores attributes to be added to a log message.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
public
#else
/// <summary>
/// Stores attributes to be added to a log message.
/// </summary>
internal
#endif
    struct LogRecordAttributeList : IReadOnlyList<KeyValuePair<string, object?>>
{
    internal const int OverflowMaxCount = 8;
    internal const int OverflowAdditionalCapacity = 16;
    internal List<KeyValuePair<string, object?>>? OverflowAttributes;
    private static readonly IReadOnlyList<KeyValuePair<string, object?>> Empty = Array.Empty<KeyValuePair<string, object?>>();
    private KeyValuePair<string, object?> attribute1;
    private KeyValuePair<string, object?> attribute2;
    private KeyValuePair<string, object?> attribute3;
    private KeyValuePair<string, object?> attribute4;
    private KeyValuePair<string, object?> attribute5;
    private KeyValuePair<string, object?> attribute6;
    private KeyValuePair<string, object?> attribute7;
    private KeyValuePair<string, object?> attribute8;
    private int count;

    /// <inheritdoc/>
    public readonly int Count => this.count;

    /// <inheritdoc/>
    public KeyValuePair<string, object?> this[int index]
    {
        readonly get
        {
            if (this.OverflowAttributes is not null)
            {
                Debug.Assert(index < this.OverflowAttributes.Count, "Invalid index accessed.");
                return this.OverflowAttributes[index];
            }

            if ((uint)index >= (uint)this.count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index switch
            {
                0 => this.attribute1,
                1 => this.attribute2,
                2 => this.attribute3,
                3 => this.attribute4,
                4 => this.attribute5,
                5 => this.attribute6,
                6 => this.attribute7,
                7 => this.attribute8,
                _ => default, // we shouldn't come here anyway.
            };
        }

        set
        {
            if (this.OverflowAttributes is not null)
            {
                Debug.Assert(index < this.OverflowAttributes.Count, "Invalid index accessed.");
                this.OverflowAttributes[index] = value;
                return;
            }

            if ((uint)index >= (uint)this.count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0: this.attribute1 = value; break;
                case 1: this.attribute2 = value; break;
                case 2: this.attribute3 = value; break;
                case 3: this.attribute4 = value; break;
                case 4: this.attribute5 = value; break;
                case 5: this.attribute6 = value; break;
                case 6: this.attribute7 = value; break;
                case 7: this.attribute8 = value; break;
                default:
                    Debug.Assert(false, "Unreachable code executed.");
                    break;
            }
        }
    }

    /// <summary>
    /// Add an attribute.
    /// </summary>
    /// <param name="key">Attribute name.</param>
    /// <returns>Attribute value.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object? this[string key]
    {
        // Note: This only exists to enable collection initializer syntax
        // like { ["key"] = value }.
        set => this.Add(new KeyValuePair<string, object?>(key, value));
    }

    /// <summary>
    /// Create a <see cref="LogRecordAttributeList"/> collection from an enumerable.
    /// </summary>
    /// <param name="attributes">Source attributes.</param>
    /// <returns><see cref="LogRecordAttributeList"/>.</returns>
    public static LogRecordAttributeList CreateFromEnumerable(IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        Guard.ThrowIfNull(attributes);

        LogRecordAttributeList logRecordAttributes = default;
        logRecordAttributes.OverflowAttributes = new(attributes);
        logRecordAttributes.count = logRecordAttributes.OverflowAttributes.Count;
        return logRecordAttributes;
    }

    /// <summary>
    /// Add an attribute.
    /// </summary>
    /// <param name="key">Attribute name.</param>
    /// <param name="value">Attribute value.</param>
    public void Add(string key, object? value)
        => this.Add(new KeyValuePair<string, object?>(key, value));

    /// <summary>
    /// Add an attribute.
    /// </summary>
    /// <param name="attribute">Attribute.</param>
    public void Add(KeyValuePair<string, object?> attribute)
    {
        var count = this.count++;

        if (count <= OverflowMaxCount)
        {
            switch (count)
            {
                case 0: this.attribute1 = attribute; return;
                case 1: this.attribute2 = attribute; return;
                case 2: this.attribute3 = attribute; return;
                case 3: this.attribute4 = attribute; return;
                case 4: this.attribute5 = attribute; return;
                case 5: this.attribute6 = attribute; return;
                case 6: this.attribute7 = attribute; return;
                case 7: this.attribute8 = attribute; return;
                case 8:
                    this.MoveAttributesToTheOverflowList();
                    break;
            }
        }

        Debug.Assert(this.OverflowAttributes is not null, "Overflow attributes creation failure.");
        this.OverflowAttributes!.Add(attribute);
    }

    /// <summary>
    /// Removes all elements from the <see cref="LogRecordAttributeList"/>.
    /// </summary>
    public void Clear()
    {
        this.count = 0;
        this.OverflowAttributes?.Clear();
    }

    /// <summary>
    /// Adds attributes representing an <see cref="Exception"/> to the list.
    /// </summary>
    /// <param name="exception"><see cref="Exception"/>.</param>
    public void RecordException(Exception exception)
    {
        Guard.ThrowIfNull(exception);

        this.Add(SemanticConventions.AttributeExceptionType, exception.GetType().Name);
        this.Add(SemanticConventions.AttributeExceptionMessage, exception.Message);
        this.Add(SemanticConventions.AttributeExceptionStacktrace, exception.ToInvariantString());
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="LogRecordAttributeList"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public readonly Enumerator GetEnumerator()
        => new(in this);

    /// <inheritdoc/>
    readonly IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() => this.GetEnumerator();

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    internal readonly IReadOnlyList<KeyValuePair<string, object?>> Export(ref List<KeyValuePair<string, object?>>? attributeStorage)
    {
        int count = this.count;
        if (count <= 0)
        {
            return Empty;
        }

        var overflowAttributes = this.OverflowAttributes;
        if (overflowAttributes != null)
        {
            // An allocation has already occurred, just use the list.
            return overflowAttributes;
        }

        Debug.Assert(count <= 8, "Invalid size detected.");

        attributeStorage ??= new List<KeyValuePair<string, object?>>(OverflowAdditionalCapacity);

        // TODO: Perf test this, adjust as needed.
        if (count > 0)
        {
            attributeStorage.Add(this.attribute1);
            if (count == 1)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute2);
            if (count == 2)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute3);
            if (count == 3)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute4);
            if (count == 4)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute5);
            if (count == 5)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute6);
            if (count == 6)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute7);
            if (count == 7)
            {
                return attributeStorage;
            }

            attributeStorage.Add(this.attribute8);
        }

        return attributeStorage;
    }

    private void MoveAttributesToTheOverflowList()
    {
        Debug.Assert(this.count - 1 == OverflowMaxCount, "count did not match OverflowMaxCount");

        var attributes = this.OverflowAttributes ??= new(OverflowAdditionalCapacity);

        attributes.Add(this.attribute1);
        attributes.Add(this.attribute2);
        attributes.Add(this.attribute3);
        attributes.Add(this.attribute4);
        attributes.Add(this.attribute5);
        attributes.Add(this.attribute6);
        attributes.Add(this.attribute7);
        attributes.Add(this.attribute8);
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="LogRecordAttributeList"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>, IEnumerator
    {
        private LogRecordAttributeList attributes;
        private int index;

        internal Enumerator(in LogRecordAttributeList attributes)
        {
            this.index = -1;
            this.attributes = attributes;
        }

        /// <inheritdoc/>
        public readonly KeyValuePair<string, object?> Current
            => this.attributes[this.index];

        /// <inheritdoc/>
        readonly object IEnumerator.Current => this.Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            this.index++;
            return this.index < this.attributes.Count;
        }

        /// <inheritdoc/>
        public readonly void Dispose()
        {
        }

        /// <inheritdoc/>
        readonly void IEnumerator.Reset()
            => throw new NotSupportedException();
    }
}
