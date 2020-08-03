using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

class Program
{
    static readonly ActivitySource activitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    static void Main()
    {
        using var otel = Sdk.CreateTracerProvider(b => b
            .AddActivitySource("MyCompany.MyProduct.MyLibrary")
            .UseConsoleExporter());

        using (var activity = activitySource.StartActivity("SayHello"))
        {
            activity?.AddTag("foo", "1");
            activity?.AddTag("bar", "Hello, World!");
        }
    }
}
