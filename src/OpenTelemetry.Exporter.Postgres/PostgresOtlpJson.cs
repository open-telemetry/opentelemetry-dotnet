// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text.Json;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

internal static class PostgresOtlpJson
{
    public static string SerializeAttributes(IEnumerable<KeyValuePair<string, object?>> attributes)
        => JsonSerializer.Serialize(attributes.Select(static attribute => new
        {
            key = attribute.Key,
            value = ToJsonElement(SerializeAnyValue(attribute.Value)),
        }));

    public static string SerializeResource(Resource resource)
        => JsonSerializer.Serialize(new
        {
            attributes = resource.Attributes.ToDictionary(static attribute => attribute.Key, static attribute => attribute.Value),
            schemaUrl = resource.SchemaUrl,
        });

    public static string? SerializeAnyValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        object result = value switch
        {
            string text => new { stringValue = text },
            bool boolean => new { boolValue = boolean },
            byte[] bytes => new { bytesValue = Convert.ToBase64String(bytes) },
            byte or sbyte or short or ushort or int or uint or long or ulong => new { intValue = Convert.ToString(value, CultureInfo.InvariantCulture) },
            float or double or decimal => new { doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture) },
            IEnumerable enumerable when value is not string => new
            {
                arrayValue = new
                {
                    values = enumerable.Cast<object?>().Select(static item => ToJsonElement(SerializeAnyValue(item))),
                },
            },
            _ => new { stringValue = value.ToString() },
        };

        return JsonSerializer.Serialize(result);
    }

    public static JsonElement ToJsonElement(string? value)
        => value is null ? default : JsonSerializer.Deserialize<JsonElement>(value);

    public static long ToUnixTimeNanoseconds(DateTime timestamp)
        => timestamp == DateTime.MinValue
            ? 0
            : checked((timestamp.ToUniversalTime().Ticks - DateTime.UnixEpoch.Ticks) * 100);

    public static long ToUnixTimeNanoseconds(DateTimeOffset timestamp)
        => timestamp == DateTimeOffset.MinValue
            ? 0
            : checked((timestamp.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks) * 100);
}