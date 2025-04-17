// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;

namespace TestApp.AspNetCore.Controllers;

[Route("api/[controller]")]
public class ErrorController : Controller
{
    // GET api/error
    [HttpGet]
    public string Get()
    {
        throw new InvalidOperationException("something's wrong!");
    }
}
