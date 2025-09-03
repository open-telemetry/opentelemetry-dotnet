// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources;

internal sealed class OtelEnvResourceDetector : IResourceDetector
{
    public const string EnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";
    private const char AttributeListSplitter = ',';
    private const char AttributeKeyValueSplitter = '=';

    private readonly IConfiguration configuration;

    public OtelEnvResourceDetector(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Resource Detect()
    {
        var resource = Resource.Empty;

        if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
        {
            var attributes = ParseResourceAttributes(envResourceAttributeValue);
            resource = new Resource(attributes);
        }

        return resource;
    }

    private static List<KeyValuePair<string, object>> ParseResourceAttributes(string resourceAttributes)
    {
        var attributes = new List<KeyValuePair<string, object>>();

        string[] rawAttributes = resourceAttributes.Split(AttributeListSplitter);
        foreach (string rawKeyValuePair in rawAttributes)
        {
#if NETSTANDARD2_1 || NET8_0_OR_GREATER
            var indexOfFirstEquals = rawKeyValuePair.IndexOf(AttributeKeyValueSplitter.ToString(), StringComparison.Ordinal);
#else
            var indexOfFirstEquals = rawKeyValuePair.IndexOf(AttributeKeyValueSplitter);
#endif
            if (indexOfFirstEquals == -1)
            {
                continue;
            }

            var key = rawKeyValuePair.Substring(0, indexOfFirstEquals).Trim();
            var value = rawKeyValuePair.Substring(indexOfFirstEquals + 1).Trim();

            if (!IsValidKeyValuePair(key, value))
            {
                continue;
            }

            var decodedValue = DecodeValue(value);

            attributes.Add(new KeyValuePair<string, object>(key, decodedValue));
        }

        return attributes;
    }

    private static bool IsValidKeyValuePair(string key, string value) =>
        !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value) && key.All(c => c <= 127);

    private static string DecodeValue(string baggageEncoded)
    {
        var bytes = new List<byte>();
        for (int i = 0; i < baggageEncoded.Length; i++)
        {
            if (baggageEncoded[i] == '%' && i + 2 < baggageEncoded.Length && IsHex(baggageEncoded[i + 1]) && IsHex(baggageEncoded[i + 2]))
            {
                string hex = baggageEncoded.Substring(i + 1, 2);
                bytes.Add(Convert.ToByte(hex, 16));

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
