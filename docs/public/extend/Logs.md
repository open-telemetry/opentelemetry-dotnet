# Logs

## Exporter

[Exporter Requirements](./Introduction.md#ExporterRequirements)

Here is a demo exporter which simply writes log records to the console

```{literalinclude} ../../logs/extending-the-sdk/MyExporter.cs
:language: c#
:lines: 17-
```

Apart from the exporter itself, you should also provide extension methods like this

```{literalinclude} ../../logs/extending-the-sdk/LoggerExtensions.cs
:language: c#
:lines: 17-
```

This allows users to add the Exporter to the `OpenTelemetryLoggerOptions` shown here

```{literalinclude} ../../logs/extending-the-sdk/Program.cs
:language: c#
:lines: 17-
```

## Processor

- Inherit from `OpenTelemetry.BaseProcessor<LogRecord>`

Here is a demo processor

```{literalinclude} ../../logs/extending-the-sdk/MyProcessor.cs
:language: c#
:lines: 17-
```

## Sampler

TBD
