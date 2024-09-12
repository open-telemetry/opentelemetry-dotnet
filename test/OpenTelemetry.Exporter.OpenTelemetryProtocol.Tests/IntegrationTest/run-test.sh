#!/bin/bash
set -e

# Trust the self-signed certificated used by the collector
cp /obj/Debug/otel-test-ca-cert.pem /usr/local/share/ca-certificates/
update-ca-certificates --verbose

dotnet test OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll --TestCaseFilter:CategoryName=CollectorIntegrationTests --logger "console;verbosity=detailed"
