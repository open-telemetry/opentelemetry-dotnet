// <copyright file="TraceParamsBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Config
{
    using System;

    /// <summary>
    /// Trace parameters builder.
    /// </summary>
    public sealed class TraceParamsBuilder
    {
        private ISampler sampler;
        private int? maxNumberOfAttributes;
        private int? maxNumberOfEvents;
        private int? maxNumberOfMessageEvents;
        private int? maxNumberOfLinks;

        internal TraceParamsBuilder(TraceParams source)
        {
            this.sampler = source.Sampler;
            this.maxNumberOfAttributes = source.MaxNumberOfAttributes;
            this.maxNumberOfEvents = source.MaxNumberOfEvents;
            this.maxNumberOfMessageEvents = source.MaxNumberOfMessageEvents;
            this.maxNumberOfLinks = source.MaxNumberOfLinks;
        }

        /// <summary>
        /// Sets sempler to use.
        /// </summary>
        /// <param name="sampler">Sampler to use.</param>
        /// <returns>Builder to chain operations.</returns>
        public TraceParamsBuilder SetSampler(ISampler sampler)
        {
            this.sampler = sampler ?? throw new ArgumentNullException("Null sampler");
            return this;
        }

        /// <summary>
        /// Sets the maximum number of attributes.
        /// </summary>
        /// <param name="maxNumberOfAttributes">Maximum number of attributes per span.</param>
        /// <returns>Builder to chain operations.</returns>
        public TraceParamsBuilder SetMaxNumberOfAttributes(int maxNumberOfAttributes)
        {
            this.maxNumberOfAttributes = maxNumberOfAttributes;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of events.
        /// </summary>
        /// <param name="maxNumberOfEvents">Maximum number of events per span.</param>
        /// <returns>Builder to chain operations.</returns>
        public TraceParamsBuilder SetMaxNumberOfEvents(int maxNumberOfEvents)
        {
            this.maxNumberOfEvents = maxNumberOfEvents;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of message events on span.
        /// </summary>
        /// <param name="maxNumberOfMessageEvents">Maximum number of message events per span.</param>
        /// <returns>Builder to chain operations.</returns>
        public TraceParamsBuilder SetMaxNumberOfMessageEvents(int maxNumberOfMessageEvents)
        {
            this.maxNumberOfMessageEvents = maxNumberOfMessageEvents;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of links on span.
        /// </summary>
        /// <param name="maxNumberOfLinks">Maximum number of links on span.</param>
        /// <returns>Builder to chain operations.</returns>
        public TraceParamsBuilder SetMaxNumberOfLinks(int maxNumberOfLinks)
        {
            this.maxNumberOfLinks = maxNumberOfLinks;
            return this;
        }

        /// <summary>
        /// Builds trace parameters from provided arguments.
        /// </summary>
        /// <returns>Builder to chain operations.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If maximum values are not set or set to less than one.</exception>
        public TraceParams Build()
        {
            var missing = string.Empty;
            if (this.sampler == null)
            {
                missing += " sampler";
            }

            if (!this.maxNumberOfAttributes.HasValue)
            {
                missing += " maxNumberOfAttributes";
            }

            if (!this.maxNumberOfEvents.HasValue)
            {
                missing += " maxNumberOfAnnotations";
            }

            if (!this.maxNumberOfMessageEvents.HasValue)
            {
                missing += " maxNumberOfMessageEvents";
            }

            if (!this.maxNumberOfLinks.HasValue)
            {
                missing += " maxNumberOfLinks";
            }

            if (!string.IsNullOrEmpty(missing))
            {
                throw new ArgumentOutOfRangeException(nameof(missing), $"Missing required properties: {missing}.");
            }

            return new TraceParams(
                this.sampler,
                this.maxNumberOfAttributes.Value,
                this.maxNumberOfEvents.Value,
                this.maxNumberOfMessageEvents.Value,
                this.maxNumberOfLinks.Value);
        }
    }
}
