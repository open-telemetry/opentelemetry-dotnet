// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

    private static readonly char[] EqualSignSeparator = new[] { '=' };
    private static readonly char[] CommaSignSeparator = new[] { ',' };

    /// <inheritdoc/>
    public override ISet<string> Fields => new HashSet<string> { BaggageHeaderName };

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
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
            Dictionary<string, string> baggage = null;
            var baggageCollection = getter(carrier, BaggageHeaderName);
            if (baggageCollection?.Any() ?? false)
            {
                TryExtractBaggage(baggageCollection.ToArray(), out baggage);
            }

            return new PropagationContext(
                context.ActivityContext,
                baggage == null ? context.Baggage : new Baggage(baggage));
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
            StringBuilder baggage = null;
            do
            {
                KeyValuePair<string, string> item = e.Current;

                if (!ValidateKey(item.Key))
                {
                    continue;
                }

                if (baggage == null)
                {
                    baggage = new StringBuilder();
                }

                baggage.Append(item.Key).Append('=').Append(Uri.EscapeDataString(item.Value)).Append(',');
                itemCount++;
                if (baggage.Length >= MaxBaggageLength)
                {
                    break;
                }
            }
            while (e.MoveNext() && itemCount < MaxBaggageItems);

            if (baggage is not null)
            {
                baggage.Remove(baggage.Length - 1, 1);
                setter(carrier, BaggageHeaderName, baggage.ToString());
            }
        }
    }

    internal static bool TryExtractBaggage(string[] baggageCollection, out Dictionary<string, string> baggage)
    {
        int baggageLength = -1;
        bool done = false;
        Dictionary<string, string> baggageDictionary = null;

        foreach (var item in baggageCollection)
        {
            if (done)
            {
                break;
            }

            if (string.IsNullOrEmpty(item))
            {
                baggage = null;
                return false;
            }

            foreach (var pair in item.Split(CommaSignSeparator))
            {
                baggageLength += pair.Length + 1; // pair and comma

                if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                {
                    done = true;
                    break;
                }

                if (pair.IndexOf('=') < 0)
                {
                    baggage = null;
                    return false;
                }

                var parts = pair.Split(EqualSignSeparator, 2);
                if (parts.Length != 2)
                {
                    baggage = null;
                    return false;
                }

                var key = parts[0].Trim();

                if (!ValidateKey(key))
                {
                    baggage = null;
                    return false;
                }

                var encodedValue = parts[1].Trim();

                if (!ValidateValue(encodedValue))
                {
                    baggage = null;
                    return false;
                }

                var decodedValue = Uri.UnescapeDataString(encodedValue);

                if (baggageDictionary == null)
                {
                    baggageDictionary = new Dictionary<string, string>();
                }

                baggageDictionary[key] = decodedValue;
            }
        }

        baggage = baggageDictionary;
        return baggageDictionary != null;
    }

    private static bool ValidateValue(string encodedValue)
    {
        var index = 0;
        while (index < encodedValue.Length)
        {
            var c = encodedValue[index];

            if (c == '%')
            {
                if (!ValidatePercentEncoding(index, encodedValue))
                {
                    OpenTelemetryApiEventSource.Log.BaggageItemValueIsInvalid(encodedValue);
                    return false;
                }

                index += 3;
            }
            else if (!ValidateValueCharInRange(c))
            {
                OpenTelemetryApiEventSource.Log.BaggageItemValueIsInvalid(encodedValue);
                return false;
            }
            else
            {
                index++;
            }
        }

        return true;
    }

    private static bool ValidatePercentEncoding(int index, string encodedValue)
    {
        return index < encodedValue.Length - 2 &&
               ValidateHexChar(encodedValue[index + 1]) &&
               ValidateHexChar(encodedValue[index + 2]);
    }

    private static bool ValidateHexChar(char c)
    {
        return c is
            >= '0' and <= '9' or
            >= 'A' and <= 'F' or
            >= 'a' and <= 'f';
    }

    private static bool ValidateValueCharInRange(char c)
    {
        // https://w3c.github.io/baggage/#definition
        // value                  =  *baggage-octet
        // baggage-octet          =  %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
        //     ; US-ASCII characters excluding CTLs,
        //     ; whitespace, DQUOTE, comma, semicolon,
        // ; and backslash
        return c is
            '\u0021' or
            >= '\u0023' and <= '\u002b' or
            >= '\u002d' and <= '\u003a' or
            >= '\u003c' and <= '\u005b' or
            >= '\u005d' and <= '\u007e';
    }

    private static bool ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (var c in key)
        {
            // chars permitted in token, as defined in:
            // https://datatracker.ietf.org/doc/html/rfc7230#section-3.2.6
            if (!ValidateTokenChar(c) && !ValidateAsciiLetterOrDigit(c))
            {
                OpenTelemetryApiEventSource.Log.BaggageItemKeyIsInvalid(key);
                return false;
            }
        }

        return true;
    }

    private static bool ValidateTokenChar(char c)
    {
        // https://datatracker.ietf.org/doc/html/rfc7230#section-3.2.6
        // token          = 1*tchar
        // tchar          = "!" / "#" / "$" / "%" / "&" / "'" / "*"
        //                  / "+" / "-" / "." / "^" / "_" / "`" / "|" / "~"
        return c is '!' or '#' or '$' or '%' or '&' or '\'' or '*'
            or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
    }

    private static bool ValidateAsciiLetterOrDigit(char c)
    {
        return c is
            >= '0' and <= '9' or
            >= 'A' and <= 'Z' or
            >= 'a' and <= 'z';
    }
}
