// <copyright file="CurrentTagContextUtils.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Tags
{
    using OpenCensus.Common;
    using OpenCensus.Tags.Unsafe;

    internal static class CurrentTagContextUtils
    {
        internal static ITagContext CurrentTagContext
        {
            get { return AsyncLocalContext.CurrentTagContext; }
        }

        internal static IScope WithTagContext(ITagContext tags)
        {
            return new WithTagContextScope(tags);
        }

        private sealed class WithTagContextScope : IScope
        {
            private readonly ITagContext origContext;

            public WithTagContextScope(ITagContext tags)
            {
                this.origContext = AsyncLocalContext.CurrentTagContext;
                AsyncLocalContext.CurrentTagContext = tags;
            }

            public void Dispose()
            {
                var current = AsyncLocalContext.CurrentTagContext;
                AsyncLocalContext.CurrentTagContext = this.origContext;

                if (current != this.origContext)
                {
                    // Log
                }
            }
        }
    }
}
