// <copyright file="TracerShim.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using global::OpenTracing.Propagation;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing
{
    public class TracerShim : global::OpenTracing.ITracer
    {
        private readonly Trace.TracerNew tracer;
        private readonly ActivitySource activitySource;
        private readonly ITextFormatActivity textFormat;

        public TracerShim(Trace.TracerNew tracer, ITextFormatActivity textFormat)
        {
            this.tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            this.activitySource = tracer.activitySource;
            this.textFormat = textFormat ?? throw new ArgumentNullException(nameof(textFormat));
            this.ScopeManager = new ScopeManagerShim(this.tracer);
        }

        /// <inheritdoc/>
        public global::OpenTracing.IScopeManager ScopeManager { get; private set; }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan ActiveSpan => this.ScopeManager.Active?.Span;

        /// <inheritdoc/>
        public global::OpenTracing.ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilderShim(this.tracer, operationName);
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpanContext Extract<TCarrier>(global::OpenTracing.Propagation.IFormat<TCarrier> format, TCarrier carrier)
        {
            if (format is null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            SpanContextNew spanContext = default;

            if ((format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders) && carrier is ITextMap textMapCarrier)
            {
                var carrierMap = new Dictionary<string, IEnumerable<string>>();

                foreach (var entry in textMapCarrier)
                {
                    carrierMap.Add(entry.Key, new[] { entry.Value });
                }

                IEnumerable<string> GetCarrierKeyValue(Dictionary<string, IEnumerable<string>> source, string key)
                {
                    if (key == null || !source.TryGetValue(key, out var value))
                    {
                        return null;
                    }

                    return value;
                }

                spanContext = new SpanContextNew(this.textFormat.Extract(carrierMap, GetCarrierKeyValue));
            }

            return !spanContext.IsValid ? null : new SpanContextShim(spanContext);
        }

        /// <inheritdoc/>
        public void Inject<TCarrier>(
            global::OpenTracing.ISpanContext activityContext,
            global::OpenTracing.Propagation.IFormat<TCarrier> format,
            TCarrier carrier)
        {
            if (activityContext is null)
            {
                throw new ArgumentNullException(nameof(activityContext));
            }

            if (!(activityContext is SpanContextShim shim))
            {
                throw new ArgumentException("context is not a valid ActivityContextShim object");
            }

            if (format is null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            if ((format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders) && carrier is ITextMap textMapCarrier)
            {
                this.textFormat.Inject(shim.Context.ActivityContext, textMapCarrier, (instrumentation, key, value) => instrumentation.Set(key, value));
            }
        }
    }
}
