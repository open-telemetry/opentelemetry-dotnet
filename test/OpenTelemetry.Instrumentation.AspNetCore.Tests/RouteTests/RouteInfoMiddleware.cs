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

#nullable disable

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using static System.Net.Mime.MediaTypeNames;

namespace RouteTests;

public class RouteInfoMiddleware
{
    private readonly RequestDelegate next;

    public RouteInfoMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public static void ConfigureExceptionHandler(IApplicationBuilder builder)
    {
        builder.Run(async context =>
        {
            context.Response.Body = (context.Items["originBody"] as Stream)!;

            context.Response.ContentType = Application.Json;

            var info = context.Items["RouteInfo"] as RouteInfo;
            Debug.Assert(info != null, "RouteInfo object not present in context.Items");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            string modifiedResponse = JsonSerializer.Serialize(info, jsonOptions);
            await context.Response.WriteAsync(modifiedResponse);
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        var originBody = response.Body;
        context.Items["originBody"] = originBody;
        using var newBody = new MemoryStream();
        response.Body = newBody;

        await this.next(context);

        var stream = response.Body;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var originalResponse = await reader.ReadToEndAsync();

        var info = context.Items["RouteInfo"] as RouteInfo;
        Debug.Assert(info != null, "RouteInfo object not present in context.Items");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string modifiedResponse = JsonSerializer.Serialize(info, jsonOptions);

        stream.SetLength(0);
        using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(modifiedResponse);
        await writer.FlushAsync();
        response.ContentLength = stream.Length;
        response.ContentType = "application/json";

        newBody.Seek(0, SeekOrigin.Begin);
        await newBody.CopyToAsync(originBody);
        response.Body = originBody;
    }
}
