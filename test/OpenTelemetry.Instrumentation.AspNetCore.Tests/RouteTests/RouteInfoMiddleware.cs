// <copyright file="RouteInfoMiddleware.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RouteTests;

public class RouteInfoMiddleware
{
    public static readonly List<RouteInfo> RouteInfos = new();
    private readonly RequestDelegate next;

    public RouteInfoMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // This middleware responds with the route information captured from the
        // previous request when the request path contains GetLastRouteInfo.
        // This is used for generating the README files upon running the test suite.
        // Otherwise, the middleware serves as a passthrough.
        if (context.Request.Path.ToString().Contains("GetLastRouteInfo"))
        {
            var response = context.Response;
            var info = RouteInfos.Last();
            Debug.Assert(info != null, "RouteInfo object not present in context.Items");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string modifiedResponse = JsonSerializer.Serialize(info, jsonOptions);
            response.ContentType = "application/json";
            await response.WriteAsync(modifiedResponse);
        }
        else
        {
            await this.next(context);
        }
    }
}
