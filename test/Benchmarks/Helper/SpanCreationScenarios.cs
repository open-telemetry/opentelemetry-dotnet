// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Benchmarks.Helper;

internal static class SpanCreationScenarios
{
    public static void CreateSpan(Tracer tracer)
    {
        using var span = tracer.StartSpan("span");
        span.End();
    }

    public static void CreateSpan_ParentContext(Tracer tracer)
    {
        var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, true);
        using var span = tracer.StartSpan("span", SpanKind.Client, parentContext);
        span.End();
    }

    public static void CreateSpan_Attributes(Tracer tracer)
    {
        using var span = tracer.StartSpan("span");
        span.SetAttribute("string", "string");
        span.SetAttribute("int", 1);
        span.SetAttribute("long", 1L);
        span.SetAttribute("bool", false);
        span.End();
    }

    public static void CreateSpan_Propagate(Tracer tracer)
    {
        using var span = tracer.StartSpan("span");
        using (Tracer.WithSpan(span))
        {
        }

        span.End();
    }

    public static void CreateSpan_Active(Tracer tracer)
    {
        using var span = tracer.StartSpan("span");
    }

    public static void CreateSpan_Active_GetCurrent(Tracer tracer)
    {
        TelemetrySpan span;

        using (tracer.StartSpan("span"))
        {
            span = Tracer.CurrentSpan;
        }
    }
}