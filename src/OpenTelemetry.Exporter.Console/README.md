# OpenTelemetry.Exporter.Console

This is a simple exporter that that JSON serializes collected spans and prints them to the Console and is intended to be used during learning how spans are creating and exported.

**Note** This is not intended as a production tool

## Options

| Parameter | Description | Default |
| - | - | - |
| `-p` or `--pretty` | Specify if the output should be pretty printed | `false` |

## Examples

The default output of the test will be compressed JSON.

`dotnet run -p samples/Exporters/Exporters.csproj console`

To run the test with expanded JSON, you can use the `--pretty` flag like this.

`dotnet run -p samples/Exporters/Exporters.csproj console --pretty`
