// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

[ApiController]
[Route("[controller]")]
public class AttributeRouteController : ControllerBase
{
    [HttpGet]
    [HttpGet("[action]")]
    public IActionResult Get() => this.Ok();

    [HttpGet("[action]/{id}")]
    public IActionResult Get(int id) => this.Ok();

    [HttpGet("{id}/[action]")]
    public IActionResult GetWithActionNameInDifferentSpotInTemplate(int id) => this.Ok();
}
