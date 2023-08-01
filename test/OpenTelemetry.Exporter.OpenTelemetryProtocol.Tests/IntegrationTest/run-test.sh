#!/bin/bash

# Trust the self-signed certificated used by the collector
cp /cfg/otel-collector.crt /usr/local/share/ca-certificates/
update-ca-certificates --verbose

dotnet test OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll --TestCaseFilter:CategoryName=CollectorIntegrationTests --logger "console;verbosity=detailed"
