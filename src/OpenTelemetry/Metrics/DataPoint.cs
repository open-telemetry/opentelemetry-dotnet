// <copyright file="DataPoint.cs" company="OpenTelemetry Authors">
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
    public abstract class DataPoint
    {
        private KeyValuePair<string, object?>[] tags;

        public DataPoint(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            this.tags = tags.ToArray();
        }

        public ReadOnlySpan<KeyValuePair<string, object?>> Tags
        {
            get
            {
                return new ReadOnlySpan<KeyValuePair<string, object?>>(this.tags);
            }
        }

        public void SetTags(KeyValuePair<string, object?>[] tags)
        {
            this.tags = tags;
        }

        public virtual string ValueAsString()
        {
            throw new NotImplementedException();
        }
    }
}
