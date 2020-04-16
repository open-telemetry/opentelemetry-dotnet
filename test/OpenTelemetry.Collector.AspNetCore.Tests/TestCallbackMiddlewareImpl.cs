// <copyright file="TestCallbackMiddlewareImpl.cs" company="OpenTelemetry Authors">
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

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TestApp.AspNetCore._3._1;

namespace OpenTelemetry.Collector.AspNetCore.Tests
{
    public class TestCallbackMiddlewareImpl : CallbackMiddleware.CallbackMiddlewareImpl
    {
        public override async Task<bool> ProcessAsync(HttpContext context)
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("empty");
            return false;
        }
    }
}
