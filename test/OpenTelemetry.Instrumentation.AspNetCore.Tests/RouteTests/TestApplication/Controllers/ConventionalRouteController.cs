// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using Microsoft.AspNetCore.Mvc;

namespace RouteTests.Controllers;

public class ConventionalRouteController : Controller
{
    public IActionResult Default() => this.Ok();

    public IActionResult ActionWithParameter(int id) => this.Ok();

    public IActionResult ActionWithStringParameter(string id, int num) => this.Ok();
}