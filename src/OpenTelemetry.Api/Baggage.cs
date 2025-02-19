// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Context;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Baggage implementation.
/// </summary>
/// <remarks>
/// Spec reference: <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/baggage/api.md">Baggage API</a>.
/// </remarks>
public readonly struct Baggage : IEquatable<Baggage>
{
    private static readonly RuntimeContextSlot<BaggageHolder> RuntimeContextSlot =
        RuntimeContext.RegisterSlot<BaggageHolder>("otel.baggage");

    private static readonly Dictionary<string, string> EmptyBaggage = [];
    private static readonly Dictionary<string, BaggageEntryMetadata> EmptyMetadata = [];

    private readonly Dictionary<string, string> baggage;
    private readonly Dictionary<string, BaggageEntryMetadata>? metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="Baggage"/> struct.
    /// </summary>
    /// <param name="baggage">Baggage key/value pairs.</param>
    /// <param name="metadata">Baggage key/metadata pairs.</param>
    internal Baggage(Dictionary<string, string> baggage, Dictionary<string, BaggageEntryMetadata>? metadata = null)
    {
        this.baggage = baggage;
        this.metadata = metadata;
    }

    /// <summary>
    /// Gets or sets the current <see cref="Baggage"/>.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="Current"/> returns a forked version of the current
    /// Baggage. Changes to the forked version will not automatically be
    /// reflected back on <see cref="Current"/>. To update <see
    /// cref="Current"/> either use one of the static methods that target
    /// <see cref="Current"/> as the default source or set <see
    /// cref="Current"/> to a new instance of <see cref="Baggage"/>.
    /// Examples:
    /// <code>
    /// Baggage.SetBaggage("newKey1", "newValue1"); // Updates Baggage.Current with 'newKey1'
    /// Baggage.SetBaggage("newKey2", "newValue2"); // Updates Baggage.Current with 'newKey2'
    /// </code>
    /// Or:
    /// <code>
    /// var baggageCopy = Baggage.Current;
    /// Baggage.SetBaggage("newKey1", "newValue1"); // Updates Baggage.Current with 'newKey1'
    /// var newBaggage = baggageCopy
    ///     .SetBaggage("newKey2", "newValue2");
    ///     .SetBaggage("newKey3", "newValue3");
    /// // Sets Baggage.Current to 'newBaggage' which will override any
    /// // changes made to Baggage.Current after the copy was made. For example
    /// // the 'newKey1' change is lost.
    /// Baggage.Current = newBaggage;
    /// </code>
    /// </remarks>
    public static Baggage Current
    {
        get => RuntimeContextSlot.Get()?.Baggage ?? default;
        set => EnsureBaggageHolder().Baggage = value;
    }

    /// <summary>
    /// Gets the number of key/value pairs in the baggage.
    /// </summary>
    public int Count => this.baggage?.Count ?? 0;

    /// <summary>
    /// Compare two entries of <see cref="Baggage"/> for equality.
    /// </summary>
    /// <param name="left">First Entry to compare.</param>
    /// <param name="right">Second Entry to compare.</param>
    public static bool operator ==(Baggage left, Baggage right) => left.Equals(right);

    /// <summary>
    /// Compare two entries of <see cref="Baggage"/> for not equality.
    /// </summary>
    /// <param name="left">First Entry to compare.</param>
    /// <param name="right">Second Entry to compare.</param>
    public static bool operator !=(Baggage left, Baggage right) => !(left == right);

    /// <summary>
    /// Create a <see cref="Baggage"/> instance from dictionary of baggage key/value pairs.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.CreateWithMetadata(System.Collections.Generic.Dictionary{string,BaggageEntry})"/>
    /// method instead.
    /// </remarks>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns><see cref="Baggage"/>.</returns>
    // [Obsolete("Call CreateWithMetadata instead.")]
    public static Baggage Create(Dictionary<string, string>? baggageItems = null)
    {
        if (baggageItems == null)
        {
            return default;
        }

        Dictionary<string, string> baggageCopy =
            new Dictionary<string, string>(baggageItems.Count, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> baggageItem in baggageItems)
        {
            if (string.IsNullOrEmpty(baggageItem.Value))
            {
                baggageCopy.Remove(baggageItem.Key);
                continue;
            }

            baggageCopy[baggageItem.Key] = baggageItem.Value;
        }

        return new Baggage(baggageCopy);
    }

    /// <summary>
    /// Creates a <see cref="Baggage"/> instance from dictionary of baggage keys and values with optional metadata.
    /// </summary>
    /// <param name="baggageItems">Dictionary of baggage keys and values with optional metadata.</param>
    /// <returns><see cref="Baggage"/>.</returns>
    public static Baggage CreateWithMetadata(Dictionary<string, BaggageEntry> baggageItems)
    {
        Dictionary<string, string> baggageCopy =
            new Dictionary<string, string>(baggageItems.Count, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, BaggageEntryMetadata>? metadataCopy = null;
        foreach (KeyValuePair<string, BaggageEntry> baggageItem in baggageItems)
        {
            baggageCopy[baggageItem.Key] = baggageItem.Value.Value;
            var baggageEntryMetadata = baggageItem.Value.Metadata;
            if (baggageEntryMetadata != null)
            {
                metadataCopy ??= new Dictionary<string, BaggageEntryMetadata>(StringComparer.OrdinalIgnoreCase);
                metadataCopy[baggageItem.Key] = baggageEntryMetadata.Value;
            }
        }

        return new Baggage(baggageCopy, metadataCopy);
    }

    /// <summary>
    /// Returns the name/value pairs in the <see cref="Baggage"/>.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.GetEnumeratorWithMetadata(Baggage)"/>
    /// method instead.
    /// </remarks>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>Baggage key/value pairs.</returns>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    // [Obsolete("Call GetEnumeratorWithMetadata instead to iterate over the baggage.")]
    public static IReadOnlyDictionary<string, string> GetBaggage(Baggage baggage = default)
        => baggage == default ? Current.GetBaggage() : baggage.GetBaggage();

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.GetEnumeratorWithMetadata(Baggage)"/>
    /// method instead.
    /// </remarks>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
    // [Obsolete("Call GetEnumeratorWithMetadata instead to iterate over the baggage.")]
    public static Dictionary<string, string>.Enumerator GetEnumerator(Baggage baggage = default)
        => baggage == default ? Current.GetEnumerator() : baggage.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates over the <see cref="Baggage"/>, returning keys, values and optional metadata.
    /// </summary>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns><see cref="Baggage.Enumerator"/>.</returns>
    public static Enumerator GetEnumeratorWithMetadata(Baggage baggage)
        => baggage == default ? Current.GetEnumeratorWithMetadata() : baggage.GetEnumeratorWithMetadata();

    /// <summary>
    /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public static string? GetBaggage(string name, Baggage baggage = default)
        => baggage == default ? Current.GetBaggage(name) : baggage.GetBaggage(name);

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="value">Baggage item value.</param>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public static Baggage SetBaggage(string name, string? value, Baggage baggage = default) => SetBaggage(name, value, null, baggage);

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value with optional metadata.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="value">Baggage item value.</param>
    /// <param name="metadata">Baggage item metadata.</param>
    /// <param name="baggage"><see cref="Baggage"/> instance.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    public static Baggage SetBaggage(string name, string? value, string? metadata, Baggage baggage)
    {
        var baggageHolder = EnsureBaggageHolder();
        lock (baggageHolder)
        {
            return baggageHolder.Baggage = baggage == default
                ? baggageHolder.Baggage.SetBaggage(name, value, metadata)
                : baggage.SetBaggage(name, value, metadata);
        }
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete and will be removed in a future release.
    /// </remarks>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>New <see cref="Baggage"/> containing the new key/value pairs.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    // [Obsolete("This method is obsolete and will be removed in a future release.")]
    public static Baggage SetBaggage(IEnumerable<KeyValuePair<string, string?>> baggageItems, Baggage baggage = default)
    {
        var baggageHolder = EnsureBaggageHolder();
        lock (baggageHolder)
        {
            return baggageHolder.Baggage = baggage == default
                ? baggageHolder.Baggage.SetBaggage(baggageItems)
                : baggage.SetBaggage(baggageItems);
        }
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> with the key/value pair removed.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>New <see cref="Baggage"/> with the key/value pair removed.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    public static Baggage RemoveBaggage(string name, Baggage baggage = default)
    {
        var baggageHolder = EnsureBaggageHolder();
        lock (baggageHolder)
        {
            return baggageHolder.Baggage = baggage == default
                ? baggageHolder.Baggage.RemoveBaggage(name)
                : baggage.RemoveBaggage(name);
        }
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> with all the key/value pairs removed.
    /// </summary>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>New <see cref="Baggage"/> with all the key/value pairs removed.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    public static Baggage ClearBaggage(Baggage baggage = default)
    {
        var baggageHolder = EnsureBaggageHolder();
        lock (baggageHolder)
        {
            return baggageHolder.Baggage = baggage == default
                ? baggageHolder.Baggage.ClearBaggage()
                : baggage.ClearBaggage();
        }
    }

    /// <summary>
    /// Returns the name/value pairs in the <see cref="Baggage"/>.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete.
    /// Call the <see cref="Baggage.GetEnumeratorWithMetadata()"/> instead to iterate over the baggage.
    /// </remarks>
    /// <returns>Baggage key/value pairs.</returns>
    // [Obsolete("Call GetEnumeratorWithMetadata instead to iterate over the baggage.")]
    public IReadOnlyDictionary<string, string> GetBaggage()
        =>
            this.baggage ?? EmptyBaggage;

    /// <summary>
    /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.GetBaggageWithMetadata(string)"/>
    /// method instead.
    /// </remarks>>
    /// <param name="name">Baggage item name.</param>
    /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
    // [Obsolete("Call GetBaggageWithMetadata instead.")]
    public string? GetBaggage(string name) => this.GetBaggageWithMetadata(name)?.Value;

    /// <summary>
    /// Returns the value associated with the given name, with optional metadata, or <see langword="null"/> if the given name is not present.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <returns>Baggage item value with metadata, or <see langword="null"/> if nothing was found.</returns>
    public BaggageEntry? GetBaggageWithMetadata(string name)
    {
        Guard.ThrowIfNullOrEmpty(name);

        if (this.baggage == null || !this.baggage.TryGetValue(name, out var keyValue))
        {
            return null;
        }

        return this.metadata != null && this.metadata.TryGetValue(name, out var keyMetadata)
            ? new BaggageEntry(keyValue, keyMetadata)
            : new BaggageEntry(keyValue);
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="value">Baggage item value.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
    public Baggage SetBaggage(string name, string? value) => this.SetBaggage(name, value, null);

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="value">Baggage item value.</param>
    /// <param name="metadata">Baggage item metadata.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
    public Baggage SetBaggage(string name, string? value, string? metadata)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this.RemoveBaggage(name);
        }

        var metadataDictionary =
            new Dictionary<string, BaggageEntryMetadata>(this.metadata ?? EmptyMetadata, StringComparer.OrdinalIgnoreCase);
        if (metadata is not null)
        {
            metadataDictionary[name] = new BaggageEntryMetadata(metadata);
        }

        return new Baggage(
            new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase)
            {
                [name] = value!,
            },
            metadataDictionary);
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.CreateWithMetadata(System.Collections.Generic.Dictionary{string,BaggageEntry})"/>
    /// method instead.
    /// </remarks>>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pairs.</returns>
    // [Obsolete("Call CreateWithMetadata instead.")]
    public Baggage SetBaggage(params KeyValuePair<string, string?>[] baggageItems)
        => this.SetBaggage((IEnumerable<KeyValuePair<string, string?>>)baggageItems);

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <remarks>
    /// Note: This method is obsolete. Call the <see cref="Baggage.CreateWithMetadata(System.Collections.Generic.Dictionary{string,BaggageEntry})"/>
    /// method instead.
    /// </remarks>>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pairs.</returns>
    // [Obsolete("Call CreateWithMetadata instead.")]
    public Baggage SetBaggage(IEnumerable<KeyValuePair<string, string?>> baggageItems)
    {
        if (baggageItems?.Any() != true)
        {
            return this;
        }

        var newBaggage =
            new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase);

        foreach (var item in baggageItems)
        {
            if (string.IsNullOrEmpty(item.Value))
            {
                newBaggage.Remove(item.Key);
            }
            else
            {
                newBaggage[item.Key] = item.Value!;
            }
        }

        return new Baggage(newBaggage);
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> with the key/value pair removed.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <returns>New <see cref="Baggage"/> with the key/value pair removed.</returns>
    public Baggage RemoveBaggage(string name)
    {
        var baggageValue = CopyWithItemRemoved(this.baggage ?? EmptyBaggage, name);
        var baggageMetadata = CopyWithItemRemoved(this.metadata ?? EmptyMetadata, name);

        return new Baggage(baggageValue, baggageMetadata);

        static Dictionary<string, TValue> CopyWithItemRemoved<TValue>(Dictionary<string, TValue> dictionary, string key)
        {
            var newDictionary =
                new Dictionary<string, TValue>(dictionary, StringComparer.OrdinalIgnoreCase);
            newDictionary.Remove(key);
            return newDictionary;
        }
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> with all the key/value pairs removed.
    /// </summary>
    /// <returns>New <see cref="Baggage"/> with all the key/value pairs removed.</returns>
    public Baggage ClearBaggage()
        => default;

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
    /// </summary>
    /// Note: This method is obsolete. Call the <see cref="Baggage.GetEnumeratorWithMetadata()"/>
    /// method instead.
    /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
    // [Obsolete("Call GetEnumeratorWithMetadata instead.")]
    public Dictionary<string, string>.Enumerator GetEnumerator()
        => (this.baggage ?? EmptyBaggage).GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Baggage"/>, returning both values and metadata.
    /// </summary>
    /// <returns><see cref="Baggage.Enumerator"/>.</returns>
    public Enumerator GetEnumeratorWithMetadata()
        => new(this.baggage ?? EmptyBaggage, this.metadata ?? EmptyMetadata);

    /// <inheritdoc/>
    public bool Equals(Baggage other)
    {
        return EqualsHelper(this.baggage, other.baggage) &&
               EqualsHelper(this.metadata, other.metadata);

        static bool EqualsHelper<TValue>(Dictionary<string, TValue>? thisDictionary, Dictionary<string, TValue>? otherDictionary)
        {
            bool dictionaryIsNullOrEmpty = thisDictionary == null || thisDictionary.Count <= 0;

            if (dictionaryIsNullOrEmpty != (otherDictionary == null || otherDictionary.Count <= 0))
            {
                return false;
            }

            return dictionaryIsNullOrEmpty || thisDictionary!.SequenceEqual(otherDictionary!);
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => (obj is Baggage baggage) && this.Equals(baggage);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var baggage = this.baggage ?? EmptyBaggage;

        var hash = 17;
        hash = Hash(baggage, hash);

        var baggageMetadata = this.metadata ?? EmptyMetadata;
        hash = Hash(baggageMetadata, hash);

        return hash;

        static int Hash<TValue>(Dictionary<string, TValue> dictionary, int h)
        {
            foreach (var item in dictionary)
            {
                unchecked
                {
                    h = (h * 23) + dictionary.Comparer.GetHashCode(item.Key);
                    h = (h * 23) + item.Value!.GetHashCode();
                }
            }

            return h;
        }
    }

    private static BaggageHolder EnsureBaggageHolder()
    {
        var baggageHolder = RuntimeContextSlot.Get();
        if (baggageHolder == null)
        {
            baggageHolder = new BaggageHolder();
            RuntimeContextSlot.Set(baggageHolder);
        }

        return baggageHolder;
    }

    private sealed class BaggageHolder
    {
        public Baggage Baggage;
    }

    /// <inheritdoc />
#pragma warning disable SA1201
    public struct Enumerator : IEnumerator<KeyValuePair<string, BaggageEntry>>
#pragma warning restore SA1201
    {
        private readonly Dictionary<string, string> values;
        private readonly Dictionary<string, BaggageEntryMetadata> metadata;
        private Dictionary<string, string>.Enumerator enumerator;

        internal Enumerator(Dictionary<string, string> values, Dictionary<string, BaggageEntryMetadata> metadata)
        {
            this.values = values;
            this.metadata = metadata;

            this.enumerator = values.GetEnumerator();
        }

        /// <inheritdoc />
        public KeyValuePair<string, BaggageEntry> Current
        {
            get
            {
                var current = this.enumerator.Current;
                return new KeyValuePair<string, BaggageEntry>(
                    current.Key,
                    new BaggageEntry(current.Value, this.metadata.TryGetValue(current.Key, out var metadataValue) ? metadataValue : null));
            }
        }

        object? IEnumerator.Current => this.Current;

        /// <inheritdoc />
        public bool MoveNext() => this.enumerator.MoveNext();

        /// <inheritdoc />
        public void Reset() => this.enumerator = this.values.GetEnumerator();

        /// <inheritdoc />
        public void Dispose() => this.enumerator.Dispose();
    }
}
