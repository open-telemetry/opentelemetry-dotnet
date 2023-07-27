// <copyright file="ActivityMiddleware.cs" company="OpenTelemetry Authors">
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

namespace TestApp.AspNetCore;

public class ActivityMiddleware
{
    private readonly ActivityMiddlewareImpl impl;
    private readonly RequestDelegate next;

    public ActivityMiddleware(RequestDelegate next, ActivityMiddlewareImpl impl)
    {
        this.next = next;
        this.impl = impl;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (this.impl != null)
        {
            this.impl.PreProcess(context);
        }

        await this.next(context).ConfigureAwait(false);

        if (this.impl != null)
        {
            this.impl.PostProcess(context);
        }
    }

    public class ActivityMiddlewareImpl
    {
        public virtual void PreProcess(HttpContext context)
        {
            // Do nothing
        }

        public virtual void PostProcess(HttpContext context)
        {
            // Do nothing
        }
    }
}
