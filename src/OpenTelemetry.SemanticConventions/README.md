# Semantic Conventions for OpenTelemetry .NET

This project contains the generated code for the Semantic Conventions
defined by the OpenTelemetry specification.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.SemanticConventions
```

## Generating the files

This project uses the
[Semantic Convention Generator](https://github.com/open-telemetry/build-tools/blob/main/semantic-conventions/README.md).
The folder `scripts` at the top level of the project contains
the templates and the script file used in the process.

To generate the code files, run:

```shell
./scripts/semantic-conventions/generate.sh
```

Or, with PowerShell:

```shell
./scripts/semantic-conventions/generate.ps1
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Build tools](https://github.com/open-telemetry/build-tools)
