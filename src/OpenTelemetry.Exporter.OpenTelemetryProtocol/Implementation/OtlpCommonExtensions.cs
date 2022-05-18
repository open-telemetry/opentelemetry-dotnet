// <copyright file="OtlpCommonExtensions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OtlpCommon = Opentelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class OtlpCommonExtensions
    {
        private static OtlpKeyValueTransformer tagTransformer = new OtlpKeyValueTransformer();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OtlpCommon.KeyValue ToOtlpAttribute(this KeyValuePair<string, object> kvp)
        {
            return tagTransformer.TransformTag(kvp);
        }
    }
}
