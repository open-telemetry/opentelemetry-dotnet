// <copyright file="ActivitySourceAdapter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// This class encapsulates the logic for performing ActivitySource actions
    /// on Activities that are created using default ActivitySource.
    /// All activities created without using ActivitySource will have a
    /// default ActivitySource assigned to them with their name as empty string.
    /// This class is to be used by instrumentation adapters which converts/augments
    /// activies created without ActivitySource, into something which closely
    /// matches the one created using ActivitySource.
    /// </summary>
    public class ActivitySourceAdapter
    {
        private readonly Sampler sampler;
        private readonly ActivityProcessor activityProcessor;
        private readonly Resource resource;

        internal ActivitySourceAdapter(Sampler sampler, ActivityProcessor activityProcessor, Resource resource)
        {
            this.sampler = sampler;
            this.activityProcessor = activityProcessor;
            this.resource = resource;
        }

        private ActivitySourceAdapter()
        {
        }

        /// <summary>
        /// Method that starts an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be started.</param>
        public void Start(Activity activity)
        {
            this.RunGetRequestedData(activity);
            if (activity.IsAllDataRequested)
            {
                activity.SetResource(this.resource);
                this.activityProcessor.OnStart(activity);
            }
        }

        /// <summary>
        /// Method that stops an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be stopped.</param>
        public void Stop(Activity activity)
        {
            if (activity.IsAllDataRequested)
            {
                this.activityProcessor.OnEnd(activity);
            }
        }

        private void RunGetRequestedData(Activity activity)
        {
            ActivityContext parentContext;
            if (string.IsNullOrEmpty(activity.ParentId))
            {
                parentContext = default(ActivityContext);
            }
            else
            {
                if (activity.Parent != null)
                {
                    parentContext = activity.Parent.Context;
                }
                else
                {
                    parentContext = new ActivityContext(
                        activity.TraceId,
                        activity.ParentSpanId,
                        activity.ActivityTraceFlags,
                        activity.TraceStateString,
                        isRemote: true);
                }
            }

            var samplingParameters = new SamplingParameters(
                parentContext,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.TagObjects,
                activity.Links);

            var samplingDecision = this.sampler.ShouldSample(samplingParameters);
            activity.IsAllDataRequested = samplingDecision.IsSampled;
            if (samplingDecision.IsSampled)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }
        }
    }
}
