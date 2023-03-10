// <copyright file="TailSamplingProcessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;

namespace SDKBasedSpanLevelTailSamplingSample;

/// <summary>
/// A custom processor for filtering <see cref="Activity"/> instances.
/// </summary>
internal sealed class TailSamplingProcessor : BaseProcessor<Activity>
{
    public TailSamplingProcessor()
        : base()
    {
    }

    public override void OnEnd(Activity activity)
    {
        if (activity.Recorded)
        {
            // This means that this activity was included based on head-based sampling,
            // we continue with that decision and no further change is needed.
            Console.WriteLine($"Including head-sampled activity with id {activity.Id} and status {activity.Status}");
        }
        else
        {
            this.IncludeForExportIfFailedActivity(activity);
        }

        base.OnEnd(activity);
    }

    // Note: This is used to filter spans at the end of a span.
    // This is a basic form of tail-based sampling at a span level.
    // If a span failed, we always sample it in addition to all head-sampled spans.
    // In this example, each span is filtered individually without consideration to any other spans.
    // Tail-sampling this way involves many tradeoffs. A few examples of the tradeoffs:
    // 1. Performance: Unlike head-based sampling where the sampling decision is made at span creation time, in
    //    tail sampling the decision is made only at the end, so there is additional memory cost.
    // 2. Traces will not be complete: Since this sampling is at a span level, the generated trace will be partial and won't be complete.
    //     For example, if another part of the call tree is successful, those spans may not be sampled in leading to a partial trace.
    // 3. If multiple exporters are used, this decision will impact all of them: https://github.com/open-telemetry/opentelemetry-dotnet/issues/3861.
    private void IncludeForExportIfFailedActivity(Activity activity)
    {
        if (activity.Status == ActivityStatusCode.Error)
        {
            // We decide to always include all the failure spans
            // Set the recorded flag so that this will be exported.
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            Console.WriteLine($"Including error activity with id {activity.Id} and status {activity.Status}");
        }
        else
        {
            // This span is not sampled and exporters won't see this span.
            Console.WriteLine($"Dropping activity with id {activity.Id} and status {activity.Status}");
        }
    }
}
