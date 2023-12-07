// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

[Area("MyArea")]
public class ControllerForMyAreaController : Controller
{
    public IActionResult Default() => this.Ok();

    public IActionResult NonDefault() => this.Ok();
}