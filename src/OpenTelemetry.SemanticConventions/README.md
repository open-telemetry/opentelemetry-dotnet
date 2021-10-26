# Semantic Conventions for OpenTelemetry .NET

This project contains the generated code for the Semantic Conventions
defined by the OpenTelemetry specification.

## Installation

```shell
dotnet add package OpenTelemetry.SemanticConventions
```

## Generating the files

This project uses the
[Semantic Convention Generator](https://github.com/open-telemetry/build-tools/blob/main/semantic-conventions/README.md).
The folder `scripts` at the top level of the project contains
the templates and the script file used in the process.

To generate the code files, run:

```shell
./scripts/semantic-convetion/generate.sh
```

Or, with PowerShell:

```shell
./scripts/semantic-convetion/generate.ps1
```

### dotnet-format

The script installs and runs the [dotnet format](https://github.com/dotnet/format)
tool after the `.cs` files are generated. It will apply fixes for whitespaces,
code style and analyzer warnings.

### Updating PublicAPI files

Because the script runs `dotnet-format`, the PublicAPI files **will be
updated automatically**, as the tool fixes warnings for analyzers
([RS0016](https://github.com/dotnet/roslyn-analyzers/issues/3229)).

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Build tools](https://github.com/open-telemetry/build-tools)
