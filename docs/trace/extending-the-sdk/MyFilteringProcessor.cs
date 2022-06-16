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

using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// A custom processor for filtering <see cref="Activity"/> instances.
/// </summary>
/// <remarks>
/// Note: <see cref="CompositeProcessor{T}"/> is used as the base class because
/// the SDK needs to understand that <c>MyFilteringProcessor</c> wraps an inner
/// processor. Without that understanding some features such as <see
/// cref="Resource"/> would be unavailable because the SDK needs to push state
/// about the parent <see cref="TracerProvider"/> to all processors in the
/// chain.
/// </remarks>
internal sealed class MyFilteringProcessor : CompositeProcessor<Activity>
{
    private readonly Func<Activity, bool> filter;

    public MyFilteringProcessor(BaseProcessor<Activity> processor, Func<Activity, bool> filter)
        : base(new[] { processor })
    {
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public override void OnEnd(Activity activity)
    {
        // Call the underlying processor
        // only if the Filter returns true.
        if (this.filter(activity))
        {
            base.OnEnd(activity);
        }
    }
}
