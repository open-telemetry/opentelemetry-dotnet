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

internal class MyFilteringProcessor : BaseProcessor<Activity>
{
    private readonly Func<Activity, bool> filter;
    private readonly BaseProcessor<Activity> processor;

    public MyFilteringProcessor(BaseProcessor<Activity> processor, Func<Activity, bool> filter)
    {
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public override void OnEnd(Activity activity)
    {
        // Call the underlying processor
        // only if the Filter returns true.
        if (this.filter(activity))
        {
            this.processor.OnEnd(activity);
        }
    }
}
