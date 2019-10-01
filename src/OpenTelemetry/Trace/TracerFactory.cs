// <copyright file="TracerFactory.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
   
    /// <inheritdoc/>
    public sealed class TracerFactory : ITracerFactory
    {
        private readonly object lck = new object();
        private readonly SpanProcessor spanProcessor;
        private readonly TraceConfig traceConfig;
        private readonly ITextFormat textFormat;
        private readonly IBinaryFormat binaryFormat;
        private readonly Tracer defaultTracer;
        private readonly Dictionary<TracerRegistryKey, ITracer> tracerRegistry = new Dictionary<TracerRegistryKey, ITracer>();

        public TracerFactory(SpanProcessor spanProcessor = null, TraceConfig traceConfig = null, ITextFormat textFormat = null, IBinaryFormat binaryFormat = null)
        {
            this.spanProcessor = spanProcessor ?? Tracing.SpanProcessor;
            this.traceConfig = traceConfig ?? Tracing.TraceConfig;
            this.textFormat = textFormat ?? new TraceContextFormat();
            this.binaryFormat = binaryFormat ?? new BinaryFormat();
            this.defaultTracer = new Tracer(this.spanProcessor, this.traceConfig, this.binaryFormat, this.textFormat, Resource.Empty);
        }

        /// <inheritdoc/>
        public override ITracer GetTracer(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultTracer;
            }
            
            lock (this.lck)
            {
                var key = new TracerRegistryKey(name, version);
                if (!this.tracerRegistry.TryGetValue(key, out var tracer))
                {
                    var labels = CreateLibraryResourceLabels(name, version);
                    tracer = new Tracer(this.spanProcessor, this.traceConfig, this.binaryFormat, this.textFormat, Resource.Create(labels));
                    this.tracerRegistry.Add(key, tracer);
                }
                
                return tracer;
            }
        }

        private static Dictionary<string, string> CreateLibraryResourceLabels(string name, string version)
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
        }  
    }
}
