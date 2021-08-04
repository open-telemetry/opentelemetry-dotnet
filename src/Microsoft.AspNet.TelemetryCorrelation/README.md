# Telemetry correlation http module

[![NuGet](https://img.shields.io/nuget/v/Microsoft.AspNet.TelemetryCorrelation.svg)](https://www.nuget.org/packages/Microsoft.AspNet.TelemetryCorrelation/)

Telemetry correlation http module enables cross tier telemetry tracking.

## Usage

1. Install NuGet for your app.
2. Enable diagnostics source listener using code below. Note, some
   telemetry vendors like Azure Application Insights will enable it
   automatically.

    ``` csharp
    public class NoopDiagnosticsListener : IObserver<KeyValuePair<string, object>>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(KeyValuePair<string, object> evnt)
        {
        }
    }

    public class NoopSubscriber : IObserver<DiagnosticListener>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.AspNet.TelemetryCorrelation" || listener.Name == "System.Net.Http" )
            {
                listener.Subscribe(new NoopDiagnosticsListener());
            }
        }
    }
    ```
3. Double check that http module was registered in `web.config` for your
   app.

Once enabled - this http module will:

- Reads correlation http headers
- Start/Stops Activity for the http request
- Ensure the Activity ambient state is transferred thru the IIS
  callbacks

See http protocol [specifications][http-protocol-specification] for
details.

This http module is used by Application Insights. See
[documentation][usage-in-ai-docs] and [code][usage-in-ai-code].

[http-protocol-specification]: https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HttpCorrelationProtocol.md
[usage-in-ai-docs]: https://docs.microsoft.com/azure/application-insights/application-insights-correlation
[usage-in-ai-code]: https://github.com/Microsoft/ApplicationInsights-dotnet-server
