// <copyright file="ZipkinTagTransformer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal class ZipkinTagTransformer : TagTransformer<bool>
    {
        private readonly Utf8JsonWriter writer;

        public ZipkinTagTransformer(Utf8JsonWriter writer)
        {
            this.writer = writer;
        }

        protected override bool JsonifyArrays => true;

        protected override bool TransformIntegralTag(string key, long value)
        {
            this.writer.WriteString(key, value.ToString());
            return true;
        }

        protected override bool TransformFloatingPointTag(string key, double value)
        {
            this.writer.WriteString(key, value.ToString());
            return true;
        }

        protected override bool TransformBooleanTag(string key, bool value)
        {
            this.writer.WriteString(key, value ? "true" : "false");
            return true;
        }

        protected override bool TransformStringTag(string key, string value)
        {
            this.writer.WriteString(key, value.ToString());
            return true;
        }
    }
}
