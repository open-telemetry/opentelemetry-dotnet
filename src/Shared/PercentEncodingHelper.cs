// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Internal;

/// <summary>
/// Helper methods for percent-encoding and decoding baggage values.
/// See https://w3c.github.io/baggage/.
/// </summary>
internal static partial class PercentEncodingHelper
{
    private const int MaxBaggageLength = 8192;
    private const int MaxBaggageItems = 180;
    private const char KeyValueSplitter = '=';
    private const char ListSplitter = ',';

    internal static bool TryExtractBaggage(
        string[] baggageCollection,
#if NET
        [NotNullWhen(true)]
#endif
        out Dictionary<string, string>? baggage)
    {
        Dictionary<string, string>? baggageDictionary = null;
        int baggageLength = -1; // Start with -1 to account for no leading comma on first item

        foreach (var baggageList in baggageCollection.Where(h => !string.IsNullOrEmpty(h)))
        {
            foreach (string keyValuePair in baggageList.Split(ListSplitter))
            {
                baggageLength += keyValuePair.Length + 1; // pair length + comma
                if (ExceedsMaxBaggageLimits(baggageLength, baggageDictionary?.Count))
                {
                    baggage = baggageDictionary;
                    return baggageDictionary != null;
                }
#if NET
                var indexOfFirstEquals = keyValuePair.IndexOf(KeyValueSplitter, StringComparison.Ordinal);
#else
                var indexOfFirstEquals = keyValuePair.IndexOf(KeyValueSplitter);
#endif
                if (indexOfFirstEquals < 0)
                {
                    continue;
                }

                var splitKeyValue = keyValuePair.Split([KeyValueSplitter], 2);
                var key = splitKeyValue[0].Trim();
                var value = splitKeyValue[1].Trim();

                if (!IsValidKeyValuePair(key, value))
                {
                    continue;
                }

                var decodedValue = PercentDecodeBaggage(value);

                baggageDictionary ??= [];
                baggageDictionary[key] = decodedValue;
            }
        }

        baggage = baggageDictionary;
        return baggageDictionary != null;
    }

    /// <summary>
    /// As per the specification, only the value is percent-encoded.
    /// "Uri.EscapeDataString" encodes code points which are not required to be percent-encoded.
    /// </summary>
    /// <param name="key"> The baggage key. </param>
    /// <param name="value"> The baggage value. </param>
    /// <returns> The percent-encoded baggage item. </returns>
    internal static string PercentEncodeBaggage(string key, string value) => $"{key.Trim()}={Uri.EscapeDataString(value.Trim())}";

    private static string PercentDecodeBaggage(string baggageEncoded)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < baggageEncoded.Length; i++)
        {
            if (baggageEncoded[i] == '%' && i + 2 < baggageEncoded.Length && IsHex(baggageEncoded[i + 1]) && IsHex(baggageEncoded[i + 2]))
            {
                var hex = baggageEncoded.AsSpan(i + 1, 2);
#if NET
                bytes.Add(byte.Parse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));
#else
                bytes.Add(Convert.ToByte(hex.ToString(), 16));
#endif

                i += 2;
            }
            else if (baggageEncoded[i] == '%')
            {
                return baggageEncoded; // Bad percent triplet -> return original value
            }
            else
            {
                if (!IsBaggageOctet(baggageEncoded[i]))
                {
                    return baggageEncoded; // non-encoded character not baggage octet encoded -> return original value
                }

                bytes.Add((byte)baggageEncoded[i]);
            }
        }

        return new UTF8Encoding(false, false).GetString(bytes.ToArray());
    }

#if NET
    [GeneratedRegex(@"^[!#$%&'*+\-\.^_`|~0-9A-Z]+$", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();
#else

#pragma warning disable SA1201 // A field should not follow a method
    private static readonly Regex TokenRegexField = new(
        @"^[!#$%&'*+\-\.^_`|~0-9A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#pragma warning restore SA1201 // A field should not follow a method

    private static Regex TokenRegex() => TokenRegexField;
#endif

    private static bool ExceedsMaxBaggageLimits(int currentLength, int? currentItemCount) =>
        currentLength >= MaxBaggageLength || currentItemCount >= MaxBaggageItems;

    private static bool IsValidKeyValuePair(string key, string value) =>
        !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value) && TokenRegex().IsMatch(key);

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') ||
        (c >= 'a' && c <= 'f') ||
        (c >= 'A' && c <= 'F');

    private static bool IsBaggageOctet(char c) =>
        c == 0x21 ||
        (c >= 0x23 && c <= 0x2B) ||
        (c >= 0x2D && c <= 0x3A) ||
        (c >= 0x3C && c <= 0x5B) ||
        (c >= 0x5D && c <= 0x7E);
}
