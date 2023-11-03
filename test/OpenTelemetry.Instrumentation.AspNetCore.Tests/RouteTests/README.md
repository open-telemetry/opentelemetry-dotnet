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
> using `RoutePattern.RawText`. This is not ideal
> because the route template does not include the actual action that was
> invoked `ActionWithStringParameter`. The invoked action could be derived
> using either the `ControllerActionDescriptor`
> or `HttpContext.GetRouteData()`.

## ASP.NET Core APIs for retrieving route information

Included below are short snippets illustrating the use of the various
APIs available for retrieving route information.

### Retrieving the route template

The route template can be obtained from `HttpContext` by retrieving the
`RouteEndpoint` using the following two APIs.

For attribute routing and minimal API scenarios, using the route template alone
is sufficient for deriving `http.route` in all test cases.

The route template does not well describe the `http.route` in conventional
routing and some Razor page scenarios.

#### [RoutePattern.RawText](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.patterns.routepattern.rawtext)

```csharp
(httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
```

#### [IRouteDiagnosticsMetadata.Route](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.metadata.iroutediagnosticsmetadata.route)

This API was introduced in .NET 8.

```csharp
httpContext.GetEndpoint()?.Metadata.GetMetadata<IRouteDiagnosticsMetadata>()?.Route;
```

### RouteData

`RouteData` can be retrieved from `HttpContext` using the `GetRouteData()`
extension method. The values obtained from `RouteData` identify the controller/
action or Razor page invoked by the request.

#### [HttpContext.GetRouteData()](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.routinghttpcontextextensions.getroutedata)

```csharp
foreach (var value in httpContext.GetRouteData().Values)
{
    Console.WriteLine($"{value.Key} = {value.Value?.ToString()}");
}
```

For example, the above code produces something like:

```text
controller = ConventionalRoute
action = ActionWithStringParameter
id = 2
```

### Information from the ActionDescriptor

For requests that invoke an action or Razor page, the `ActionDescriptor` can
be used to access route information.

#### [AttributeRouteInfo.Template](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.routing.attributerouteinfo.template)

The `AttributeRouteInfo.Template` is equivalent to using
[other APIs for retrieving the route template](#retrieving-the-route-template)
when using attribute routing. For conventional routing and Razor pages it will
be `null`.

```csharp
actionDescriptor.AttributeRouteInfo?.Template;
```

#### [ControllerActionDescriptor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.controllers.controlleractiondescriptor)

For requests that invoke an action on a controller, the `ActionDescriptor`
will be of type `ControllerActionDescriptor` which includes the controller and
action name.

```csharp
(actionDescriptor as ControllerActionDescriptor)?.ControllerName;
(actionDescriptor as ControllerActionDescriptor)?.ActionName;
```

#### [PageActionDescriptor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.razorpages.pageactiondescriptor)

For requests that invoke a Razor page, the `ActionDescriptor`
will be of type `PageActionDescriptor` which includes the path to the invoked
page.

```csharp
(actionDescriptor as PageActionDescriptor)?.RelativePath;
(actionDescriptor as PageActionDescriptor)?.ViewEnginePath;
```

#### [Parameters](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.abstractions.actiondescriptor.parameters#microsoft-aspnetcore-mvc-abstractions-actiondescriptor-parameters)

The `ActionDescriptor.Parameters` property is interesting because it describes
the actual parameters (type and name) of an invoked action method. Some APM
products use `ActionDescriptor.Parameters` to more precisely describe the
method an endpoint invokes since not all parameters may be present in the
route template.

Consider the following action method:

```csharp
public IActionResult SomeActionMethod(string id, int num) { ... }
```

Using conventional routing assuming a default route template
`{controller=ConventionalRoute}/{action=Default}/{id?}`, the `SomeActionMethod`
may match this route template. The route template describes the `id` parameter
but not the `num` parameter.

```csharp
foreach (var parameter in actionDescriptor.Parameters)
{
    Console.WriteLine($"{parameter.Name}");
}
```

The above code produces:

```text
id
num
```
