﻿// <copyright file="TagContextBuilder.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;

namespace OpenTelemetry.Tags
{
    internal sealed class TagContextBuilder : TagContextBuilderBase
    {
        internal TagContextBuilder(IDictionary<TagKey, TagValue> tags)
        {
            this.Tags = new Dictionary<TagKey, TagValue>(tags);
        }

        internal TagContextBuilder()
        {
            this.Tags = new Dictionary<TagKey, TagValue>();
        }

        internal IDictionary<TagKey, TagValue> Tags { get; }

        public override ITagContextBuilder Put(TagKey key, TagValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.Tags[key] = value ?? throw new ArgumentNullException(nameof(value));
            return this;
        }

        public override ITagContextBuilder Remove(TagKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (this.Tags.ContainsKey(key))
            {
                this.Tags.Remove(key);
            }

            return this;
        }

        public override ITagContext Build()
        {
            return new TagContext(this.Tags);
        }

        public override IDisposable BuildScoped()
        {
            return CurrentTagContextUtils.WithTagContext(this.Build());
        }
    }
}
