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
    private Func<Activity, bool> filter;
    private BaseExportProcessor<Activity> exportProcessor;

    public MyFilteringProcessor(BaseExportProcessor<Activity> exportProcessor, Func<Activity, bool> filter)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (exportProcessor == null)
        {
            throw new ArgumentNullException(nameof(exportProcessor));
        }

        this.filter = filter;
        this.exportProcessor = exportProcessor;
    }

    public override void OnEnd(Activity activity)
    {
        // Call the exporting processor
        // only if the Filter returns true.
        if (this.filter(activity))
        {
            this.exportProcessor.OnEnd(activity);
        }
    }
}
