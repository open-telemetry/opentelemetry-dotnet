﻿// <copyright file="AsyncLocalContext.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;

namespace OpenTelemetry.Tags.Unsafe
{
    internal static class AsyncLocalContext
    {
        private static readonly ITagContext EmptyTagContextInstance = new EmptyTagContext();

        private static readonly AsyncLocal<ITagContext> Context = new AsyncLocal<ITagContext>();

        public static ITagContext CurrentTagContext
        {
            get
            {
                if (Context.Value == null)
                {
                    return EmptyTagContextInstance;
                }

                return Context.Value;
            }

            set
            {
                if (value == EmptyTagContextInstance)
                {
                    Context.Value = null;
                }
                else
                {
                    Context.Value = value;
                }
            }
        }

        internal sealed class EmptyTagContext : TagContextBase
        {
            public override IEnumerator<Tag> GetEnumerator()
            {
                return new List<Tag>().GetEnumerator();
            }
        }
    }
}
