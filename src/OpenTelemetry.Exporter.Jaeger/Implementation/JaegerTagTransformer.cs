// <copyright file="JaegerTagTransformer.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class JaegerTagTransformer : TagTransformer<JaegerTag>
    {
        public static JaegerTagTransformer Instance = new JaegerTagTransformer();

        protected override JaegerTag TransformIntegralTag(string key, long value)
        {
            return new JaegerTag(key, JaegerTagType.LONG, vLong: value);
        }

        protected override JaegerTag TransformFloatingPointTag(string key, double value)
        {
            return new JaegerTag(key, JaegerTagType.DOUBLE, vDouble: value);
        }

        protected override JaegerTag TransformBooleanTag(string key, bool value)
        {
            return new JaegerTag(key, JaegerTagType.BOOL, vBool: value);
        }

        protected override JaegerTag TransformStringTag(string key, string value)
        {
            return new JaegerTag(key, JaegerTagType.STRING, vStr: value);
        }
    }
}
