// <copyright file="ActivitySourceFake.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace
{
    public class ActivitySourceFake
    {
        internal ActivitySampler Sampler { get; set; }

        internal ActivityProcessor ActivityProcessor { get; set; }

        public void Start(Activity activity)
        {
            this.ActivityProcessor.OnStart(activity);
        }

        public void Stop(Activity activity)
        {
            this.ActivityProcessor.OnEnd(activity);
        }

        public void RunGetRequestedData(Activity activity)
        {
            var samplingParameters = new ActivitySamplingParameters(
                activity.Context,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.Tags,
                activity.Links);

            var samplingDecision = this.Sampler.ShouldSample(samplingParameters);
            activity.IsAllDataRequested = samplingDecision.IsSampled;
            if (samplingDecision.IsSampled)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }
        }
    }
}
