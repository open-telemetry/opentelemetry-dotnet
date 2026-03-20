// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace TestApp.AspNetCore;

internal sealed class ActivityMiddlewareCore
{
#pragma warning disable IDE0060 // Remove unused parameter
    public void PreProcess(HttpContext context)
    {
        // Do nothing
    }

    public void PostProcess(HttpContext context)
    {
        // Do nothing
    }
#pragma warning restore IDE0060 // Remove unused parameter
}
