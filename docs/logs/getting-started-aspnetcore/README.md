# Getting Started with OpenTelemetry .NET Logs in 5 Minutes - ASP.NET Core Application

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new web application:

```sh
dotnet new web -o aspnetcoreapp
cd aspnetcoreapp
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
and
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
```

Copy the [HelloLogs.cs](./HelloLogs.cs) and [Program.cs](./Program.cs) files to
the project folder.

Run the application again (using `dotnet run`) and then browse to the URL shown
in the console for your application (e.g. `http://localhost:5154`). You should see
the logs output from the console.

```text
LogRecord.Timestamp:               2023-09-01T23:36:24.3541759Z
LogRecord.CategoryName:            Microsoft.Hosting.Lifetime
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Content root path: {contentRoot}
LogRecord.Attributes (Key:Value):
    contentRoot: D:\repo\opentelemetry-dotnet\docs\logs\getting-started-aspnetcore\
    OriginalFormat (a.k.a Body): Content root path: {contentRoot}

Resource associated with LogRecord:
service.name: getting-started-aspnetcore
service.instance.id: d4f09b09-6613-4d09-8a2d-1508e4ce9067
telemetry.sdk.name: opentelemetry
telemetry.sdk.language: dotnet
telemetry.sdk.version: 1.6.0-rc.1.16

LogRecord.Timestamp:               2023-09-01T23:36:52.6956536Z
LogRecord.TraceId:                 abb98d33a390d2773985fd3488333e14
LogRecord.SpanId:                  46e671d30d10df22
LogRecord.TraceFlags:              None
LogRecord.CategoryName:            Program
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Food `{name}` price changed to `{price}`.
LogRecord.Attributes (Key:Value):
    name: artichoke
    price: 9.99
    OriginalFormat (a.k.a Body): Food `{name}` price changed to `{price}`.
LogRecord.EventId:                 1
LogRecord.EventName:               FoodPriceChanged
LogRecord.ScopeValues (Key:Value):
[Scope.0]:SpanId: 46e671d30d10df22
[Scope.0]:TraceId: abb98d33a390d2773985fd3488333e14
[Scope.0]:ParentId: 0000000000000000
[Scope.1]:ConnectionId: 0HMTB8H9151CI
[Scope.2]:RequestId: 0HMTB8H9151CI:00000002
[Scope.2]:RequestPath: /

Resource associated with LogRecord:
service.name: getting-started-aspnetcore
service.instance.id: d4f09b09-6613-4d09-8a2d-1508e4ce9067
telemetry.sdk.name: opentelemetry
telemetry.sdk.language: dotnet
telemetry.sdk.version: 1.6.0-rc.1.16
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program has added OpenTelemetry as a [logging
provider](https://learn.microsoft.com/dotnet/core/extensions/logging-providers)
to the existing logging pipeline. OpenTelemetry SDK is then configured with a
`ConsoleExporter` to export the logs to the console. In addition,
`OpenTelemetryLoggerOptions.IncludeScopes` is enabled so the logs will include
the [log
scopes](https://learn.microsoft.com/aspnet/core/fundamentals/logging/#log-scopes).
From the console output we can see the log scopes that are coming from the
ASP.NET Core framework, and we can see logs from both our logger and the ASP.NET
Core framework loggers.

## Learn more

* [Compile-time logging source
  generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
* [Customizing the OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
