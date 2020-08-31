# Building your own Processor

ActivityProcessor is the name used for OpenTelemetry
[SpanProcessors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-processor).
It allows hooks for Activity start and end method invocations.

[Built-in
processors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-span-processors)
that are part of this repo are used to pass activities to Exporters.

ActivityProcessors should inherit from `ActivityProcessor`.
[MyActivityProcessor](.\MyActivityProcessor.cs) demonstrates
this with an full example. [This](.\Program.cs) shows how to add the custom
ActivityProcessor to the TracerProvider. ActivityProcessors are invoked in the
same order they are added to the TracerProvider.

