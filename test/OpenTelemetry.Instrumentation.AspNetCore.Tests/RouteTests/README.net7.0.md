# Test results for ASP.NET Core 7

| | | display name | expected name (w/o http.method) | routing type | request |
| - | - | - | - | - | - |
| :broken_heart: | [1](#1) | / | ConventionalRoute/Default/{id?} | ConventionalRouting | GET / | / |
| :broken_heart: | [2](#2) | /ConventionalRoute/ActionWithStringParameter/2 | ConventionalRoute/ActionWithStringParameter/{id?} | ConventionalRouting | GET /ConventionalRoute/ActionWithStringParameter/2?num=3 | /ConventionalRoute/ActionWithStringParameter/2 |
| :broken_heart: | [3](#3) | /ConventionalRoute/ActionWithStringParameter | ConventionalRoute/ActionWithStringParameter/{id?} | ConventionalRouting | GET /ConventionalRoute/ActionWithStringParameter?num=3 | /ConventionalRoute/ActionWithStringParameter |
| :broken_heart: | [4](#4) | /ConventionalRoute/NotFound |  | ConventionalRouting | GET /ConventionalRoute/NotFound | /ConventionalRoute/NotFound |
| :broken_heart: | [5](#5) | /SomePath/SomeString/2 | SomePath/{id}/{num:int} | ConventionalRouting | GET /SomePath/SomeString/2 | /SomePath/SomeString/2 |
| :broken_heart: | [6](#6) | /SomePath/SomeString/NotAnInt |  | ConventionalRouting | GET /SomePath/SomeString/NotAnInt | /SomePath/SomeString/NotAnInt |
| :broken_heart: | [7](#7) | /MyArea | {area:exists}/ControllerForMyArea/Default/{id?} | ConventionalRouting | GET /MyArea | /MyArea |
| :broken_heart: | [8](#8) | /MyArea/ControllerForMyArea/NonDefault | {area:exists}/ControllerForMyArea/NonDefault/{id?} | ConventionalRouting | GET /MyArea/ControllerForMyArea/NonDefault | /MyArea/ControllerForMyArea/NonDefault |
| :broken_heart: | [9](#9) | /SomePrefix | SomePrefix/AnotherArea/Index/{id?} | ConventionalRouting | GET /SomePrefix | /SomePrefix |
| :green_heart: | [10](#10) | AttributeRoute | AttributeRoute | AttributeRouting | GET /AttributeRoute | AttributeRoute |
| :green_heart: | [11](#11) | AttributeRoute/Get | AttributeRoute/Get | AttributeRouting | GET /AttributeRoute/Get | AttributeRoute/Get |
| :green_heart: | [12](#12) | AttributeRoute/Get/{id} | AttributeRoute/Get/{id} | AttributeRouting | GET /AttributeRoute/Get/12 | AttributeRoute/Get/{id} |
| :green_heart: | [13](#13) | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate | AttributeRouting | GET /AttributeRoute/12/GetWithActionNameInDifferentSpotInTemplate | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate |
| :green_heart: | [14](#14) | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate | AttributeRouting | GET /AttributeRoute/NotAnInt/GetWithActionNameInDifferentSpotInTemplate | AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate |
| :broken_heart: | [15](#15) | / | /Index | RazorPages | GET / | / |
| :broken_heart: | [16](#16) | Index | /Index | RazorPages | GET /Index | Index |
| :broken_heart: | [17](#17) | PageThatThrowsException | /PageThatThrowsException | RazorPages | GET /PageThatThrowsException | PageThatThrowsException |
| :broken_heart: | [18](#18) | /js/site.js |  | RazorPages | GET /js/site.js | /js/site.js |
| :broken_heart: | [19](#19) | /MinimalApi | /MinimalApi/ | MinimalApi | GET /MinimalApi | /MinimalApi |
| :broken_heart: | [20](#20) | /MinimalApi/123 | /MinimalApi/{id} | MinimalApi | GET /MinimalApi/123 | /MinimalApi/123 |

## 1

```json
{
  "HttpMethod": "GET",
  "Path": "/",
  "HttpRouteByRawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "HttpRouteByControllerActionAndParameters": "ConventionalRoute/Default",
  "HttpRouteByActionDescriptor": "ConventionalRoute/Default/{id?}",
  "RouteSummary": {
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 2

```json
{
  "HttpMethod": "GET",
  "Path": "/ConventionalRoute/ActionWithStringParameter/2?num=3",
  "HttpRouteByRawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "HttpRouteByControllerActionAndParameters": "ConventionalRoute/ActionWithStringParameter/{id}/{num}",
  "HttpRouteByActionDescriptor": "ConventionalRoute/ActionWithStringParameter/{id?}",
  "RouteSummary": {
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

## 3

```json
{
  "HttpMethod": "GET",
  "Path": "/ConventionalRoute/ActionWithStringParameter?num=3",
  "HttpRouteByRawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
  "HttpRouteByControllerActionAndParameters": "ConventionalRoute/ActionWithStringParameter/{id}/{num}",
  "HttpRouteByActionDescriptor": "ConventionalRoute/ActionWithStringParameter/{id?}",
  "RouteSummary": {
    "RoutePattern.RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 4

```json
{
  "HttpMethod": "GET",
  "Path": "/ConventionalRoute/NotFound",
  "HttpRouteByRawText": null,
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": null,
  "RouteSummary": {
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## 5

```json
{
  "HttpMethod": "GET",
  "Path": "/SomePath/SomeString/2",
  "HttpRouteByRawText": "SomePath/{id}/{num:int}",
  "HttpRouteByControllerActionAndParameters": "ConventionalRoute/ActionWithStringParameter/{id}/{num}",
  "HttpRouteByActionDescriptor": "SomePath/{id}/{num:int}",
  "RouteSummary": {
    "RoutePattern.RawText": "SomePath/{id}/{num:int}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 6

```json
{
  "HttpMethod": "GET",
  "Path": "/SomePath/SomeString/NotAnInt",
  "HttpRouteByRawText": null,
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": null,
  "RouteSummary": {
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## 7

```json
{
  "HttpMethod": "GET",
  "Path": "/MyArea",
  "HttpRouteByRawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "HttpRouteByControllerActionAndParameters": "ControllerForMyArea/Default",
  "HttpRouteByActionDescriptor": "{area:exists}/ControllerForMyArea/Default/{id?}",
  "RouteSummary": {
    "RoutePattern.RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 8

```json
{
  "HttpMethod": "GET",
  "Path": "/MyArea/ControllerForMyArea/NonDefault",
  "HttpRouteByRawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
  "HttpRouteByControllerActionAndParameters": "ControllerForMyArea/NonDefault",
  "HttpRouteByActionDescriptor": "{area:exists}/ControllerForMyArea/NonDefault/{id?}",
  "RouteSummary": {
    "RoutePattern.RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 9

```json
{
  "HttpMethod": "GET",
  "Path": "/SomePrefix",
  "HttpRouteByRawText": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
  "HttpRouteByControllerActionAndParameters": "AnotherArea/Index",
  "HttpRouteByActionDescriptor": "SomePrefix/AnotherArea/Index/{id?}",
  "RouteSummary": {
    "RoutePattern.RawText": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 10

```json
{
  "HttpMethod": "GET",
  "Path": "/AttributeRoute",
  "HttpRouteByRawText": "AttributeRoute",
  "HttpRouteByControllerActionAndParameters": "AttributeRoute/Get",
  "HttpRouteByActionDescriptor": "AttributeRoute",
  "RouteSummary": {
    "RoutePattern.RawText": "AttributeRoute",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 11

```json
{
  "HttpMethod": "GET",
  "Path": "/AttributeRoute/Get",
  "HttpRouteByRawText": "AttributeRoute/Get",
  "HttpRouteByControllerActionAndParameters": "AttributeRoute/Get",
  "HttpRouteByActionDescriptor": "AttributeRoute/Get",
  "RouteSummary": {
    "RoutePattern.RawText": "AttributeRoute/Get",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 12

```json
{
  "HttpMethod": "GET",
  "Path": "/AttributeRoute/Get/12",
  "HttpRouteByRawText": "AttributeRoute/Get/{id}",
  "HttpRouteByControllerActionAndParameters": "AttributeRoute/Get/{id}",
  "HttpRouteByActionDescriptor": "AttributeRoute/Get/{id}",
  "RouteSummary": {
    "RoutePattern.RawText": "AttributeRoute/Get/{id}",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 13

```json
{
  "HttpMethod": "GET",
  "Path": "/AttributeRoute/12/GetWithActionNameInDifferentSpotInTemplate",
  "HttpRouteByRawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "HttpRouteByControllerActionAndParameters": "AttributeRoute/GetWithActionNameInDifferentSpotInTemplate/{id}",
  "HttpRouteByActionDescriptor": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "RouteSummary": {
    "RoutePattern.RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 14

```json
{
  "HttpMethod": "GET",
  "Path": "/AttributeRoute/NotAnInt/GetWithActionNameInDifferentSpotInTemplate",
  "HttpRouteByRawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "HttpRouteByControllerActionAndParameters": "AttributeRoute/GetWithActionNameInDifferentSpotInTemplate/{id}",
  "HttpRouteByActionDescriptor": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
  "RouteSummary": {
    "RoutePattern.RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 15

```json
{
  "HttpMethod": "GET",
  "Path": "/",
  "HttpRouteByRawText": "",
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": "/Index",
  "RouteSummary": {
    "RoutePattern.RawText": "",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 16

```json
{
  "HttpMethod": "GET",
  "Path": "/Index",
  "HttpRouteByRawText": "Index",
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": "/Index",
  "RouteSummary": {
    "RoutePattern.RawText": "Index",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 17

```json
{
  "HttpMethod": "GET",
  "Path": "/PageThatThrowsException",
  "HttpRouteByRawText": "PageThatThrowsException",
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": "/PageThatThrowsException",
  "RouteSummary": {
    "RoutePattern.RawText": "PageThatThrowsException",
    "IRouteDiagnosticsMetadata.Route": null,
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

## 18

```json
{
  "HttpMethod": "GET",
  "Path": "/js/site.js",
  "HttpRouteByRawText": null,
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": null,
  "RouteSummary": {
    "RoutePattern.RawText": null,
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## 19

```json
{
  "HttpMethod": "GET",
  "Path": "/MinimalApi",
  "HttpRouteByRawText": "/MinimalApi/",
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": null,
  "RouteSummary": {
    "RoutePattern.RawText": "/MinimalApi/",
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {},
    "ActionDescriptor": null
  }
}
```

## 20

```json
{
  "HttpMethod": "GET",
  "Path": "/MinimalApi/123",
  "HttpRouteByRawText": "/MinimalApi/{id}",
  "HttpRouteByControllerActionAndParameters": "",
  "HttpRouteByActionDescriptor": null,
  "RouteSummary": {
    "RoutePattern.RawText": "/MinimalApi/{id}",
    "IRouteDiagnosticsMetadata.Route": null,
    "HttpContext.GetRouteData()": {
      "id": "123"
    },
    "ActionDescriptor": null
  }
}
```
