# Test results for ASP.NET Core 8

[fake change to trigger CI]

| http.route | App | Test Name |
| - | - | - |
| :broken_heart: | ConventionalRouting | [Root path](#conventionalrouting-root-path) |
| :broken_heart: | ConventionalRouting | [Non-default action with route parameter and query string](#conventionalrouting-non-default-action-with-route-parameter-and-query-string) |
| :broken_heart: | ConventionalRouting | [Non-default action with query string](#conventionalrouting-non-default-action-with-query-string) |
| :green_heart: | ConventionalRouting | [Not Found (404)](#conventionalrouting-not-found-404) |
| :green_heart: | ConventionalRouting | [Route template with parameter constraint](#conventionalrouting-route-template-with-parameter-constraint) |
| :green_heart: | ConventionalRouting | [Path that does not match parameter constraint](#conventionalrouting-path-that-does-not-match-parameter-constraint) |
| :broken_heart: | ConventionalRouting | [Area using area:exists, default controller/action](#conventionalrouting-area-using-areaexists-default-controlleraction) |
| :broken_heart: | ConventionalRouting | [Area using area:exists, non-default action](#conventionalrouting-area-using-areaexists-non-default-action) |
| :broken_heart: | ConventionalRouting | [Area w/o area:exists, default controller/action](#conventionalrouting-area-wo-areaexists-default-controlleraction) |
| :green_heart: | AttributeRouting | [Default action](#attributerouting-default-action) |
| :green_heart: | AttributeRouting | [Action without parameter](#attributerouting-action-without-parameter) |
| :green_heart: | AttributeRouting | [Action with parameter](#attributerouting-action-with-parameter) |
| :green_heart: | AttributeRouting | [Action with parameter before action name in template](#attributerouting-action-with-parameter-before-action-name-in-template) |
| :green_heart: | AttributeRouting | [Action invoked resulting in 400 Bad Request](#attributerouting-action-invoked-resulting-in-400-bad-request) |
| :broken_heart: | RazorPages | [Root path](#razorpages-root-path) |
| :broken_heart: | RazorPages | [Index page](#razorpages-index-page) |
| :broken_heart: | RazorPages | [Throws exception](#razorpages-throws-exception) |
| :green_heart: | RazorPages | [Static content](#razorpages-static-content) |
| :green_heart: | MinimalApi | [Action without parameter](#minimalapi-action-without-parameter) |
| :green_heart: | MinimalApi | [Action with parameter](#minimalapi-action-with-parameter) |
| :green_heart: | MinimalApi | [Action without parameter (MapGroup)](#minimalapi-action-without-parameter-mapgroup) |
| :green_heart: | MinimalApi | [Action with parameter (MapGroup)](#minimalapi-action-with-parameter-mapgroup) |

## ConventionalRouting: Root path

```json
{
  "IdealHttpRoute": "ConventionalRoute/Default/{id?}",
  "ActivityDisplayName": "GET {controller=ConventionalRoute}/{action=Default}/{id?}",
  "ActivityHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "MetricHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/",
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "HttpContext.GetRouteData()": {
      "controller": "ConventionalRoute",
      "action": "Default"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": null,
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "ConventionalRoute",
        "ActionName": "Default"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## ConventionalRouting: Non-default action with route parameter and query string

```json
{
  "IdealHttpRoute": "ConventionalRoute/ActionWithStringParameter/{id?}",
  "ActivityDisplayName": "GET {controller=ConventionalRoute}/{action=Default}/{id?}",
  "ActivityHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "MetricHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/ConventionalRoute/ActionWithStringParameter/2?num=3",
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "{controller=ConventionalRoute}/{action=Default}/{id?}",
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

## ConventionalRouting: Non-default action with query string

```json
{
  "IdealHttpRoute": "ConventionalRoute/ActionWithStringParameter/{id?}",
  "ActivityDisplayName": "GET {controller=ConventionalRoute}/{action=Default}/{id?}",
  "ActivityHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "MetricHttpRoute": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/ConventionalRoute/ActionWithStringParameter?num=3",
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "HttpContext.GetRouteData()": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter"
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

## ConventionalRouting: Not Found (404)

```json
{
  "IdealHttpRoute": "",
  "ActivityDisplayName": "GET",
  "ActivityHttpRoute": "",
  "MetricHttpRoute": "",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/ConventionalRoute/NotFound",
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## ConventionalRouting: Route template with parameter constraint

```json
{
  "IdealHttpRoute": "SomePath/{id}/{num:int}",
  "ActivityDisplayName": "GET SomePath/{id}/{num:int}",
  "ActivityHttpRoute": "SomePath/{id}/{num:int}",
  "MetricHttpRoute": "SomePath/{id}/{num:int}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/SomePath/SomeString/2",
    "RoutePattern.RawText": "SomePath/{id}/{num:int}",
    "IRouteDiagnosticsMetadata.Route": "SomePath/{id}/{num:int}",
    "HttpContext.GetRouteData()": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter",
      "id": "SomeString",
      "num": "2"
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

## ConventionalRouting: Path that does not match parameter constraint

```json
{
  "IdealHttpRoute": "",
  "ActivityDisplayName": "GET",
  "ActivityHttpRoute": "",
  "MetricHttpRoute": "",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/SomePath/SomeString/NotAnInt",
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## ConventionalRouting: Area using area:exists, default controller/action

```json
{
  "IdealHttpRoute": "{area:exists}/ControllerForMyArea/Default/{id?}",
  "ActivityDisplayName": "GET {area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "ActivityHttpRoute": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "MetricHttpRoute": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MyArea",
    "RoutePattern.RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "HttpContext.GetRouteData()": {
      "controller": "ControllerForMyArea",
      "action": "Default",
      "area": "MyArea"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": null,
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "ControllerForMyArea",
        "ActionName": "Default"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## ConventionalRouting: Area using area:exists, non-default action

```json
{
  "IdealHttpRoute": "{area:exists}/ControllerForMyArea/NonDefault/{id?}",
  "ActivityDisplayName": "GET {area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "ActivityHttpRoute": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "MetricHttpRoute": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MyArea/ControllerForMyArea/NonDefault",
    "RoutePattern.RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "HttpContext.GetRouteData()": {
      "controller": "ControllerForMyArea",
      "area": "MyArea",
      "action": "NonDefault"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": null,
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "ControllerForMyArea",
        "ActionName": "NonDefault"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## ConventionalRouting: Area w/o area:exists, default controller/action

```json
{
  "IdealHttpRoute": "SomePrefix/AnotherArea/Index/{id?}",
  "ActivityDisplayName": "GET SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
  "ActivityHttpRoute": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
  "MetricHttpRoute": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/SomePrefix",
    "RoutePattern.RawText": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
    "IRouteDiagnosticsMetadata.Route": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
    "HttpContext.GetRouteData()": {
      "area": "AnotherArea",
      "controller": "AnotherArea",
      "action": "Index"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": null,
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "AnotherArea",
        "ActionName": "Index"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## AttributeRouting: Default action

```json
{
  "IdealHttpRoute": "AttributeRoute",
  "ActivityDisplayName": "GET AttributeRoute",
  "ActivityHttpRoute": "AttributeRoute",
  "MetricHttpRoute": "AttributeRoute",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/AttributeRoute",
    "RoutePattern.RawText": "AttributeRoute",
    "IRouteDiagnosticsMetadata.Route": "AttributeRoute",
    "HttpContext.GetRouteData()": {
      "action": "Get",
      "controller": "AttributeRoute"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "AttributeRoute",
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "AttributeRoute",
        "ActionName": "Get"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## AttributeRouting: Action without parameter

```json
{
  "IdealHttpRoute": "AttributeRoute/Get",
  "ActivityDisplayName": "GET AttributeRoute/Get",
  "ActivityHttpRoute": "AttributeRoute/Get",
  "MetricHttpRoute": "AttributeRoute/Get",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/AttributeRoute/Get",
    "RoutePattern.RawText": "AttributeRoute/Get",
    "IRouteDiagnosticsMetadata.Route": "AttributeRoute/Get",
    "HttpContext.GetRouteData()": {
      "action": "Get",
      "controller": "AttributeRoute"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "AttributeRoute/Get",
      "Parameters": [],
      "ControllerActionDescriptor": {
        "ControllerName": "AttributeRoute",
        "ActionName": "Get"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## AttributeRouting: Action with parameter

```json
{
  "IdealHttpRoute": "AttributeRoute/Get/{id}",
  "ActivityDisplayName": "GET AttributeRoute/Get/{id}",
  "ActivityHttpRoute": "AttributeRoute/Get/{id}",
  "MetricHttpRoute": "AttributeRoute/Get/{id}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/AttributeRoute/Get/12",
    "RoutePattern.RawText": "AttributeRoute/Get/{id}",
    "IRouteDiagnosticsMetadata.Route": "AttributeRoute/Get/{id}",
    "HttpContext.GetRouteData()": {
      "action": "Get",
      "controller": "AttributeRoute",
      "id": "12"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "AttributeRoute/Get/{id}",
      "Parameters": [
        "id"
      ],
      "ControllerActionDescriptor": {
        "ControllerName": "AttributeRoute",
        "ActionName": "Get"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## AttributeRouting: Action with parameter before action name in template

```json
{
  "IdealHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "ActivityDisplayName": "GET AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "ActivityHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "MetricHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/AttributeRoute/12/GetWithActionNameInDifferentSpotInTemplate",
    "RoutePattern.RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "IRouteDiagnosticsMetadata.Route": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "HttpContext.GetRouteData()": {
      "action": "GetWithActionNameInDifferentSpotInTemplate",
      "controller": "AttributeRoute",
      "id": "12"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
      "Parameters": [
        "id"
      ],
      "ControllerActionDescriptor": {
        "ControllerName": "AttributeRoute",
        "ActionName": "GetWithActionNameInDifferentSpotInTemplate"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## AttributeRouting: Action invoked resulting in 400 Bad Request

```json
{
  "IdealHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "ActivityDisplayName": "GET AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "ActivityHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "MetricHttpRoute": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/AttributeRoute/NotAnInt/GetWithActionNameInDifferentSpotInTemplate",
    "RoutePattern.RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "IRouteDiagnosticsMetadata.Route": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "HttpContext.GetRouteData()": {
      "action": "GetWithActionNameInDifferentSpotInTemplate",
      "controller": "AttributeRoute",
      "id": "NotAnInt"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
      "Parameters": [
        "id"
      ],
      "ControllerActionDescriptor": {
        "ControllerName": "AttributeRoute",
        "ActionName": "GetWithActionNameInDifferentSpotInTemplate"
      },
      "PageActionDescriptor": null
    }
  }
}
```

## RazorPages: Root path

```json
{
  "IdealHttpRoute": "/Index",
  "ActivityDisplayName": "GET",
  "ActivityHttpRoute": "",
  "MetricHttpRoute": "",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/",
    "RoutePattern.RawText": "",
    "IRouteDiagnosticsMetadata.Route": "",
    "HttpContext.GetRouteData()": {
      "page": "/Index"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "",
      "Parameters": [],
      "ControllerActionDescriptor": null,
      "PageActionDescriptor": {
        "RelativePath": "/Pages/Index.cshtml",
        "ViewEnginePath": "/Index"
      }
    }
  }
}
```

## RazorPages: Index page

```json
{
  "IdealHttpRoute": "/Index",
  "ActivityDisplayName": "GET Index",
  "ActivityHttpRoute": "Index",
  "MetricHttpRoute": "Index",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/Index",
    "RoutePattern.RawText": "Index",
    "IRouteDiagnosticsMetadata.Route": "Index",
    "HttpContext.GetRouteData()": {
      "page": "/Index"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "Index",
      "Parameters": [],
      "ControllerActionDescriptor": null,
      "PageActionDescriptor": {
        "RelativePath": "/Pages/Index.cshtml",
        "ViewEnginePath": "/Index"
      }
    }
  }
}
```

## RazorPages: Throws exception

```json
{
  "IdealHttpRoute": "/PageThatThrowsException",
  "ActivityDisplayName": "GET PageThatThrowsException",
  "ActivityHttpRoute": "PageThatThrowsException",
  "MetricHttpRoute": "PageThatThrowsException",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/PageThatThrowsException",
    "RoutePattern.RawText": "PageThatThrowsException",
    "IRouteDiagnosticsMetadata.Route": "PageThatThrowsException",
    "HttpContext.GetRouteData()": {
      "page": "/PageThatThrowsException"
    },
    "ActionDescriptor": {
      "AttributeRouteInfo.Template": "PageThatThrowsException",
      "Parameters": [],
      "ControllerActionDescriptor": null,
      "PageActionDescriptor": {
        "RelativePath": "/Pages/PageThatThrowsException.cshtml",
        "ViewEnginePath": "/PageThatThrowsException"
      }
    }
  }
}
```

## RazorPages: Static content

```json
{
  "IdealHttpRoute": "",
  "ActivityDisplayName": "GET",
  "ActivityHttpRoute": "",
  "MetricHttpRoute": "",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/js/site.js",
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## MinimalApi: Action without parameter

```json
{
  "IdealHttpRoute": "/MinimalApi",
  "ActivityDisplayName": "GET /MinimalApi",
  "ActivityHttpRoute": "/MinimalApi",
  "MetricHttpRoute": "/MinimalApi",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MinimalApi",
    "RoutePattern.RawText": "/MinimalApi",
    "IRouteDiagnosticsMetadata.Route": "/MinimalApi",
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## MinimalApi: Action with parameter

```json
{
  "IdealHttpRoute": "/MinimalApi/{id}",
  "ActivityDisplayName": "GET /MinimalApi/{id}",
  "ActivityHttpRoute": "/MinimalApi/{id}",
  "MetricHttpRoute": "/MinimalApi/{id}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MinimalApi/123",
    "RoutePattern.RawText": "/MinimalApi/{id}",
    "IRouteDiagnosticsMetadata.Route": "/MinimalApi/{id}",
    "HttpContext.GetRouteData()": {
      "id": "123"
    },
    "ActionDescriptor": null
  }
}
```

## MinimalApi: Action without parameter (MapGroup)

```json
{
  "IdealHttpRoute": "/MinimalApiUsingMapGroup/",
  "ActivityDisplayName": "GET /MinimalApiUsingMapGroup/",
  "ActivityHttpRoute": "/MinimalApiUsingMapGroup/",
  "MetricHttpRoute": "/MinimalApiUsingMapGroup/",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MinimalApiUsingMapGroup",
    "RoutePattern.RawText": "/MinimalApiUsingMapGroup/",
    "IRouteDiagnosticsMetadata.Route": "/MinimalApiUsingMapGroup/",
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## MinimalApi: Action with parameter (MapGroup)

```json
{
  "IdealHttpRoute": "/MinimalApiUsingMapGroup/{id}",
  "ActivityDisplayName": "GET /MinimalApiUsingMapGroup/{id}",
  "ActivityHttpRoute": "/MinimalApiUsingMapGroup/{id}",
  "MetricHttpRoute": "/MinimalApiUsingMapGroup/{id}",
  "RouteInfo": {
    "HttpMethod": "GET",
    "Path": "/MinimalApiUsingMapGroup/123",
    "RoutePattern.RawText": "/MinimalApiUsingMapGroup/{id}",
    "IRouteDiagnosticsMetadata.Route": "/MinimalApiUsingMapGroup/{id}",
    "HttpContext.GetRouteData()": {
      "id": "123"
    },
    "ActionDescriptor": null
  }
}
```
