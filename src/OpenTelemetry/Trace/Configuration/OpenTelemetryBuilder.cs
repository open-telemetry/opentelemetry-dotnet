﻿// <copyright file="OpenTelemetryBuilder.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Build OpenTelemetry pipeline.
    /// Currently supports a single processing pipeline, and any number of activity source names
    /// to subscribe to.
    /// </summary>
    public class OpenTelemetryBuilder
    {
        internal OpenTelemetryBuilder()
        {
        }

        internal Resource Resource { get; private set; } = Resource.Empty;

        internal ActivityProcessorPipelineBuilder ProcessingPipeline { get; private set; }

        internal HashSet<string> ActivitySourceNames { get; private set; }

        /// <summary>
        /// Sets the <see cref="Resource"/> describing the app associated with all traces. Overwrites currently set resource.
        /// </summary>
        /// <param name="resource">Resource to be associate with all traces.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder SetResource(Resource resource)
        {
            this.Resource = resource ?? Resource.Empty;
            return this;
        }

        /// <summary>
        /// Sets processing and exporting pipeline.
        /// </summary>
        /// <param name="configure">Function that configures pipeline.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder SetProcessorPipeline(Action<ActivityProcessorPipelineBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var pipelineBuilder = new ActivityProcessorPipelineBuilder();
            configure(pipelineBuilder);
            this.ProcessingPipeline = pipelineBuilder;
            return this;
        }

        /// <summary>
        /// Adds given activitysource name to the list of subscribed sources.
        /// </summary>
        /// <param name="activitySourceName">Activity source name.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder AddActivitySource(string activitySourceName)
        {
            if (this.ActivitySourceNames == null)
            {
                this.ActivitySourceNames = new HashSet<string>();
            }

            this.ActivitySourceNames.Add(activitySourceName.ToLowerInvariant());
            return this;
        }
    }
}
