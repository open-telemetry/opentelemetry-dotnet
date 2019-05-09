// <copyright file="ITraceComponent.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using OpenCensus.Common;
    using OpenCensus.Trace.Config;
    using OpenCensus.Trace.Export;
    using OpenCensus.Trace.Propagation;

    /// <summary>
    /// Trace component holds all the extensibility points required for distributed tracing.
    /// </summary>
    public interface ITraceComponent
    {
        /// <summary>
        /// Gets the tracer to record Spans.
        /// </summary>
        ITracer Tracer { get; }

        /// <summary>
        /// Gets the propagation component that defines how to extract and inject the context from the wire protocols.
        /// </summary>
        IPropagationComponent PropagationComponent { get; }

        /// <summary>
        /// Gets the exporter to use to upload spans.
        /// </summary>
        IExportComponent ExportComponent { get; }

        /// <summary>
        /// Gets the tracer configuration. Include sampling definition and limits.
        /// </summary>
        ITraceConfig TraceConfig { get; }
    }
}
