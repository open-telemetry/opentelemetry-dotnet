# OpenTelemetry .Net 6  Web API Example

This example uses the new WebApplication host that ships with .Net 6
and shows how to setup

1. OpenTelemetry logging
2. OpenTelemetry tracing
3. OpenTelemetry metrics

`ResourceBuilder` is associated with OpenTelemetry to associate the
service name, version and the machine on which this program is running.

The sample rate is set to emit all the traces using `AlwaysOnSampler`.
You can try out different samplers like `TraceIdRatioBasedSampler`.

## References

* [.Net Core 3.1 Sample](https://github.com/open-telemetry/opentelemetry-dotnet/tree/2aa816314cf3f3b73d6e343b4ffeb608dd4b60af/examples/AspNetCore)
* [OpenTelemetry Project](https://opentelemetry.io/)
