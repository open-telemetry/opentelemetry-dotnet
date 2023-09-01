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
LogRecord.Timestamp:               2023-09-01T22:55:50.6389757Z
LogRecord.TraceId:                 8fa0ec5519c9bd5a498978c089a78182
LogRecord.SpanId:                  b888feebe74e9685
LogRecord.TraceFlags:              None
LogRecord.CategoryName:            Program
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Hello, world!
LogRecord.Attributes (Key:Value):
    OriginalFormat (a.k.a Body): Hello, world!
LogRecord.EventId:                 1
LogRecord.EventName:               SayHello
LogRecord.ScopeValues (Key:Value):
[Scope.0]:SpanId: b888feebe74e9685
[Scope.0]:TraceId: 8fa0ec5519c9bd5a498978c089a78182
[Scope.0]:ParentId: 0000000000000000
[Scope.1]:ConnectionId: 0HMTB7Q8V1EHB
[Scope.2]:RequestId: 0HMTB7Q8V1EHB:0000000F
[Scope.2]:RequestPath: /

Resource associated with LogRecord:
service.name: getting-started-aspnetcore
service.instance.id: 144fa25b-b25c-4829-b8f6-d465df52fdaa
telemetry.sdk.name: opentelemetry
telemetry.sdk.language: dotnet
telemetry.sdk.version: 1.6.0-rc.1.15
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program has configured the ASP.NET Core logging pipeline by enabling
OpenTelemetry SDK and the `ConsoleExporter`. `ConsoleExporter` simply displays
it on the console.

## Learn more

* [Compile-time logging source
  generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
* [Customizing the OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
