# Resources

Quick links:

* [Building your own resource detector](#resource-detector)

## Resource Detector

OpenTelemetry .NET SDK provides a resource detector for detecting resource
information from the `OTEL_RESOURCE_ATTRIBUTES` and `OTEL_SERVICE_NAME`
environment variables.

Custom resource detectors can be implemented:

* ResourceDetectors should inherit from
  `OpenTelemetry.Resources.IResourceDetector`, (which belongs to the
  [OpenTelemetry](../../src/OpenTelemetry/README.md) package), and implement
  the `Detect` method.

A demo ResourceDetector is shown [here](../trace/extending-the-sdk/MyResourceDetector.cs).
