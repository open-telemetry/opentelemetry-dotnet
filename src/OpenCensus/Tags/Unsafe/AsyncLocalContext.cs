// <copyright file="AsyncLocalContext.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Tags.Unsafe
{
    using System.Collections.Generic;
    using System.Threading;

    internal static class AsyncLocalContext
    {
        private static readonly ITagContext EmptyTagContextInstance = new EmptyTagContext();

        private static AsyncLocal<ITagContext> context = new AsyncLocal<ITagContext>();

        public static ITagContext CurrentTagContext
        {
            get
            {
                if (context.Value == null)
                {
                    return EmptyTagContextInstance;
                }

                return context.Value;
            }

            set
            {
                if (value == EmptyTagContextInstance)
                {
                    context.Value = null;
                }
                else
                {
                    context.Value = value;
                }
            }
        }

        internal sealed class EmptyTagContext : TagContextBase
        {
            public override IEnumerator<ITag> GetEnumerator()
            {
                return new List<ITag>().GetEnumerator();
            }
        }
    }
}
