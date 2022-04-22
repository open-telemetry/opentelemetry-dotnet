// <copyright file="MyClassWithRedactionEnumerator.cs" company="OpenTelemetry Authors">
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

using System.Collections;
using System.Collections.Generic;

internal class MyClassWithRedactionEnumerator : IReadOnlyList<KeyValuePair<string, object>>
{
    private readonly IReadOnlyList<KeyValuePair<string, object>> state;

    public MyClassWithRedactionEnumerator(IReadOnlyList<KeyValuePair<string, object>> state)
    {
        this.state = state;
    }

    public int Count => this.state.Count;

    public KeyValuePair<string, object> this[int index] => this.state[index];

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        foreach (var entry in this.state)
        {
            var entryVal = entry.Value;
            if (entryVal != null && entryVal.ToString() != null && entryVal.ToString().Contains("<secret>"))
            {
                yield return new KeyValuePair<string, object>(entry.Key, "newRedactedValueHere");
            }
            else
            {
                yield return entry;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
