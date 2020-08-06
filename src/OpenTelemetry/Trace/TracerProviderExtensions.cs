// <copyright file="TracerProviderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Internal;

namespace OpenTelemetry.Trace
{
    public static class TracerProviderExtensions
    {
        public static TracerProvider AddProcessor(this TracerProvider provider, ActivityProcessor processor)
        {
            var trait = provider as TracerProviderSdk;

            // TODO: validate input
            // TODO: leverage CompositeActivityProcessor to handle multiple processors, in a thread safe (CoW) way
            if (trait.ActivityProcessor == null)
            {
                trait.ActivityProcessor = processor;
            }
            else if (trait.ActivityProcessor is FanOutActivityProcessor fanOutProcessor)
            {
                // TODO: add the processor to existing fan out processor
                // TODO: FanOutActivityProcessor shouldn't be under internal namespace
            }
            else
            {
                trait.ActivityProcessor = new FanOutActivityProcessor(new[]
                {
                    trait.ActivityProcessor,
                    processor,
                });
            }

            return trait;
        }
    }
}
