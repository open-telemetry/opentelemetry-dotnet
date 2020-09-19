// <copyright file="CallbackMiddleware.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TestApp.AspNetCore._3._1
{
    public class CallbackMiddleware
    {
        private readonly CallbackMiddlewareImpl impl;
        private readonly RequestDelegate next;

        public CallbackMiddleware(RequestDelegate next, CallbackMiddlewareImpl impl)
        {
            this.next = next;
            this.impl = impl;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (this.impl == null || await this.impl.ProcessAsync(context))
            {
                await this.next(context);
            }
        }

        public class CallbackMiddlewareImpl
        {
            public virtual async Task<bool> ProcessAsync(HttpContext context)
            {
                return await Task.FromResult(true);
            }
        }
    }
}
