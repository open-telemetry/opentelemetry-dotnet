// <copyright file="TagTransformerJsonHelperBenchmarks.cs" company="OpenTelemetry Authors">
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
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal;

namespace Benchmarks
{
    /// <summary>
    /// Compares the three different ways to perform JSON array serialization.
    /// </summary>
    [MemoryDiagnoser]
    public partial class TagTransformerJsonHelperBenchmarks
    {
        [Params(new object[]
        {
            new string[] { "short", "a", "", "longer text", "really long text with ch4r2" },
            new char[] { 'a' },
            new int[] { 1, 2, 3, 4, 5 },
        })]

        public Array Value { get; set; }

        [Benchmark]
        public void TagTransformerJsonSerializeArrayTag()
        {
            TagTransformerJsonHelper.JsonSerializeArrayTag(this.Value);
        }

        [Benchmark]
        public void TagTransformerJsonSerializeArrayTag_SourceGen()
        {
            JsonSerializer.Serialize(this.Value, this.Value.GetType(), JsonArrayContext.Default);
        }

        [Benchmark]
        public void TagTransformerJsonSerializeArrayTag_Reflection()
        {
            JsonSerializer.Serialize(this.Value);
        }

        [JsonSerializable(typeof(object))]
        [JsonSerializable(typeof(Array))]
        [JsonSerializable(typeof(string[]))]
        [JsonSerializable(typeof(char[]))]
        [JsonSerializable(typeof(int[]))]
        internal partial class JsonArrayContext : JsonSerializerContext
        {
        }
    }
}
