// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;

namespace TestApp.AspNetCore.Controllers;

public class ChildActivityController : Controller
{
    [HttpGet]
    [Route("api/GetChildActivityTraceContext")]
    public Dictionary<string, string> GetChildActivityTraceContext()
    {
        var result = new Dictionary<string, string>();
        var activity = new Activity("ActivityInsideHttpRequest");
        activity.Start();
        result["TraceId"] = activity.Context.TraceId.ToString();
        result["ParentSpanId"] = activity.ParentSpanId.ToString();
        if (activity.Context.TraceState != null)
        {
            result["TraceState"] = activity.Context.TraceState;
        }

        activity.Stop();
        return result;
    }

    [HttpGet]
    [Route("api/GetChildActivityBaggageContext")]
    public IReadOnlyDictionary<string, string> GetChildActivityBaggageContext()
    {
        var result = Baggage.Current.GetBaggage();
        return result;
    }
}
