// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
#if NET
using System.Buffers;
#endif
using System.Diagnostics.CodeAnalysis;
#endif
using System.Net;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A text map propagator for W3C Baggage. See https://w3c.github.io/baggage/.
/// </summary>
public class BaggagePropagator : TextMapPropagator
{
    internal const string BaggageHeaderName = "baggage";

    private const int MaxBaggageLength = 8192;
    private const int MaxBaggageItems = 180;

#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> DecodeHints = SearchValues.Create("%");

    private static readonly SearchValues<char> InvalidKeySearcher = SearchValues.Create(
        "\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F\x7F \"(),/:;<=>?@[\\]{}");

    private static readonly SearchValues<char> InvalidValueSearcher = SearchValues.Create(
        "\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F\x7F \",;\\");

#else

    private static readonly char[] InvalidCharsArray =
    [
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
        '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17',
        '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x7F', ' ', '"', '(', ')', ',', '/', ':', ';', '<',
        '=', '>', '?', '@', '[', '\\', ']', '{', '}',
    ];

    private static readonly char[] InvalidValueChars =
    [
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
        '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17',
        '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x7F', ' ', '"', ',', ';', '\\',
    ];
#endif

    /// <inheritdoc/>
    public override ISet<string> Fields => new HashSet<string> { BaggageHeaderName };

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        if (context.Baggage != default)
        {
            // If baggage has already been extracted, perform a noop.
            return context;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggagePropagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggagePropagator), "null getter");
            return context;
        }

        try
        {
            var baggageCollection = getter(carrier, BaggageHeaderName);
            if (baggageCollection is not null)
            {
                if (TryExtractBaggage(baggageCollection, out var baggageItems))
                {
                    Baggage baggage =
#if NET
                        new(baggageItems);
#else
                        new(baggageItems!);
#endif

                    return new PropagationContext(context.ActivityContext, baggage);
                }
            }

            return new PropagationContext(context.ActivityContext, context.Baggage);
        }
        catch (Exception ex)
        {
            OpenTelemetryApiEventSource.Log.BaggageExtractException(nameof(BaggagePropagator), ex);
        }

        return context;
    }

    /// <inheritdoc/>
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggagePropagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggagePropagator), "null setter");
            return;
        }

        using var e = context.Baggage.GetEnumerator();

        if (e.MoveNext())
        {
            var itemCount = 0;
            var baggage = new StringBuilder();
            do
            {
                var item = e.Current;
                if (string.IsNullOrEmpty(item.Value))
                {
                    continue;
                }

                var encodedKey = EncodeKey(item.Key);
                var encodedValue = EncodeValue(item.Value);
                var baggageItemLength = encodedKey.Length + encodedValue.Length + 1;

                if (baggage.Length > 0)
                {
                    baggageItemLength++;
                }

                if (baggage.Length + baggageItemLength > MaxBaggageLength)
                {
                    break;
                }

                if (baggage.Length > 0)
                {
                    baggage.Append(',');
                }

                baggage.Append(encodedKey)
                       .Append('=')
                       .Append(encodedValue);
            }
            while (e.MoveNext() && ++itemCount < MaxBaggageItems);

            if (baggage.Length > 0)
            {
                setter(carrier, BaggageHeaderName, baggage.ToString());
            }
        }
    }

    internal static bool TryExtractBaggage(
        IEnumerable<string> baggageCollection,
#if NET
        [NotNullWhen(true)]
#endif
        out Dictionary<string, string>? baggage)
    {
        var baggageLength = -1;
        var done = false;
        Dictionary<string, string>? baggageDictionary = null;

        foreach (var item in baggageCollection)
        {
            if (done)
            {
                break;
            }

            if (string.IsNullOrEmpty(item))
            {
                continue;
            }

            var remaining = item.AsSpan();
            while (!remaining.IsEmpty)
            {
                var pair = ReadNextSegment(ref remaining, ',');
                baggageLength += pair.Length + 1; // pair and comma

                if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                {
                    done = true;
                    break;
                }

                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                // Do not decode keys, add a function that checks if key-value pair needs to be dropped
                if (!IsValidKey(pair.Slice(0, separatorIndex)))
                {
                    continue;
                }

                var key = pair.Slice(0, separatorIndex).ToString();

                var rawValue = pair.Slice(separatorIndex + 1);

                var semicolonIndex = rawValue.IndexOf(';');
                if (semicolonIndex >= 0)
                {
                    rawValue = rawValue.Slice(0, semicolonIndex);
                }

                rawValue = rawValue.Trim();

                var value = DecodeIfNeeded(rawValue);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                baggageDictionary ??= new(StringComparer.Ordinal);
                baggageDictionary[key] = value;
            }
        }

        baggage = baggageDictionary;
        return baggageDictionary != null;
    }

    private static ReadOnlySpan<char> ReadNextSegment(ref ReadOnlySpan<char> remaining, char separator)
    {
        var separatorIndex = remaining.IndexOf(separator);
        if (separatorIndex < 0)
        {
            var segment = remaining;
            remaining = [];
            return segment;
        }

        var result = remaining.Slice(0, separatorIndex);
        remaining = remaining.Slice(separatorIndex + 1);
        return result;
    }

    private static string EncodeValue(ReadOnlySpan<char> value)
    {
#if NET8_0_OR_GREATER
        if (!value.ContainsAny(InvalidValueSearcher))
        {
            return value.ToString();
        }
#else
        if (value.IndexOfAny(InvalidValueChars) < 0)
        {
            return value.ToString();
        }
#endif

        const string hex = "0123456789ABCDEF";
        var sb = new StringBuilder(value.Length);
#if NET
        Span<char> encoded = ['%', '\0', '\0'];
#endif
        foreach (var c in value)
        {
            if (IsInvalidValueChar(c))
            {
#if NET
                encoded[1] = hex[(c >> 4) & 0xF];
                encoded[2] = hex[c & 0xF];
                sb.Append(encoded);
#else
                sb.Append('%')
                .Append(hex[(c >> 4) & 0xF])
                .Append(hex[c & 0xF]);
#endif
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string EncodeKey(ReadOnlySpan<char> key)
    {
#if NET8_0_OR_GREATER
        if (!key.ContainsAny(InvalidKeySearcher))
        {
            return key.ToString();
        }
#else
        if (key.IndexOfAny(InvalidCharsArray) < 0)
        {
            return key.ToString();
        }
#endif

        const string hex = "0123456789ABCDEF";
        var sb = new StringBuilder(key.Length);
#if NET
        Span<char> encoded = ['%', '\0', '\0'];
#endif
        foreach (var c in key)
        {
            if (!IsValidKey(c))
            {
#if NET
                encoded[1] = hex[(c >> 4) & 0xF];
                encoded[2] = hex[c & 0xF];
                sb.Append(encoded);
#else
                sb.Append('%')
                .Append(hex[(c >> 4) & 0xF])
                .Append(hex[c & 0xF]);
#endif
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsInvalidValueChar(char c) =>
#if NET8_0_OR_GREATER
        InvalidValueSearcher.Contains(c);
#else
        Array.IndexOf(InvalidValueChars, c) >= 0;
#endif

    private static bool IsValidKey(char c) =>
#if NET8_0_OR_GREATER
        !InvalidKeySearcher.Contains(c);
#else
        Array.IndexOf(InvalidCharsArray, c) < 0;
#endif

    private static bool IsValidKey(ReadOnlySpan<char> key) =>
#if NET8_0_OR_GREATER
        !key.ContainsAny(InvalidKeySearcher);
#else
        key.IndexOfAny(InvalidCharsArray) < 0;
#endif

    private static string DecodeIfNeeded(ReadOnlySpan<char> value) =>
#if NET8_0_OR_GREATER
        value.ContainsAny(DecodeHints) ? WebUtility.UrlDecode(value.ToString()) : value.ToString();
#else
        value.IndexOf('%') < 0 ? value.ToString() : WebUtility.UrlDecode(value.ToString());
#endif
}
