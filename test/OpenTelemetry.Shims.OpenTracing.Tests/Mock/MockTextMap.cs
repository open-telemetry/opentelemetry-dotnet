// <copyright file="MockTextMap.cs" company="OpenTelemetry Authors">
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
using OpenTracing.Propagation;

namespace OpenTelemetry.Shims.OpenTracing.Tests.Mock;

internal class MockTextMap : ITextMap
{
    public bool GetEnumeratorCalled { get; private set; }
    public bool SetCalled { get; private set; }

#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
    public Dictionary<string, string> Items { get; } = [];
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        this.GetEnumeratorCalled = true;
        return this.Items.GetEnumerator();
    }

    public void Set(string key, string value)
    {
        this.SetCalled = true;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
