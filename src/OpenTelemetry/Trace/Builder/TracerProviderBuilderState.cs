// <copyright file="TracerProviderBuilderState.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Stores state used to build a <see cref="TracerProvider"/>.
    /// </summary>
    internal sealed class TracerProviderBuilderState : ProviderBuilderState<TracerProviderBuilderSdk, TracerProviderSdk>
    {
        private TracerProviderBuilderSdk? builder;

        public TracerProviderBuilderState(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public override TracerProviderBuilderSdk Builder
            => this.builder ??= new TracerProviderBuilderSdk(this);

        public List<BaseProcessor<Activity>> Processors { get; } = new();

        public List<string> Sources { get; } = new();

        public HashSet<string> LegacyActivityOperationNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Sampler? Sampler { get; private set; }

        public bool SetErrorStatusOnException { get; set; }

        public void AddLegacySource(string operationName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(operationName), "operationName was null or whitespace");

            this.LegacyActivityOperationNames.Add(operationName);
        }

        public void AddProcessor(BaseProcessor<Activity> processor)
        {
            Debug.Assert(processor != null, "processor was null");

            this.Processors.Add(processor!);
        }

        public void AddSource(params string[] names)
        {
            Debug.Assert(names != null, "names was null");

            foreach (var name in names!)
            {
                Guard.ThrowIfNullOrWhitespace(name);

                // TODO: We need to fix the listening model.
                // Today it ignores version.
                this.Sources.Add(name);
            }
        }

        public void SetSampler(Sampler sampler)
        {
            Debug.Assert(sampler != null, "sampler was null");

            this.Sampler = sampler;
        }

        internal void EnableErrorStatusOnException()
        {
            try
            {
                this.Processors.Insert(0, new ExceptionProcessor());
            }
            catch (Exception ex)
            {
                throw new NotSupportedException($"'{nameof(TracerProviderBuilderExtensions.SetErrorStatusOnException)}' is not supported on this platform", ex);
            }
        }
    }
}
