// <copyright file="ChildActivityController.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;

namespace TestApp.AspNetCore._3._1.Controllers
{
    public class ChildActivityController : Controller
    {
        [Route("api/GetChildActivityTraceContext")]
        public Dictionary<string, string> GetChildActivityTraceContext()
        {
            var result = new Dictionary<string, string>();
            var activity = new Activity("ActivityInsideHttpRequest");
            activity.Start();
            result["TraceId"] = activity.Context.TraceId.ToString();
            result["ParentSpanId"] = activity.ParentSpanId.ToString();
            result["TraceState"] = activity.Context.TraceState;
            activity.Stop();
            return result;
        }

        [Route("api/GetChildActivityBaggageContext")]
        public IReadOnlyDictionary<string, string> GetChildActivityBaggageContext()
        {
            var result = Baggage.Current.GetBaggage();
            return result;
        }
    }
}
