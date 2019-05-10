// <copyright file="CallbackMiddleware.cs" company="OpenTelemetry Authors">
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

namespace TestApp.AspNetCore._2._0
{
    using Microsoft.AspNetCore.Http;
    using System.Threading.Tasks;

    public class CallbackMiddleware
    {
        public class CallbackMiddlewareImpl
        {
            public virtual async Task<bool> ProcessAsync(HttpContext context)
            {
                return await Task.FromResult(true);
            }
        }

        private readonly CallbackMiddlewareImpl _impl;

        private readonly RequestDelegate _next;

        public CallbackMiddleware(RequestDelegate next, CallbackMiddlewareImpl impl)
        {
            _next = next;
            _impl = impl;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_impl== null || await _impl.ProcessAsync(context))
            {
                await _next(context);
            }
        }
    }
}
