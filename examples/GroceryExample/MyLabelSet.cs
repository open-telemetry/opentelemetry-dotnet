// <copyright file="MyLabelSet.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;

#pragma warning disable CS0618

namespace GroceryExample
{
    public class MyLabelSet : LabelSet
    {
        public MyLabelSet(params KeyValuePair<string, string>[] labels)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            foreach (var kv in labels)
            {
                list.Add(kv);
            }

            this.Labels = list;
        }

        public override IEnumerable<KeyValuePair<string, string>> Labels { get; set; } = System.Linq.Enumerable.Empty<KeyValuePair<string, string>>();
    }
}
