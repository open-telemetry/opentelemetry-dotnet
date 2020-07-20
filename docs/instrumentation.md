# OpenTelemetry .NET Instrumentation Library

[Instrumented
library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumented-library)
denotes the library for which telemetry signals are gathered. [Instrumentation
library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library)
refers to the library that provides instrumentation for a given instrumented
library. Instrumented Library and Instrumentation Library may be the same
library if it has built-in OpenTelemetry instrumentation.

Instrumentation libraries were previously referred to as OpenTelemetry
Adapters. Other terms like `Auto-Collectors` were also in use previously.

OpenTelemetry .NET currently provides the following Instrumentation Libraries.

1. HttpClient (.NET Core)
2. [HttpClient (.NET Framework)](https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest)
3. [ASP.NET](https://docs.microsoft.com/en-us/aspnet/overview)
4. [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core)
5. SQL Client
   1. [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient)
   2. [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
6. [gRPC for .NET](https://github.com/grpc/grpc-dotnet)
7. [StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/)

## Using HttpClient (.NET Core) Instrumentation
