# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output Hello
cd Hello
dotnet run
```

You should see the following output:

```console
Hello World!
```

Install the
[OpenTelemetry.Exporter.Console](../src/OpenTelemetry.Exporter.Console/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the following code:

```csharp
using System.Diagnostics;
using OpenTelemetry.Trace;

class Program
{
    static readonly ActivitySource activitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    static void Main()
    {
        using var otel = OpenTelemetrySdk.CreateTracerProvider(b => b
            .AddActivitySource("MyCompany.MyProduct.MyLibrary")
            .UseConsoleExporter());

        using (var activity = activitySource.StartActivity("SayHello"))
        {
            activity?.AddTag("foo", "1");
            activity?.AddTag("bar", "Hello, World!");
        }
    }
}
```

Run the application again (using `dotnet run`) and you should see the trace
output from the console.

```text
Activity ID - 00-3ae67370100cdc44a8d461d1b2cf846f-d80f2b1ab6d3bc4b-01
Activity DisplayName - SayHello
Activity Kind - Internal
Activity StartTime - 7/24/2020 1:16:21 AM
Activity Duration - 00:00:00.0018754
Activity Tags
         foo : 1
         bar : Hello, World!
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program creates an `ActivitySource` which represents [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#tracer).
The activitysource instance is used to start an `Activity` which represent
[OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#span).
`OpenTelemetrySdk.CreateTracerProvider` sets up the OpenTelemetry Sdk, and
configures it to subscribe to the activities from the source
`MyCompany.MyProduct.MyLibrary`, and export it to `ConsoleExporter`, which
simply displays it on the console.
