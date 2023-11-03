# ASP.NET Core `http.route` tests

This folder contains a test suite that validates the instrumentation produces
the expected `http.route` attribute on both the activity and metric it emits.
When available, the `http.route` is also a required component of the
`Activity.DisplayName`.

The test suite covers a variety of different routing scenarios available for
ASP.NET Core:

* [Conventional routing](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing#conventional-routing)
* [Conventional routing using areas](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing#areas)
* [Attribute routing](https://learn.microsoft.com/aspnet/core/mvc/controllers/routing#attribute-routing-for-rest-apis)
* [Razor pages](https://learn.microsoft.com/aspnet/core/razor-pages/razor-pages-conventions)
* [Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers)

The individual test cases are defined in [RoutingTestCases.json](./RoutingTestCases.json).

The test suite is unique in that, when run, it generates README files for each
target framework which aids in documenting how the instrumentation behaves for
each test case. These files are source-controlled, so if the behavior of the
instrumentation changes, the README files will be updated to reflect the change.

* [.NET 6](./README.net6.0.md)
* [.NET 7](./README.net6.0.md)
* [.NET 8](./README.net6.0.md)

For each test case a request is made to an ASP.NET Core application with a
particular routing configuration. ASP.NET Core offers a
[variety of APIs](#aspnet-core-apis-for-retrieving-route-information) for
retrieving the route information of a given request. The README files include
detailed information documenting the route information available using the
various APIs in each test case. For example, here is the detailed result
generated for a test case:

```json
{
  "IdealHttpRoute": "ConventionalRoute/ActionWithStringParameter/{id?}",
  "ActivityDisplayName": "/ConventionalRoute/ActionWithStringParameter/2",
  "ActivityHttpRoute": "",
  "MetricHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/ConventionalRoute/ActionWithStringParameter/2?num=3",
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter",
      "id": "2"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": null,
      "Parameters": [
        "id",
        "num"
      ],
      "ControllerActionDescriptor": {
        "ControllerName": "ConventionalRoute",
        "ActionName": "ActionWithStringParameter"
      },
      "PageActionDescriptor": null
    }
  }
}
```

> [!NOTE]
> The test result currently includes an `IdealHttpRoute` property. This is
> temporary, and is meant to drive a conversation to determine the best way
> for generating the `http.route` attribute under different routing scenarios.
> In the example above, the path invoked is
> `/ConventionalRoute/ActionWithStringParameter/2?num=3`. Currently, we see
> that the `http.route` attribute on the metric emitted is
> `{controller=ConventionalRoute}/{action=Default}/{id?}` which was derived
> using [`RoutePattern.RawText`](#routepatternrawtext). This is not ideal
> because the route template does not include the actual action that was
> invoked `ActionWithStringParameter`. The invoked action could be derived
> using either the [`ControllerActionDescriptor`](#controlleractiondescriptor)
> or [`HttpContext.GetRouteData()`](#httpcontextgetroutedata).

## ASP.NET Core APIs for retrieving route information

Included below are short snippets illustrating the use of the various
APIs available for retrieving route information.

### Retrieving the route template

#### [RoutePattern.RawText](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.patterns.routepattern.rawtext)

```csharp
(httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
```

#### [IRouteDiagnosticsMetadata.Route](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.metadata.iroutediagnosticsmetadata.route)

```csharp
httpContext.GetEndpoint()?.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()?.Route;
```

### [HttpContext.GetRouteData()](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.routinghttpcontextextensions.getroutedata)

```csharp
foreach (var value in context.GetRouteData().Values)
{
    Console.WriteLine($"{value.Key} = {value.Value?.ToString()}");
}
```

### Information from the ActionDescriptor

#### [AttributeRouteInfo.Template](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.routing.attributerouteinfo.template)

```csharp
actionDescriptor.AttributeRouteInfo?.Template;
```

#### [Parameters](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.abstractions.actiondescriptor.parameters#microsoft-aspnetcore-mvc-abstractions-actiondescriptor-parameters)

```csharp
actionDescriptor.Parameters;
```

#### [ControllerActionDescriptor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.controllers.controlleractiondescriptor)

```csharp
(actionDescriptor as ControllerActionDescriptor)?.ControllerName;
(actionDescriptor as ControllerActionDescriptor)?.ActionName;
```

#### [PageActionDescriptor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.razorpages.pageactiondescriptor)

```csharp
(actionDescriptor as PageActionDescriptor)?.RelativePath;
(actionDescriptor as PageActionDescriptor)?.ViewEnginePath;
```
