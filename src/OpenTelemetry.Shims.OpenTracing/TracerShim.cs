// <copyright file="TracerShim.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Shims.OpenTracing
{
    using System;
    using System.Collections.Generic;
    using global::OpenTracing.Propagation;
    using OpenTelemetry.Trace;

    public class TracerShim : global::OpenTracing.ITracer
    {
        private readonly Trace.ITracer tracer;

        private TracerShim(Trace.ITracer tracer)
        {
            this.tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            this.ScopeManager = new ScopeManagerShim(this.tracer);
        }

        public global::OpenTracing.IScopeManager ScopeManager { get; private set; }

        public global::OpenTracing.ISpan ActiveSpan => this.ScopeManager.Active?.Span;

        public static global::OpenTracing.ITracer Create(Trace.ITracer tracer)
        {
            return new TracerShim(tracer);
        }

        public global::OpenTracing.ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilderShim(this.tracer, operationName);
        }

        public global::OpenTracing.ISpanContext Extract<TCarrier>(global::OpenTracing.Propagation.IFormat<TCarrier> format, TCarrier carrier)
        {
            if (format is null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (carrier == default)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            Trace.SpanContext spanContext = null;

            // TODO Add binary support post OpenTracing vNext.
            if (format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders)
            {
                // We know carrier is of Type ITextMap because we made it in here.
                var carrierMap = new Dictionary<string, IEnumerable<string>>();

                foreach (var entry in (ITextMap)carrier)
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

                spanContext = this.tracer.TextFormat?.Extract(carrierMap, GetCarrierKeyValue);
            }

            return (spanContext == null || spanContext == SpanContext.Blank) ? null : new SpanContextShim(spanContext);
        }

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

            if (carrier == default)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            // TODO Add binary support post OpenTracing vNext.
            if (format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders)
            {
                // We know carrier is of Type ITextMap because we made it in here.
                this.tracer.TextFormat?.Inject(shim.SpanContext, (ITextMap)carrier, (adapter, key, value) => adapter.Set(key, value));
            }
        }
    }
}
