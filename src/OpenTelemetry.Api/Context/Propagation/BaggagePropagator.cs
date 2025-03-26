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

    private static readonly char[] EqualSignSeparator = ['='];
    private static readonly char[] CommaSignSeparator = [','];

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
                if (TryExtractBaggage([.. baggageCollection], out var baggage))
                {
                    return new PropagationContext(context.ActivityContext, new Baggage(baggage!));
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

        if (e.MoveNext() == true)
        {
            int itemCount = 0;
            StringBuilder baggage = new StringBuilder();
            do
            {
                KeyValuePair<string, string> item = e.Current;
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
        string[] baggageCollection,
#if NET
        [NotNullWhen(true)]
#endif
        out Dictionary<string, string>? baggage)
    {
        int baggageLength = -1;
        bool done = false;
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

            foreach (var pair in item.Split(CommaSignSeparator))
            {
                baggageLength += pair.Length + 1; // pair and comma

                if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                {
                    done = true;
                    break;
                }

#if NET
                if (pair.IndexOf('=', StringComparison.Ordinal) < 0)
#else
                if (pair.IndexOf('=') < 0)
#endif
                {
                    continue;
                }

                var parts = pair.Split(EqualSignSeparator, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = WebUtility.UrlDecode(parts[0]);
                var value = WebUtility.UrlDecode(parts[1]);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                baggageDictionary ??= new Dictionary<string, string>();

                baggageDictionary[key] = value;
            }
        }

        baggage = baggageDictionary;
        return baggageDictionary != null;
    }
}
