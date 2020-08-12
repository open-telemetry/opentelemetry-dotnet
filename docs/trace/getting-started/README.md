# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started cd getting-started dotnet run
```

You should see the following output:

```console
Hello World!
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.Console -v 0.4.0-beta.2
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the trace
output from the console.

```text
Activity.Id:
00-55c1ae1c6c23784bbc4fca463d7e15ca-bfe3220d9e489e4b-01 Activity.ParentId:
00-55c1ae1c6c23784bbc4fca463d7e15ca-0000000000000000-00 Activity.DisplayName:
SayHello Activity.Kind:        Internal Activity.StartTime:
2020-08-12T07:37:40.9681787Z Activity.Duration:    00:00:00.0047972
Activity.TagObjects:
    foo: 1 bar: Hello, World! baz: [1, 2, 3]
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program creates an `ActivitySource` which represents [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#tracer).
The activitysource instance is used to start an `Activity` which represent
[OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#span).
An OpenTelemetry
[TracerProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#tracerprovider)
configured to subscribe to the activities from the source
`MyCompany.MyProduct.MyLibrary`, and export it to `ConsoleExporter`.
`ConsoleExporter` simply displays it on the console.
