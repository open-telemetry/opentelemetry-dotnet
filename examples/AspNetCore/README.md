# OpenTelemetry ASP.NET Core 7 Web API Example

This example uses the new WebApplication host that ships with .NET 7
and shows how to setup

1. OpenTelemetry logging
2. OpenTelemetry metrics
3. OpenTelemetry tracing

`ResourceBuilder` is associated with OpenTelemetry to associate the
service name, version and the machine on which this program is running.

The sample rate is set to emit all the traces using `AlwaysOnSampler`.
You can try out different samplers like `TraceIdRatioBasedSampler`.

## References

* [ASP.NET Core 3.1 Example](https://github.com/open-telemetry/opentelemetry-dotnet/tree/98cb28974af43fc893ab80a8cead6e2d4163e144/examples/AspNetCore)
* [OpenTelemetry Project](https://opentelemetry.io/)
