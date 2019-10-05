// <copyright file="TracerFactorySdk.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Configuration;
    using OpenTelemetry.Trace.Export;
   
    /// <inheritdoc/>
    public sealed class TracerFactorySdk : TracerFactory
    {
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfigurationOptions tracerConfigurationOptions;
        private readonly ITextFormat textFormat;
        private readonly IBinaryFormat binaryFormat;
        private readonly Tracer defaultTracer;
        private readonly ConcurrentDictionary<TracerRegistryKey, ITracer> tracerRegistry = new ConcurrentDictionary<TracerRegistryKey, ITracer>();

        public TracerFactorySdk(SpanProcessor spanProcessor = null, TracerConfigurationOptions tracerConfigurationOptions = null, ITextFormat textFormat = null, IBinaryFormat binaryFormat = null)
        {
            this.spanProcessor = spanProcessor ?? Tracing.SpanProcessor;
            this.tracerConfigurationOptions = tracerConfigurationOptions ?? Tracing.TracerConfigurationOptions;
            this.textFormat = textFormat ?? new TraceContextFormat();
            this.binaryFormat = binaryFormat ?? new BinaryFormat();
            this.defaultTracer = new Tracer(this.spanProcessor, this.tracerConfigurationOptions, this.binaryFormat, this.textFormat, Resource.Empty);
        }

        /// <inheritdoc/>
        public override ITracer GetTracer(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultTracer;
            }
            
            var key = new TracerRegistryKey(name, version);
            return this.tracerRegistry.GetOrAdd(
                key, 
                k => new Tracer(this.spanProcessor, this.tracerConfigurationOptions, this.binaryFormat, this.textFormat, key.CreateResource()));
        }
        
        private static IEnumerable<KeyValuePair<string, string>> CreateLibraryResourceLabels(string name, string version)
        {
            var labels = new Dictionary<string, string> { { "name", name } };
            if (!string.IsNullOrEmpty(version))
            {
                labels.Add("version", version);
            }
            
            return labels;
        }
        
        private struct TracerRegistryKey
        {
            private string name;
            private string version;

            internal TracerRegistryKey(string name, string version)
            {
                this.name = name;
                this.version = version;
            }

            internal Resource CreateResource()
            {
                return new Resource(CreateLibraryResourceLabels(this.name, this.version));
            }
        }
    }
}
