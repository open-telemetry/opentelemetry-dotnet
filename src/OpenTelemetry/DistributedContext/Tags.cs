// <copyright file="Tags.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Context
{
    public sealed class Tags
    {
        private static readonly object Lock = new object();

        private static Tags tags;

        private readonly ITagger tagger;
        private readonly ITagContextBinarySerializer tagContextBinarySerializer;

        internal Tags()
        {
            this.tagger = new Tagger();
            this.tagContextBinarySerializer = new TagContextBinarySerializer();
        }

        public static ITagger Tagger
        {
            get
            {
                Initialize();
                return tags.tagger;
            }
        }

        public static ITagContextBinarySerializer BinarySerializer
        {
            get
            {
                Initialize();
                return tags.tagContextBinarySerializer;
            }
        }

        internal static void Initialize()
        {
            if (tags == null)
            {
                lock (Lock)
                {
                    tags = tags ?? new Tags();
                }
            }
        }
    }
}
