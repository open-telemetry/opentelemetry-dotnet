// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
#if NET9_0_OR_GREATER
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

    private static readonly char[] InvalidCharsArray =
        Enumerable.Range(0, 32).Select(c => (char)c)
        .Concat(new[] { (char)127 })
        .Concat("()<>@,;:\\\"/[]?={} \t".ToCharArray())
        .Distinct()
        .ToArray();

    private static readonly char[] InvalidValueChars =
        Enumerable.Range(0, 32).Select(c => (char)c)
        .Concat(new[] { (char)127 })
        .Concat(new[] { ',', ';', '"', '\\', ' ' })
        .Distinct()
        .ToArray();

#if NET9_0_OR_GREATER
    private static readonly SearchValues<char> DecodeHints = SearchValues.Create('%');

    private static readonly SearchValues<char> InvalidKeySearcher = SearchValues.Create(InvalidCharsArray);

    private static readonly SearchValues<char> InvalidValueSearcher =
        SearchValues.Create(InvalidValueChars);
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
#if NET9_0_OR_GREATER
        if (!value.ContainsAny(InvalidValueSearcher))
        {
            return value.ToString(); // fast path
        }
#else
        if (value.IndexOfAny(InvalidValueChars) < 0)
        {
            return value.ToString(); // fast path
        }
#endif

        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (IsInvalidValueChar(c))
            {
                sb.Append('%')
                .Append(((int)c).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
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
#if NET9_0_OR_GREATER
        if (!key.ContainsAny(InvalidKeySearcher))
        {
            return key.ToString(); // fast path
        }
#else
        if (key.IndexOfAny(InvalidCharsArray) < 0)
        {
            return key.ToString(); // fast path
        }
#endif

        var sb = new StringBuilder(key.Length);

        foreach (var c in key)
        {
            if (!IsValidKey(new ReadOnlySpan<char>([c])))
            {
                sb.Append('%')
                .Append(((int)c).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsInvalidValueChar(char c)
    {
        if (c <= 31 || c == 127)
        {
            return true;
        }

        return c == ',' || c == ';' || c == '"' || c == '\\' || c == ' ';
    }

    private static bool IsValidKey(ReadOnlySpan<char> key) =>
#if NET9_0_OR_GREATER
        key.ContainsAny(InvalidKeySearcher);
#else
        key.IndexOfAny(InvalidCharsArray) < 0;
#endif

    private static string DecodeIfNeeded(ReadOnlySpan<char> value) =>
#if NET9_0_OR_GREATER
        value.ContainsAny(DecodeHints) ? WebUtility.UrlDecode(value.ToString()) : value.ToString();
#else
        value.IndexOf('%') < 0 ? value.ToString() : WebUtility.UrlDecode(value.ToString());
#endif
}
