// <copyright file="DataPoint{T}.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;

#nullable enable

namespace OpenTelemetry.Metrics
{
    internal class DataPoint<T> : DataPoint
        where T : struct
    {
        internal readonly T Value;

        public DataPoint(T value, params KeyValuePair<string, object?>[] tags)
            : base(new ReadOnlySpan<KeyValuePair<string, object?>>(tags))
        {
            this.Value = value;
        }

        public DataPoint(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
            : base(tags)
        {
            this.Value = value;
        }

        public override DataPoint NewWithTags(params KeyValuePair<string, object?>[] tags)
        {
            return new DataPoint<T>(this.Value, tags);
        }

        public override string ValueAsString()
        {
            return this.Value.ToString();
        }
    }
}
