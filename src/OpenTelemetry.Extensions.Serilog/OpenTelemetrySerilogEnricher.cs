// <copyright file="OpenTelemetrySerilogEnricher.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace OpenTelemetry.Logs;

internal sealed class OpenTelemetrySerilogEnricher : ILogEventEnricher
{
    private readonly OpenTelemetrySerilogEnricherOptions options;

    public OpenTelemetrySerilogEnricher(OpenTelemetrySerilogEnricherOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void Enrich(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory)
    {
        Debug.Assert(logEvent != null, "LogEvent was null.");
        Debug.Assert(propertyFactory != null, "PropertyFactory was null.");

        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        logEvent!.AddPropertyIfAbsent(propertyFactory!.CreateProperty(nameof(Activity.SpanId), activity.SpanId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.TraceId), activity.TraceId.ToHexString()));

        if (activity.ParentSpanId != default)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nameof(Activity.ParentSpanId), activity.ParentSpanId.ToHexString()));
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceFlags", activity.ActivityTraceFlags));

        if (this.options.IncludeTraceState)
        {
            var traceState = activity.TraceStateString;
            if (!string.IsNullOrEmpty(traceState))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceState", traceState));
            }
        }
    }
}

