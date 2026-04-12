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

#if !NET
    private static readonly char[] CommaSignSeparator = [','];
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

                baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
            }
            while (e.MoveNext() && ++itemCount < MaxBaggageItems && baggage.Length < MaxBaggageLength);
            baggage.Remove(baggage.Length - 1, 1);
            setter(carrier, BaggageHeaderName, baggage.ToString());
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

#if NET
            var span = item.AsSpan();
            while (!span.IsEmpty)
            {
                ReadOnlySpan<char> pairSpan;

                var index = span.IndexOf(',');
                if (index < 0)
                {
                    pairSpan = span;
                    span = default;
                }
                else
                {
                    pairSpan = span[..index];
                    span = span[(index + 1)..];
                }

                baggageLength += pairSpan.Length + 1;

                if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                {
                    done = true;
                    break;
                }

                index = pairSpan.IndexOf('=');
                if (index < 0)
                {
                    continue;
                }

                var key = WebUtility.UrlDecode(pairSpan[..index].ToString());
                var value = WebUtility.UrlDecode(pairSpan[(index + 1)..].ToString());

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                baggageDictionary ??= [];
                baggageDictionary[key] = value;
            }
#else
            foreach (var pair in item.Split(CommaSignSeparator))
            {
                baggageLength += pair.Length + 1;

                if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                {
                    done = true;
                    break;
                }

                var index = pair.IndexOf('=');
                if (index < 0)
                {
                    continue;
                }

                var key = WebUtility.UrlDecode(pair.Substring(0, index));
                var value = WebUtility.UrlDecode(pair.Substring(index + 1));

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                baggageDictionary ??= [];
                baggageDictionary[key] = value;
            }
#endif
        }

        baggage = baggageDictionary;
        return baggageDictionary != null;
    }
}
