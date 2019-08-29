// <copyright file="AzureSdkDiagnosticListener.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.Dependencies
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using OpenTelemetry.Trace;

    internal class AzureSdkDiagnosticListener : ListenerHandler<HttpRequestMessage>
    {
        private readonly ITracer tracer;

        private readonly ISampler sampler;

        public AzureSdkDiagnosticListener(string sourceName, ITracer tracer, ISampler sampler)
            : base(sourceName, tracer, null)
        {
            this.tracer = tracer;
            this.sampler = sampler;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public override void OnStartActivity(Activity current, object valueValue)
        {
            bool isHttp = false;
            var operationName = current.OperationName;
            foreach (var keyValuePair in current.Tags)
            {
                if (keyValuePair.Key == "http.url")
                {
                    isHttp = true;
                    operationName = keyValuePair.Value;
                    break;
                }
            }

            var spanBuilder = this.tracer.SpanBuilder(operationName)
                .SetCreateChild(false)
                .SetSampler(this.sampler);

            spanBuilder.SetSpanKind(isHttp ? SpanKind.Client : SpanKind.Internal);

            var span = spanBuilder.StartSpan();

            span.Status = Status.Ok;

            this.tracer.WithSpan(span);
        }

        public override void OnStopActivity(Activity current, object valueValue)
        {
            var span = this.tracer.CurrentSpan;
            foreach (var keyValuePair in current.Tags)
            {
                span.SetAttribute(keyValuePair.Key, keyValuePair.Value);
            }

            this.tracer.CurrentSpan.End();
        }

        public override void OnException(Activity current, object valueValue)
        {
            var span = this.tracer.CurrentSpan;

            span.Status = Status.Unknown.WithDescription(valueValue?.ToString());
        }
    }
}
