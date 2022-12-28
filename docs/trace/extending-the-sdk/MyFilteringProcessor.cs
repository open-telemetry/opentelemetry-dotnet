// <copyright file="MyFilteringProcessor.cs" company="OpenTelemetry Authors">
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

/// <summary>
/// A custom processor for filtering <see cref="Activity"/> instances.
/// </summary>
internal sealed class MyFilteringProcessor : BaseProcessor<Activity>
{
    private readonly Func<Activity, bool> filter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyFilteringProcessor"/>
    /// class.
    /// </summary>
    /// <param name="filter">Function used to test if an <see cref="Activity"/>
    /// should be recorded or dropped. Return <see langword="true"/> to record
    /// or <see langword="false"/> to drop.</param>
    public MyFilteringProcessor(Func<Activity, bool> filter)
    {
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public override void OnEnd(Activity activity)
    {
        // Bypass export if the Filter returns false.
        if (!this.filter(activity))
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
