// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;

namespace OpenTelemetry.Internal;

/// <summary>
/// Enumerates the entries of a tag value which represents a key/value list.
/// </summary>
/// <remarks>
/// Implementations are structs so that when they are used as a generic
/// argument constrained to this interface the enumeration is specialized for
/// the concrete shape and does not allocate an enumerator.
/// </remarks>
internal interface IKeyValueListEnumerator
{
    string CurrentKey { get; }

    object? CurrentValue { get; }

    bool MoveNext();

    void Dispose();
}

internal struct ArrayKeyValueListEnumerator(KeyValuePair<string, object?>[] array) : IKeyValueListEnumerator
{
    private readonly KeyValuePair<string, object?>[] array = array;
    private int index;

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        var items = this.array;
        if ((uint)this.index < (uint)items.Length)
        {
            var item = items[this.index++];
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public readonly void Dispose()
    {
    }
}

internal struct ListKeyValueListEnumerator(List<KeyValuePair<string, object?>> list) : IKeyValueListEnumerator
{
    private List<KeyValuePair<string, object?>>.Enumerator enumerator = list.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        if (this.enumerator.MoveNext())
        {
            var item = this.enumerator.Current;
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public void Dispose() => this.enumerator.Dispose();
}

internal struct ObjectDictionaryKeyValueListEnumerator(Dictionary<string, object?> dictionary) : IKeyValueListEnumerator
{
    private Dictionary<string, object?>.Enumerator enumerator = dictionary.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        if (this.enumerator.MoveNext())
        {
            var item = this.enumerator.Current;
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public void Dispose() => this.enumerator.Dispose();
}

internal struct ObjectEnumerableKeyValueListEnumerator(IEnumerable<KeyValuePair<string, object?>> enumerable) : IKeyValueListEnumerator
{
    private readonly IEnumerator<KeyValuePair<string, object?>> enumerator = enumerable.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        if (this.enumerator.MoveNext())
        {
            var item = this.enumerator.Current;
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public readonly void Dispose() => this.enumerator.Dispose();
}

internal struct StringDictionaryKeyValueListEnumerator(Dictionary<string, string?> dictionary) : IKeyValueListEnumerator
{
    private Dictionary<string, string?>.Enumerator enumerator = dictionary.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        if (this.enumerator.MoveNext())
        {
            var item = this.enumerator.Current;
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public void Dispose() => this.enumerator.Dispose();
}

internal struct StringEnumerableKeyValueListEnumerator(IEnumerable<KeyValuePair<string, string?>> enumerable) : IKeyValueListEnumerator
{
    private readonly IEnumerator<KeyValuePair<string, string?>> enumerator = enumerable.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        if (this.enumerator.MoveNext())
        {
            var item = this.enumerator.Current;
            this.CurrentKey = item.Key;
            this.CurrentValue = item.Value;
            return true;
        }

        return false;
    }

    public readonly void Dispose() => this.enumerator.Dispose();
}

internal struct DictionaryKeyValueListEnumerator(IDictionary dictionary) : IKeyValueListEnumerator
{
    private readonly IDictionaryEnumerator enumerator = dictionary.GetEnumerator();

    public string CurrentKey { get; private set; } = string.Empty;

    public object? CurrentValue { get; private set; } = null;

    public bool MoveNext()
    {
        // Entries with a key which cannot be represented as a string are
        // skipped rather than ending the enumeration.
        while (this.enumerator.MoveNext())
        {
            var entryKey = this.enumerator.Key as string
                ?? Convert.ToString(this.enumerator.Key, CultureInfo.InvariantCulture);

            if (entryKey != null)
            {
                this.CurrentKey = entryKey;
                this.CurrentValue = this.enumerator.Value;
                return true;
            }
        }

        return false;
    }

    public readonly void Dispose() => (this.enumerator as IDisposable)?.Dispose();
}
