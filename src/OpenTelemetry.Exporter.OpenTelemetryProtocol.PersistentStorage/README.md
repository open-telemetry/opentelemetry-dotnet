# OTLP Exporter Persistent Storage for OpenTelemetry .NET

## Usage

```csharp
var storagePath = Path.GetTempPath();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("ActivitySourceName")
    .AddOtlpExporterWithPersistentStorage(
        opt => {},
        serviceProvider => new FileBlobProvider(storagePath)))
    .Build();
```
