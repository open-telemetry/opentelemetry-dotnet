# Metrics

## Exporter

[Exporters](./Introduction.html#exporters)

- Inherit from `OpenTelemetry.BaseExporter<Metric>`

Here is a demo exporter which simply writes metrics to the console

```{literalinclude} ../../metrics/extending-the-sdk/MyExporter.cs
:language: c#
:lines: 17-
```

Apart from the exporter itself, you should also provide extension methods like this

```{literalinclude} ../../metrics/extending-the-sdk/MyExporterExtensions.cs
:language: c#
:lines: 17-
```

This allows users to add the Exporter to the `TracerProvider` shown here

```{literalinclude} ../../metrics/extending-the-sdk/Program.cs
:language: c#
:lines: 17-
```

## Reader

TBD

## Exemplar

TBD
