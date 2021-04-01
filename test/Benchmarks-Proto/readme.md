# Benchmark Proto

Benchmarking Encoding/Decoding of OTLP Proto between version 0.4 and 0.8.

## Notes

- opentelemetry4 folder contains copy of OTLP Proto v0.4.0.

  - Changed package namespace to opentelemetry4.proto.* to allow both version of
  Proto to be in this project.

- opentelemetry folder contains copy of OTLP Proto v0.8.0.

  - Modified **metrics/experimental/metrics_config_service.proto** to work
  around compiler error name conflict with "Equals" while using grpc.tools.

  ``` protobuf
    message Pattern {
      oneof match {
        string equalsZZ = 1;    // <<<=== changed from "equals" to "equalsZZ"
        string starts_with = 2;
      }
    }

  ```

## Running

``` cmd
> dotnet run -c Release
```
