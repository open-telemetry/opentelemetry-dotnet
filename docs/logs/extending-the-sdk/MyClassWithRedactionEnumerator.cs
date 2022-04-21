// <copyright file="MyRedactionProcessor.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Text.RegularExpressions;

internal class MyClassWithRedactionEnumerator : IReadOnlyList<KeyValuePair<string, object>>
{
    private static readonly Regex Rgx = new("\\(?\\d{3}[\\.|\\)]?\\d{3}[\\.|\\-]\\d{4}");
    private readonly IReadOnlyList<KeyValuePair<string, object>> myList;

    public MyClassWithRedactionEnumerator(IReadOnlyList<KeyValuePair<string, object>> traits)
    {
        this.myList = traits;
    }

    public int Count => this.myList.Count;

    public KeyValuePair<string, object> this[int index] => this.myList[index];

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        foreach (var entry in this.myList)
        {
            var entryVal = entry.Value;
            if (entryVal != null && Rgx.IsMatch(entryVal.ToString()))
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

    public override string ToString()
    {
        var cur = this.GetEnumerator();
        var sb = new StringBuilder();

        cur.MoveNext();
        sb.Append(cur.Current.ToString());
        while (cur.MoveNext())
        {
            sb.Append(", ");
            sb.Append(cur.Current.ToString());
        }

        return sb.ToString();
    }
}
