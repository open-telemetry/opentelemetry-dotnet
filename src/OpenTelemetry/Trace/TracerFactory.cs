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
        private readonly Dictionary<string, ITracer> tracerRegistry = new Dictionary<string, ITracer>();

        public TracerFactory(SpanProcessor spanProcessor = null, TraceConfig traceConfig = null)
        {
            this.spanProcessor = spanProcessor ?? Tracing.SpanProcessor;
            this.traceConfig = traceConfig ?? Tracing.TraceConfig;
        }

        internal ITextFormat TextFormat { get; set; }

        /// <inheritdoc/>
        public ITracer GetTracer(string name, string version = null)
        {
            var labels = new Dictionary<string, string>();
            var key = string.Empty;
            if (!string.IsNullOrEmpty(name))
            {
                labels.Add("name", name);
                if (!string.IsNullOrEmpty(version))
                {
                    labels.Add("version", version);
                }
                
                key = $"{name}-{version}";
            }

            ITracer tracer;
            lock (this.lck)
            {
                if (!this.tracerRegistry.ContainsKey(key))
                {
                    this.tracerRegistry[key] = new Tracer(this.spanProcessor, Tracing.TraceConfig, null, this.TextFormat, Resource.Create(labels));
                }

                tracer = this.tracerRegistry[key];
            }

            return tracer;
        }
    }
}
