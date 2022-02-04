// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static readonly CircularBuffer<Item> Buffer = new CircularBuffer<Item>(500);

    public static void Main()
    {
        Stress(concurrency: 5, prometheusPort: 9184);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        Buffer.TryAdd(new Item(0), 10000);
        Buffer.Read();
    }

    internal class Item
    {
        internal Item(long value)
        {
            this.Value = value;
        }

        public long Value { get; }
    }
}
