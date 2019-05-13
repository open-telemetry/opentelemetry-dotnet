﻿// <copyright file="TraceConfigTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Config.Test
{
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class TraceConfigTest
    {
        private readonly TraceConfig traceConfig = new TraceConfig();

        [Fact]
        public void DefaultActiveTraceParams()
        {
            Assert.Equal(TraceParams.Default, traceConfig.ActiveTraceParams);
        }

        [Fact]
        public void UpdateTraceParams()
        {
            TraceParams traceParams =
                TraceParams.Default
                    .ToBuilder()
                    .SetSampler(Samplers.AlwaysSample)
                    .SetMaxNumberOfAttributes(8)
                    .SetMaxNumberOfAnnotations(9)
                    // .SetMaxNumberOfNetworkEvents(10)
                    .SetMaxNumberOfLinks(11)
                    .Build();
            traceConfig.UpdateActiveTraceParams(traceParams);
            Assert.Equal(traceParams, traceConfig.ActiveTraceParams);
            traceConfig.UpdateActiveTraceParams(TraceParams.Default);
            Assert.Equal(TraceParams.Default, traceConfig.ActiveTraceParams);
        }
    }
}
