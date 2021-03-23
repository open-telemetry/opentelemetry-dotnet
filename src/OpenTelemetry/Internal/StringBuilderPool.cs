// <copyright file="StringBuilderPool.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace OpenTelemetry.Internal
{
    // Based on Microsoft.Extensions.ObjectPool
    // https://github.com/dotnet/aspnetcore/blob/main/src/ObjectPool/src/DefaultObjectPool.cs
    internal class StringBuilderPool
    {
        internal static StringBuilderPool Instance = new StringBuilderPool();

        private protected readonly ObjectWrapper[] items;
        private protected StringBuilder firstItem;

        public StringBuilderPool()
            : this(Environment.ProcessorCount * 2)
        {
        }

        public StringBuilderPool(int maximumRetained)
        {
            // -1 due to _firstItem
            this.items = new ObjectWrapper[maximumRetained - 1];
        }

        public int MaximumRetainedCapacity { get; set; } = 4 * 1024;

        public int InitialCapacity { get; set; } = 100;

        public StringBuilder Get()
        {
            var item = this.firstItem;
            if (item == null || Interlocked.CompareExchange(ref this.firstItem, null, item) != item)
            {
                var items = this.items;
                for (var i = 0; i < items.Length; i++)
                {
                    item = items[i].Element;
                    if (item != null && Interlocked.CompareExchange(ref items[i].Element, null, item) == item)
                    {
                        return item;
                    }
                }

                item = new StringBuilder(this.InitialCapacity);
            }

            return item;
        }

        public bool Return(StringBuilder item)
        {
            if (item.Capacity > this.MaximumRetainedCapacity)
            {
                // Too big. Discard this one.
                return false;
            }

            item.Clear();

            if (this.firstItem != null || Interlocked.CompareExchange(ref this.firstItem, item, null) != null)
            {
                var items = this.items;
                for (var i = 0; i < items.Length && Interlocked.CompareExchange(ref items[i].Element, item, null) != null; ++i)
                {
                }
            }

            return true;
        }

        [DebuggerDisplay("{Element}")]
        private protected struct ObjectWrapper
        {
            public StringBuilder Element;
        }
    }
}
