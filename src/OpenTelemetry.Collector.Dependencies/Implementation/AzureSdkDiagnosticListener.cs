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
        // all fetchers must not be reused between DiagnosticSources.
        private readonly PropertyFetcher linksPropertyFetcher = new PropertyFetcher("Links");

        public AzureSdkDiagnosticListener(string sourceName, Tracer tracer)
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
            string operationName = null;
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

            if (operationName == null)
            {
                operationName = this.GetOperationName(current);
            }

            List<Link> parentLinks = null;
            if (this.linksPropertyFetcher.Fetch(valueValue) is IEnumerable<Activity> activityLinks)
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

            this.Tracer.StartSpanFromActivity(operationName, current, spanKind, parentLinks);
        }

        public override void OnStopActivity(Activity current, object valueValue)
        {
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                CollectorEventSource.Log.NullOrBlankSpan(this.SourceName + ".OnStopActivity");
                return;
            }

            if (span.IsRecording)
            {
                foreach (var keyValuePair in current.Tags)
                {
                    span.SetAttribute(keyValuePair.Key, keyValuePair.Value);
                }
            }

            span.End();
        }

        public override void OnException(Activity current, object valueValue)
        {
            var span = this.Tracer.CurrentSpan;

            if (span == null || !span.Context.IsValid)
            {
                CollectorEventSource.Log.NullOrBlankSpan(this.SourceName + ".OnException");
                return;
            }

            span.Status = Status.Unknown.WithDescription(valueValue?.ToString());
        }

        private string GetOperationName(Activity activity)
        {
            // activity name looks like 'Azure.<...>.<Class>.<Name>'
            // as namespace is too verbose, we'll just take the last two nodes from the activity name as telemetry name
            // this will change with https://github.com/Azure/azure-sdk-for-net/issues/9071 ~Feb 2020

            string activityName = activity.OperationName;
            int methodDotIndex = activityName.LastIndexOf('.');
            if (methodDotIndex <= 0)
            {
                return activityName;
            }

            int classDotIndex = activityName.LastIndexOf('.', methodDotIndex - 1);

            if (classDotIndex == -1)
            {
                return activityName;
            }

            return activityName.Substring(classDotIndex + 1, activityName.Length - classDotIndex - 1);
        }
    }
}
