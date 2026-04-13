// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
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
            if (baggageCollection?.Any() ?? false)
            {
                if (TryExtractBaggage([.. baggageCollection], out var baggageItems))
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

                var encodedKey = WebUtility.UrlEncode(item.Key);
                var encodedValue = WebUtility.UrlEncode(item.Value);
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
        string[] baggageCollection,
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

                var key = DecodeIfNeeded(pair.Slice(0, separatorIndex));
                var value = DecodeIfNeeded(pair.Slice(separatorIndex + 1));

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                baggageDictionary ??= new(MaxBaggageItems, StringComparer.Ordinal);

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

    private static string DecodeIfNeeded(ReadOnlySpan<char> value) =>
        value.IndexOfAny('%', '+') < 0 ? value.ToString() : WebUtility.UrlDecode(value.ToString());
}
