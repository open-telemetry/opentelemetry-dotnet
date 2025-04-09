// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

public class CallbackMiddlewareCore
{
    public virtual Task<bool> ProcessAsync(HttpContext context)
    {
        return Task.FromResult(true);
    }
}
