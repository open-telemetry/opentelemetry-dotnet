#!/bin/bash
set -e

# Trust the self-signed certificate used by the collector
cp /cfg/certs/otel-test-ca-cert.pem /usr/local/share/ca-certificates/otel-test-ca-cert.crt
update-ca-certificates --verbose

dotnet test OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.dll --TestCaseFilter:CategoryName=CollectorIntegrationTests --logger "console;verbosity=detailed"
