// <copyright file="CurrentTaggingState.cs" company="OpenCensus Authors">
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
    using System;

    public sealed class CurrentTaggingState
    {
        private readonly object lck = new object();
        private TaggingState currentState = TaggingState.ENABLED;
        private bool isRead;

        public TaggingState Value
        {
            get
            {
                lock (this.lck)
                {
                    this.isRead = true;
                    return this.Internal;
                }
            }
        }

        public TaggingState Internal
        {
            get
            {
                lock (this.lck)
                {
                    return this.currentState;
                }
            }
        }

        // Sets current state to the given state.
        internal void Set(TaggingState state)
        {
            lock (this.lck)
            {
                if (this.isRead)
                {
                    throw new InvalidOperationException("State was already read, cannot set state.");
                }

                this.currentState = state;
            }
        }
    }
}
