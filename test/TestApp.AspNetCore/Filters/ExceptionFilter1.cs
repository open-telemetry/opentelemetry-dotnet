// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc.Filters;

namespace TestApp.AspNetCore.Filters;

public class ExceptionFilter1 : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // test the behaviour when an application has two ExceptionFilters defined
    }
}