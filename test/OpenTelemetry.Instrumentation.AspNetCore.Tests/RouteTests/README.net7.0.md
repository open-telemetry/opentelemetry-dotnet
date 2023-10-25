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
  "DebugInfo": {
    "RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ConventionalRoute",
      "action": "Default"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ConventionalRoute",
    "ControllerActionDescriptorActionName": "Default"
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
  "DebugInfo": {
    "RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter",
      "id": "2"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [
      "id",
      "num"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ConventionalRoute",
    "ControllerActionDescriptorActionName": "ActionWithStringParameter"
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
  "DebugInfo": {
    "RawText": "{controller=ConventionalRoute}/{action=Default}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [
      "id",
      "num"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ConventionalRoute",
    "ControllerActionDescriptorActionName": "ActionWithStringParameter"
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
  "HttpRouteByActionDescriptor": "",
  "DebugInfo": {
    "RawText": null,
    "RouteDiagnosticMetadata": null,
    "RouteData": {},
    "AttributeRouteInfo": null,
    "ActionParameters": null,
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "DebugInfo": {
    "RawText": "SomePath/{id}/{num:int}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ConventionalRoute",
      "action": "ActionWithStringParameter",
      "id": "SomeString",
      "num": "2"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [
      "id",
      "num"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ConventionalRoute",
    "ControllerActionDescriptorActionName": "ActionWithStringParameter"
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
  "HttpRouteByActionDescriptor": "",
  "DebugInfo": {
    "RawText": null,
    "RouteDiagnosticMetadata": null,
    "RouteData": {},
    "AttributeRouteInfo": null,
    "ActionParameters": null,
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "DebugInfo": {
    "RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ControllerForMyArea",
      "action": "Default",
      "area": "MyArea"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ControllerForMyArea",
    "ControllerActionDescriptorActionName": "Default"
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
  "DebugInfo": {
    "RawText": "{area:exists}/{controller=ControllerForMyArea}/{action=Default}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "controller": "ControllerForMyArea",
      "area": "MyArea",
      "action": "NonDefault"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "ControllerForMyArea",
    "ControllerActionDescriptorActionName": "NonDefault"
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
  "DebugInfo": {
    "RawText": "SomePrefix/{controller=AnotherArea}/{action=Index}/{id?}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "area": "AnotherArea",
      "controller": "AnotherArea",
      "action": "Index"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AnotherArea",
    "ControllerActionDescriptorActionName": "Index"
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
  "DebugInfo": {
    "RawText": "AttributeRoute",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "action": "Get",
      "controller": "AttributeRoute"
    },
    "AttributeRouteInfo": "AttributeRoute",
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AttributeRoute",
    "ControllerActionDescriptorActionName": "Get"
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
  "DebugInfo": {
    "RawText": "AttributeRoute/Get",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "action": "Get",
      "controller": "AttributeRoute"
    },
    "AttributeRouteInfo": "AttributeRoute/Get",
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AttributeRoute",
    "ControllerActionDescriptorActionName": "Get"
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
  "DebugInfo": {
    "RawText": "AttributeRoute/Get/{id}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "action": "Get",
      "controller": "AttributeRoute",
      "id": "12"
    },
    "AttributeRouteInfo": "AttributeRoute/Get/{id}",
    "ActionParameters": [
      "id"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AttributeRoute",
    "ControllerActionDescriptorActionName": "Get"
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
  "DebugInfo": {
    "RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "action": "GetWithActionNameInDifferentSpotInTemplate",
      "controller": "AttributeRoute",
      "id": "12"
    },
    "AttributeRouteInfo": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "ActionParameters": [
      "id"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AttributeRoute",
    "ControllerActionDescriptorActionName": "GetWithActionNameInDifferentSpotInTemplate"
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
  "DebugInfo": {
    "RawText": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "action": "GetWithActionNameInDifferentSpotInTemplate",
      "controller": "AttributeRoute",
      "id": "NotAnInt"
    },
    "AttributeRouteInfo": "AttributeRoute/{id}/GetWithActionNameInDifferentSpotInTemplate",
    "ActionParameters": [
      "id"
    ],
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": "AttributeRoute",
    "ControllerActionDescriptorActionName": "GetWithActionNameInDifferentSpotInTemplate"
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
  "DebugInfo": {
    "RawText": "",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "page": "/Index"
    },
    "AttributeRouteInfo": "",
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": "/Pages/Index.cshtml",
    "PageActionDescriptorViewEnginePath": "/Index",
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "DebugInfo": {
    "RawText": "Index",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "page": "/Index"
    },
    "AttributeRouteInfo": "Index",
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": "/Pages/Index.cshtml",
    "PageActionDescriptorViewEnginePath": "/Index",
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "DebugInfo": {
    "RawText": "PageThatThrowsException",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "page": "/PageThatThrowsException"
    },
    "AttributeRouteInfo": "PageThatThrowsException",
    "ActionParameters": [],
    "PageActionDescriptorRelativePath": "/Pages/PageThatThrowsException.cshtml",
    "PageActionDescriptorViewEnginePath": "/PageThatThrowsException",
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "HttpRouteByActionDescriptor": "",
  "DebugInfo": {
    "RawText": null,
    "RouteDiagnosticMetadata": null,
    "RouteData": {},
    "AttributeRouteInfo": null,
    "ActionParameters": null,
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "HttpRouteByActionDescriptor": "",
  "DebugInfo": {
    "RawText": "/MinimalApi/",
    "RouteDiagnosticMetadata": null,
    "RouteData": {},
    "AttributeRouteInfo": null,
    "ActionParameters": null,
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
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
  "HttpRouteByActionDescriptor": "",
  "DebugInfo": {
    "RawText": "/MinimalApi/{id}",
    "RouteDiagnosticMetadata": null,
    "RouteData": {
      "id": "123"
    },
    "AttributeRouteInfo": null,
    "ActionParameters": null,
    "PageActionDescriptorRelativePath": null,
    "PageActionDescriptorViewEnginePath": null,
    "ControllerActionDescriptorControllerName": null,
    "ControllerActionDescriptorActionName": null
  }
}
```
