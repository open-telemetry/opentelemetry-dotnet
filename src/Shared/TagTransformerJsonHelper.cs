// <copyright file="TagTransformerJsonHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
