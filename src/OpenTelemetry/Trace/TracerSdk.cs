// <copyright file="TracerSdk.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace
{
    internal sealed class TracerSdk : Tracer
    {
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly Sampler sampler;

        static TracerSdk()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        /// <summary>
        /// Creates an instance of <see cref="TracerSdk"/>.
        /// </summary>
        /// <param name="spanProcessor">Span processor.</param>
        /// <param name="sampler">Sampler to use.</param>
        /// <param name="tracerConfiguration">Trace configuration.</param>
        /// <param name="libraryResource">Resource describing the instrumentation library.</param>
        internal TracerSdk(SpanProcessor spanProcessor, Sampler sampler, TracerConfiguration tracerConfiguration, Resource libraryResource)
        {
            this.spanProcessor = spanProcessor ?? throw new ArgumentNullException(nameof(spanProcessor));
            this.tracerConfiguration = tracerConfiguration ?? throw new ArgumentNullException(nameof(tracerConfiguration));
            this.LibraryResource = libraryResource ?? throw new ArgumentNullException(nameof(libraryResource));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        }

        public Resource LibraryResource { get; }

        /// <inheritdoc/>
        public override ISpan CurrentSpan => SpanSdk.Current;

        public override IDisposable WithSpan(ISpan span, bool endSpanOnDispose)
        {
            if (span == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("WithSpan", nameof(span), "is null");
            }

            if (span is SpanSdk spanImpl)
            {
                return spanImpl.BeginScope(endSpanOnDispose);
            }

            return NoopDisposable.Instance;
        }

        /// <inheritdoc/>
        public override ISpan StartRootSpan(string operationName, SpanKind kind, SpanCreationOptions options)
        {
            return SpanSdk.CreateRoot(operationName, kind, options, this.sampler, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public override ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, SpanCreationOptions options)
        {
            if (parent == null)
            {
                parent = this.CurrentSpan;
            }

            return SpanSdk.CreateFromParentSpan(operationName, parent, kind, options, this.sampler, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public override ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, SpanCreationOptions options)
        {
            if (parent.IsValid)
            {
                return SpanSdk.CreateFromParentContext(operationName, parent, kind, options, this.sampler, this.tracerConfiguration,
                    this.spanProcessor, this.LibraryResource);
            }

            return SpanSdk.CreateRoot(operationName, kind, options, this.sampler, this.tracerConfiguration,
                this.spanProcessor, this.LibraryResource);
        }

        /// <inheritdoc/>
        public override ISpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            bool isValidActivity = true;
            if (activity == null)
            {
                isValidActivity = false;
                OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is null");
            }
            else
            {
                if (activity.IdFormat != ActivityIdFormat.W3C)
                {
                    isValidActivity = false;
                    OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is not in W3C Trace-Context format");
                }

                if (activity.StartTimeUtc == default)
                {
                    isValidActivity = false;
                    OpenTelemetrySdkEventSource.Log.InvalidArgument("StartSpanFromActivity", nameof(activity), "is not started");
                }
            }

            if (!isValidActivity)
            {
                return this.StartSpan(operationName, kind, links != null ? new SpanCreationOptions { Links = links } : null);
            }

            return SpanSdk.CreateFromActivity(operationName, activity, kind, links, this.sampler, this.tracerConfiguration, this.spanProcessor, this.LibraryResource);
        }
    }
}
