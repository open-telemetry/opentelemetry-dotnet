# Building your own Processor

ActivityProcessor is the name used for OpenTelemetry
[SpanProcessors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-processor).
It allows hooks for Activity start and end method invocations.

[Built-in
processors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-span-processors)
that are part of this repo are used to pass activities to Exporters.

ActivityProcessors can also be used to enrich an Activity with additional
information. For example, it can be used to add additional Tags to an
`Activity`.

ActivityProcessors should inherit from `ActivityProcessor`.
[MyEnrichmentActivityProcessor](.\MyEnrichmentActivityProcessor.cs) shows how to
add more Tags to an activity. [This](.\Program.cs) shows how to add the custom
ActivityProcessor to the TracerProvider. ActivityProcessors are invoked in the
same order they are added to the TracerProvider. Hence it is important to add
this processor before any exporting processors, otherwise the enrichments done
by the processor will not be reflected in exported data.
