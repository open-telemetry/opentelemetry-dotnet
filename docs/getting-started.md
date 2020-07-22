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
dotnet add package OpenTelemetry.Exporter.Console -v 0.2.0-alpha.419
```

Update the `Program.cs` file with the following code:

```csharp
using System.Diagnostics;
using OpenTelemetry.Trace.Configuration;

class Program
{
    static readonly ActivitySource activitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    static void Main()
    {
        using var otel = OpenTelemetrySdk.EnableOpenTelemetry(b => b
            .AddActivitySource("MyCompany.MyProduct.MyLibrary")
            .UseConsoleExporter(options => options.DisplayAsJson = true));

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

```json
{
  "Kind": "Internal",
  "OperationName": "SayHello",
  "DisplayName": "SayHello",
  "Source": {
    "Name": "MyCompany.MyProduct.MyLibrary",
    "Version": ""
  },
  "Parent": null,
  "Duration": {
    "Ticks": 19138,
    "Days": 0,
    "Hours": 0,
    "Milliseconds": 1,
    "Minutes": 0,
    "Seconds": 0,
    "TotalDays": 2.2150462962962963E-08,
    "TotalHours": 5.316111111111111E-07,
    "TotalMilliseconds": 1.9138,
    "TotalMinutes": 3.189666666666667E-05,
    "TotalSeconds": 0.0019138
  },
  "StartTimeUtc": "2020-07-22T01:21:23.6303212Z",
  "Id": "00-6cfd9c572593ea448a0e5c1cbcde3a82-b79f6fe5ba84d040-01",
  "ParentId": null,
  "RootId": "6cfd9c572593ea448a0e5c1cbcde3a82",
  "Tags": [
    {
      "Key": "foo",
      "Value": "1"
    },
    {
      "Key": "bar",
      "Value": "Hello, World!"
    }
  ],
  "Events": [],
  "Links": [],
  "Baggage": [],
  "Context": {
    "TraceId": "6cfd9c572593ea448a0e5c1cbcde3a82",
    "SpanId": "b79f6fe5ba84d040",
    "TraceFlags": "Recorded",
    "TraceState": null
  },
  "TraceStateString": null,
  "SpanId": "b79f6fe5ba84d040",
  "TraceId": "6cfd9c572593ea448a0e5c1cbcde3a82",
  "Recorded": true,
  "IsAllDataRequested": true,
  "ActivityTraceFlags": "Recorded",
  "ParentSpanId": "0000000000000000",
  "IdFormat": "W3C"
}
```

Congratulations! You are now collecting traces using OpenTelemetry.
