﻿// <copyright file="NoopTags.cs" company="OpenTelemetry Authors">
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
    internal sealed class NoopTags
    {
        internal static ITagger NoopTagger
        {
            get
            {
                return OpenTelemetry.Context.NoopTagger.Instance;
            }
        }

        internal static ITagContextBuilder NoopTagContextBuilder
        {
            get
            {
                return OpenTelemetry.Context.NoopTagContextBuilder.Instance;
            }
        }

        internal static ITagContext NoopTagContext
        {
            get
            {
                return OpenTelemetry.Context.NoopTagContext.Instance;
            }
        }

        internal static ITagContextBinarySerializer NoopTagContextBinarySerializer
        {
            get
            {
                return OpenTelemetry.Context.NoopTagContextBinarySerializer.Instance;
            }
        }
    }
}
