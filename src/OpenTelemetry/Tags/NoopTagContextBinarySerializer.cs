﻿// <copyright file="NoopTagContextBinarySerializer.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tags.Propagation;

namespace OpenTelemetry.Tags
{
    public class NoopTagContextBinarySerializer : TagContextBinarySerializerBase
    {
        internal static readonly ITagContextBinarySerializer Instance = new NoopTagContextBinarySerializer();
        private static readonly byte[] EmptyByteArray = { };

        public override byte[] ToByteArray(ITagContext tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }

            return EmptyByteArray;
        }

        public override ITagContext FromByteArray(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            return NoopTags.NoopTagContext;
        }
    }
}
