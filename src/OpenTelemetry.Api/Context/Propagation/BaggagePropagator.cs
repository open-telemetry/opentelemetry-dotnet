// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A text map propagator for W3C Baggage. See https://w3c.github.io/baggage/.
/// </summary>
public class BaggagePropagator : TextMapPropagator
{
    internal const string BaggageHeaderName = "baggage";
    private const string Hex = "0123456789ABCDEF";

    private const int MaxBaggageLength = 8192;
    private const int MaxBaggageItems = 180;

#if NET
    private static readonly SearchValues<char> DecodeHints = SearchValues.Create("%");

    private static readonly SearchValues<char> ValidKeySearcher = SearchValues.Create(
        "!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    // W3C Baggage 3.3 baggage-octet, '%' excluded so raw '%' is always encoded as %25
    private static readonly SearchValues<char> ValidValueSearcher = SearchValues.Create(
        "!#$&'()*+-./:<=>?@[]^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz{}");

#else

    private static readonly char[] ValidKeyChars =
    [
        '!', '#', '$', '%', '&', '\'', '*', '+', '-', '.', '^', '_', '`', '|', '~',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
        'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
    ];

    // baggage-octet minus %, so raw % is always encoded as %25
    private static readonly char[] ValidValueChars =
    [
        '!', '#', '$', '&', '\'', '(', ')', '*', '+', '-', '.', '/', ':',
        '<', '=', '>', '?', '@', '[', ']', '^', '_', '`', '{', '|', '}', '~',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
        'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
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
                baggageLength += pair.Length + 1;

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

                var rawKey = pair.Slice(0, separatorIndex).Trim();

                if (!IsValidKey(rawKey))
                {
                    continue;
                }

                var key = rawKey.ToString();

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

    private static string EncodeKey(ReadOnlySpan<char> key) => Encode(key, isKey: true);

    private static string EncodeValue(ReadOnlySpan<char> value) => Encode(value, isKey: false);

    private static string Encode(ReadOnlySpan<char> value, bool isKey)
    {
#if NET
        if (!value.ContainsAnyExcept(isKey ? ValidKeySearcher : ValidValueSearcher))
        {
            return value.ToString();
        }
#else
        var validChars = isKey ? ValidKeyChars : ValidValueChars;
        var allValid = true;
        foreach (var c in value)
        {
            if (Array.IndexOf(validChars, c) < 0)
            {
                allValid = false;
                break;
            }
        }

        if (allValid)
        {
            return value.ToString();
        }
#endif

        var sb = new StringBuilder(value.Length);

#if NET
        Span<byte> utf8Buffer = stackalloc byte[4];
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.IsAscii)
            {
                var c = (char)rune.Value;
                if (isKey ? !IsValidKey(c) : !IsValidValueChar(c))
                {
                    AppendPercentEncoded(sb, (byte)c);
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                // Non-ASCII rune: always encode as UTF-8 bytes.
                // This correctly handles non-BMP scalar values (emoji, etc.)
                // because Rune represents the full codepoint, not a surrogate half.
                var byteCount = rune.EncodeToUtf8(utf8Buffer);
                foreach (var b in utf8Buffer[..byteCount])
                {
                    AppendPercentEncoded(sb, b);
                }
            }
        }
#else
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];

            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                // Non-BMP pair: encode both chars as one UTF-8 sequence.
                // Passing the pair to Encoding.UTF8 produces the correct 4-byte result
                // rather than two replacement characters.
                foreach (var b in Encoding.UTF8.GetBytes(new string(new[] { c, value[i + 1] })))
                {
                    AppendPercentEncoded(sb, b);
                }

                i += 2;
            }
            else
            {
                var shouldEncode = isKey ? !IsValidKey(c) : !IsValidValueChar(c);
                if (shouldEncode)
                {
                    if (c > '\x7F')
                    {
                        foreach (var b in Encoding.UTF8.GetBytes(c.ToString()))
                        {
                            AppendPercentEncoded(sb, b);
                        }
                    }
                    else
                    {
                        AppendPercentEncoded(sb, (byte)c);
                    }
                }
                else
                {
                    sb.Append(c);
                }

                i++;
            }
        }
#endif

        return sb.ToString();
    }

    private static bool IsValidValueChar(char c) =>
#if NET
        ValidValueSearcher.Contains(c);
#else
        Array.IndexOf(ValidValueChars, c) >= 0;
#endif

    private static bool IsValidKey(char c) =>
#if NET
        ValidKeySearcher.Contains(c);
#else
        Array.IndexOf(ValidKeyChars, c) >= 0;
#endif

    private static bool IsValidKey(ReadOnlySpan<char> key)
    {
#if NET
        return !key.ContainsAnyExcept(ValidKeySearcher);
#else
        foreach (var c in key)
        {
            if (Array.IndexOf(ValidKeyChars, c) < 0)
            {
                return false;
            }
        }

        return true;
#endif
    }

    private static string DecodeIfNeeded(ReadOnlySpan<char> value)
    {
#if NET
        if (!value.ContainsAny(DecodeHints))
        {
            return value.ToString();
        }
#else
        if (value.IndexOf('%') < 0)
        {
            return value.ToString();
        }
#endif

        var sb = new StringBuilder(value.Length);

        var byteBuffer = new byte[value.Length];
        var byteCount = 0;
        var i = 0;

        while (i < value.Length)
        {
            if (value[i] == '%')
            {
                if (i + 2 < value.Length && IsHexDigit(value[i + 1]) && IsHexDigit(value[i + 2]))
                {
                    byteBuffer[byteCount++] = (byte)((HexDigitValue(value[i + 1]) << 4) | HexDigitValue(value[i + 2]));
                    i += 3;
                }
                else
                {
                    FlushByteBuffer(sb, byteBuffer, ref byteCount);
                    sb.Append('\uFFFD');
                    i += Math.Min(3, value.Length - i);
                }
            }
            else
            {
                FlushByteBuffer(sb, byteBuffer, ref byteCount);
                sb.Append(value[i]);
                i++;
            }
        }

        FlushByteBuffer(sb, byteBuffer, ref byteCount);

        return sb.ToString();
    }

    private static void FlushByteBuffer(StringBuilder sb, byte[] buffer, ref int count)
    {
        if (count == 0)
        {
            return;
        }

        sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
        count = 0;
    }

    private static bool IsHexDigit(char c) =>
        char.IsAsciiDigit(c) || c is (>= 'A' and <= 'F') or (>= 'a' and <= 'f');

    private static int HexDigitValue(char c) =>
        c <= '9' ? c - '0' : (c & 0x0f) + 9;

    private static void AppendPercentEncoded(StringBuilder sb, byte b) =>
        sb.Append('%')
            .Append(Hex[(b >> 4) & 0xF])
            .Append(Hex[b & 0xF]);
}
