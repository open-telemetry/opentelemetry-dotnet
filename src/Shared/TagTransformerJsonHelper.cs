// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Text.Json;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace OpenTelemetry.Internal;

/// <summary>
/// This class has to be partial so that JSON source generator can provide code for the JsonSerializerContext.
/// </summary>
internal static partial class TagTransformerJsonHelper
{
#if NET6_0_OR_GREATER
    // In net6.0 or higher ships System.Text.Json "in box" as part of the base class libraries;
    // meaning the consumer automatically got upgraded to use v6.0 System.Text.Json
    // which has support for using source generators for JSON serialization.
    // The source generator makes the serialization faster and also AOT compatible.

    internal static string JsonSerializeArrayTag(Array array)
    {
        return JsonSerializer.Serialize(array, typeof(Array), ArrayTagJsonContext.Default);
    }

    [JsonSerializable(typeof(Array))]
    [JsonSerializable(typeof(char))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(byte))]
    [JsonSerializable(typeof(sbyte))]
    [JsonSerializable(typeof(short))]
    [JsonSerializable(typeof(ushort))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(uint))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(double))]
    private sealed partial class ArrayTagJsonContext : JsonSerializerContext
    {
    }

#else
    internal static string JsonSerializeArrayTag(Array array)
    {
        return JsonSerializer.Serialize(array);
    }
#endif
}
