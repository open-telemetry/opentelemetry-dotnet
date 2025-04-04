// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private static readonly RuntimeContextSlot<BaggageHolder> RuntimeContextSlot = RuntimeContext.RegisterSlot<BaggageHolder>("otel.baggage");
    private static readonly Dictionary<string, string> EmptyBaggage = [];

    private readonly Dictionary<string, string> baggage;

    /// <summary>
    /// Initializes a new instance of the <see cref="Baggage"/> struct.
    /// </summary>
    /// <param name="baggage">Baggage key/value pairs.</param>
    internal Baggage(Dictionary<string, string> baggage)
    {
        this.baggage = baggage;
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
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns><see cref="Baggage"/>.</returns>
    public static Baggage Create(Dictionary<string, string>? baggageItems = null)
    {
        if (baggageItems == null)
        {
            return default;
        }

        Dictionary<string, string> baggageCopy = new Dictionary<string, string>(baggageItems.Count, StringComparer.OrdinalIgnoreCase);
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
    /// Returns the name/value pairs in the <see cref="Baggage"/>.
    /// </summary>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>Baggage key/value pairs.</returns>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public static IReadOnlyDictionary<string, string> GetBaggage(Baggage baggage = default)
        => baggage == default ? Current.GetBaggage() : baggage.GetBaggage();

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
    /// </summary>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
    public static Dictionary<string, string>.Enumerator GetEnumerator(Baggage baggage = default)
        => baggage == default ? Current.GetEnumerator() : baggage.GetEnumerator();

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
    public static Baggage SetBaggage(string name, string? value, Baggage baggage = default)
    {
        var baggageHolder = EnsureBaggageHolder();
        lock (baggageHolder)
        {
            return baggageHolder.Baggage = baggage == default
                ? baggageHolder.Baggage.SetBaggage(name, value)
                : baggage.SetBaggage(name, value);
        }
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <param name="baggage">Optional <see cref="Baggage"/>. <see cref="Current"/> is used if not specified.</param>
    /// <returns>New <see cref="Baggage"/> containing the new key/value pairs.</returns>
    /// <remarks>Note: The <see cref="Baggage"/> returned will be set as the new <see cref="Current"/> instance.</remarks>
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
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
    /// <returns>Baggage key/value pairs.</returns>
    public IReadOnlyDictionary<string, string> GetBaggage()
        => this.baggage ?? EmptyBaggage;

    /// <summary>
    /// Returns the value associated with the given name, or <see langword="null"/> if the given name is not present.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <returns>Baggage item or <see langword="null"/> if nothing was found.</returns>
    public string? GetBaggage(string name)
    {
        Guard.ThrowIfNullOrEmpty(name);

        return this.baggage != null && this.baggage.TryGetValue(name, out string? value)
            ? value
            : null;
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pair.
    /// </summary>
    /// <param name="name">Baggage item name.</param>
    /// <param name="value">Baggage item value.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pair.</returns>
    public Baggage SetBaggage(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return this.RemoveBaggage(name);
        }

        return new Baggage(
            new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase)
            {
                [name] = value!,
            });
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pairs.</returns>
    public Baggage SetBaggage(params KeyValuePair<string, string?>[] baggageItems)
        => this.SetBaggage((IEnumerable<KeyValuePair<string, string?>>)baggageItems);

    /// <summary>
    /// Returns a new <see cref="Baggage"/> which contains the new key/value pairs.
    /// </summary>
    /// <param name="baggageItems">Baggage key/value pairs.</param>
    /// <returns>New <see cref="Baggage"/> containing the key/value pairs.</returns>
    public Baggage SetBaggage(IEnumerable<KeyValuePair<string, string?>> baggageItems)
    {
        if (baggageItems?.Any() != true)
        {
            return this;
        }

        var newBaggage = new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase);

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
        var baggage = new Dictionary<string, string>(this.baggage ?? EmptyBaggage, StringComparer.OrdinalIgnoreCase);
        baggage.Remove(name);

        return new Baggage(baggage);
    }

    /// <summary>
    /// Returns a new <see cref="Baggage"/> with all the key/value pairs removed.
    /// </summary>
    /// <returns>New <see cref="Baggage"/> with all the key/value pairs removed.</returns>
#pragma warning disable CA1822 // Mark members as static
    public Baggage ClearBaggage()
#pragma warning restore CA1822 // Mark members as static
        => default;

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Baggage"/>.
    /// </summary>
    /// <returns><see cref="Dictionary{TKey, TValue}.Enumerator"/>.</returns>
    public Dictionary<string, string>.Enumerator GetEnumerator()
        => (this.baggage ?? EmptyBaggage).GetEnumerator();

    /// <inheritdoc/>
    public bool Equals(Baggage other)
    {
        bool baggageIsNullOrEmpty = this.baggage == null || this.baggage.Count <= 0;

        if (baggageIsNullOrEmpty != (other.baggage == null || other.baggage.Count <= 0))
        {
            return false;
        }

        return baggageIsNullOrEmpty || this.baggage!.SequenceEqual(other.baggage!);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => (obj is Baggage baggage) && this.Equals(baggage);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var baggage = this.baggage ?? EmptyBaggage;

        var hash = 17;
        foreach (var item in baggage)
        {
            unchecked
            {
                hash = (hash * 23) + baggage.Comparer.GetHashCode(item.Key);
#if NET
                hash = (hash * 23) + item.Value.GetHashCode(StringComparison.Ordinal);
#else
                hash = (hash * 23) + item.Value.GetHashCode();
#endif
            }
        }

        return hash;
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
}
