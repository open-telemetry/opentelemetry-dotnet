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
using global::OpenTracing.Propagation;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Shims.OpenTracing
{
    public class TracerShim : global::OpenTracing.ITracer
    {
        private readonly Trace.Tracer tracer;
        private readonly IPropagator propagator;

        public TracerShim(Trace.Tracer tracer, IPropagator textFormat)
        {
            this.tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            this.propagator = textFormat ?? throw new ArgumentNullException(nameof(textFormat));

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

            PropagationContext propagationContext = default;

            if ((format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders) && carrier is ITextMap textMapCarrier)
            {
                var carrierMap = new Dictionary<string, IEnumerable<string>>();

                foreach (var entry in textMapCarrier)
                {
                    carrierMap.Add(entry.Key, new[] { entry.Value });
                }

                static IEnumerable<string> GetCarrierKeyValue(Dictionary<string, IEnumerable<string>> source, string key)
                {
                    if (key == null || !source.TryGetValue(key, out var value))
                    {
                        return null;
                    }

                    return value;
                }

                propagationContext = this.propagator.Extract(propagationContext, carrierMap, GetCarrierKeyValue);
            }

            // TODO:
            //  Not sure what to do here. Really, Baggage should be returned and not set until this ISpanContext is turned into a live Span.
            //  But that code doesn't seem to exist.
            // Baggage.Current = propagationContext.Baggage;

            return !propagationContext.ActivityContext.IsValid() ? null : new SpanContextShim(new Trace.SpanContext(propagationContext.ActivityContext));
        }

        /// <inheritdoc/>
        public void Inject<TCarrier>(
            global::OpenTracing.ISpanContext spanContext,
            global::OpenTracing.Propagation.IFormat<TCarrier> format,
            TCarrier carrier)
        {
            if (spanContext is null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (!(spanContext is SpanContextShim shim))
            {
                throw new ArgumentException("context is not a valid SpanContextShim object");
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
                this.propagator.Inject(
                    new PropagationContext(shim.SpanContext, Baggage.Current),
                    textMapCarrier,
                    (instrumentation, key, value) => instrumentation.Set(key, value));
            }
        }
    }
}
