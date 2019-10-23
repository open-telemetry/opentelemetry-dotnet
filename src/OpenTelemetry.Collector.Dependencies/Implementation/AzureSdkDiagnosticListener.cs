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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Collector.Dependencies.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Collector.Dependencies
{
    internal class AzureSdkDiagnosticListener : ListenerHandler
    {
        private static readonly PropertyFetcher LinksPropertyFetcher = new PropertyFetcher("Links");

        public AzureSdkDiagnosticListener(string sourceName, ITracer tracer)
            : base(sourceName, tracer)
        {
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public override void OnStartActivity(Activity current, object valueValue)
        {
            var operationName = current.OperationName;
            var spanKind = SpanKind.Internal;

            foreach (var keyValuePair in current.Tags)
            {
                if (keyValuePair.Key == "http.url")
                {
                    operationName = keyValuePair.Value;
                    spanKind = SpanKind.Client;
                    break;
                }

                if (keyValuePair.Key == "kind")
                {
                    if (Enum.TryParse(keyValuePair.Value, true, out SpanKind parsedSpanKind))
                    {
                        spanKind = parsedSpanKind;
                    }
                }
            }

            List<Link> parentLinks = null;
            if (LinksPropertyFetcher.Fetch(valueValue) is IEnumerable<Activity> activityLinks)
            {
                if (activityLinks.Any())
                {
                    parentLinks = new List<Link>();
                    foreach (var link in activityLinks)
                    {
                        if (link != null)
                        {
                            parentLinks.Add(new Link(new SpanContext(link.TraceId, link.ParentSpanId, link.ActivityTraceFlags)));
                        }
                    }
                }
            }

            var span = this.Tracer.StartSpanFromActivity(operationName, current, spanKind, parentLinks);

            this.Tracer.WithSpan(span);
        }

        public override void OnStopActivity(Activity current, object valueValue)
        {
            var span = this.Tracer.CurrentSpan;
            foreach (var keyValuePair in current.Tags)
            {
                span.SetAttribute(keyValuePair.Key, keyValuePair.Value);
            }

            this.Tracer.CurrentSpan.End();
        }

        public override void OnException(Activity current, object valueValue)
        {
            var span = this.Tracer.CurrentSpan;

            span.Status = Status.Unknown.WithDescription(valueValue?.ToString());
        }
    }
}
