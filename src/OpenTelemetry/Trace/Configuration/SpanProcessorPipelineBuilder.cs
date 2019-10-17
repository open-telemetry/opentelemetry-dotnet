// <copyright file="SpanProcessorPipelineBuilder.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Configuration
{
    public class SpanProcessorPipelineBuilder
    {
        private Func<SpanExporter, SpanProcessor> lastProcessorFactory;
        private List<Func<SpanProcessor, SpanProcessor>> processorChain;

        internal SpanProcessorPipelineBuilder()
        {
        }

        internal SpanExporter Exporter { get; private set; }

        internal List<SpanProcessor> Processors { get; private set; }

        public SpanProcessorPipelineBuilder AddProcessor(Func<SpanProcessor, SpanProcessor> chain)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (this.processorChain == null)
            {
                this.processorChain = new List<Func<SpanProcessor, SpanProcessor>>();
            }

            this.processorChain.Add(chain);

            return this;
        }

        public SpanProcessorPipelineBuilder SetExportingProcessor(Func<SpanExporter, SpanProcessor> export)
        {
            this.lastProcessorFactory = export ?? throw new ArgumentNullException(nameof(export));
            return this;
        }

        public SpanProcessorPipelineBuilder SetExporter(SpanExporter exporter)
        {
            this.Exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            return this;
        }

        internal SpanProcessor Build()
        {
            this.Processors = new List<SpanProcessor>();
            SpanProcessor terminalProcessor = null;
            if (this.lastProcessorFactory != null)
            {
                terminalProcessor = this.lastProcessorFactory.Invoke(this.Exporter);
                this.Processors.Add(terminalProcessor);
            }
            else if (this.Exporter != null)
            {
                terminalProcessor = new BatchingSpanProcessor(this.Exporter);
                this.Processors.Add(terminalProcessor);
            }

            if (this.processorChain == null)
            {
                return terminalProcessor ?? new NoopSpanProcessor();
            }

            var next = terminalProcessor;

            for (int i = this.processorChain.Count - 1; i >= 0; i--)
            {
                next = this.processorChain[i].Invoke(next);
                this.Processors.Add(next);
            }

            return this.Processors[this.Processors.Count - 1];
        }
    }
}
