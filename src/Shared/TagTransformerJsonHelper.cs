// <copyright file="TagTransformer.Json.cs" company="OpenTelemetry Authors">
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Internal;

// The class has to be partial so that JSON source generator can provide code for the JsonSerializerContext
#pragma warning disable SA1601 // Partial elements should be documented
internal static partial class TagTransformerJsonHelper
{
    internal static string JsonSerializeArrayTag(Array array)
    {
        return JsonSerializer.Serialize(array, typeof(object), ArrayTagJsonContext.Default);
    }

    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(char[]))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(bool[]))]
    [JsonSerializable(typeof(byte[]))]
    [JsonSerializable(typeof(sbyte[]))]
    [JsonSerializable(typeof(short[]))]
    [JsonSerializable(typeof(ushort[]))]
    [JsonSerializable(typeof(int[]))]
    [JsonSerializable(typeof(uint[]))]
    [JsonSerializable(typeof(long[]))]
    [JsonSerializable(typeof(float[]))]
    [JsonSerializable(typeof(double[]))]
    private sealed partial class ArrayTagJsonContext : JsonSerializerContext
    {
    }
}
