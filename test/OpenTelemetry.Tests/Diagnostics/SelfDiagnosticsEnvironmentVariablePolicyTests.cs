// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsEnvironmentVariablePolicyTests
{
    private const string Redacted = SelfDiagnosticsEnvironmentVariablePolicy.RedactedValue;

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_HEADERS")]
    [InlineData("OTEL_EXPORTER_OTLP_LOGS_HEADERS")]
    [InlineData("OTEL_EXPORTER_OTLP_METRICS_HEADERS")]
    [InlineData("OTEL_EXPORTER_OTLP_TRACES_HEADERS")]
    public void CredentialCarryingVariables_AreRedactedWholesale(string name)
        => Assert.Equal(
            Redacted,
            SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(name, "Authorization=Bearer abc123"));

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_CERTIFICATE")]
    [InlineData("OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE")]
    [InlineData("OTEL_EXPORTER_OTLP_CLIENT_KEY")]
    public void CertificateVariables_KeepTheirPath(string name)
    {
        // These are documented as paths to PEM files, and the path is exactly what a support
        // bundle needs in order to diagnose a failed handshake or a missing file.
        const string Path = "/etc/otel/certs/client.pem";

        Assert.Equal(Path, SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(name, Path));
    }

    [Theory]
    [InlineData("OTEL_EXPORTER_OTLP_CLIENT_KEY")]
    [InlineData("OTEL_SERVICE_NAME")]
    public void InlineKeyMaterial_IsRedacted(string name)
    {
        // Guards the misconfiguration where the material itself was pasted into the variable
        // instead of a path to it. No path or scalar setting opens with PEM armour.
        const string Pem = "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADAN\n-----END PRIVATE KEY-----";

        Assert.Equal(Redacted, SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(name, Pem));
    }

    [Fact]
    public void MultiLineValue_IsRedacted()
        => Assert.Equal(
            Redacted,
            SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue("OTEL_SERVICE_NAME", "first\r\nsecond"));

    [Fact]
    public void ResourceAttributes_KeepWellKnownKeysAndRedactTheRest()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_RESOURCE_ATTRIBUTES",
            "service.name=checkout,service.version=1.2.3,deployment.environment=prod,tenant.api.token=s3cret");

        Assert.Equal(
            $"service.name=checkout,service.version=1.2.3,deployment.environment=prod,tenant.api.token={Redacted}",
            value);
    }

    [Fact]
    public void ResourceAttributes_KeepPrefixedWellKnownKeys()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_RESOURCE_ATTRIBUTES",
            "k8s.namespace.name=payments,host.name=node-7,cloud.region=eu-west-1");

        Assert.Equal("k8s.namespace.name=payments,host.name=node-7,cloud.region=eu-west-1", value);
    }

    [Fact]
    public void ResourceAttributes_MalformedPairsAreRedactedButPreserveShape()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_RESOURCE_ATTRIBUTES",
            "service.name=checkout,noequalssign,,service.version=2");

        Assert.Equal($"service.name=checkout,{Redacted},,service.version=2", value);
    }

    [Fact]
    public void ResourceAttributes_ValueContainingEqualsIsNotTruncated()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_RESOURCE_ATTRIBUTES",
            "service.name=a=b=c");

        Assert.Equal("service.name=a=b=c", value);
    }

    [Fact]
    public void ResourceAttributes_KeysAreMatchedIgnoringSurroundingWhitespace()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_RESOURCE_ATTRIBUTES",
            "service.name =checkout");

        Assert.Equal("service.name =checkout", value);
    }

    [Fact]
    public void EndpointVariables_AreReducedToTheirAuthority()
    {
        var value = SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue(
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "https://user:password@collector.example.com:4317/v1/traces?token=abc");

        Assert.Equal("https://collector.example.com:4317", value);
    }

    [Fact]
    public void EndpointVariable_ThatIsNotAUri_IsRedacted()
        => Assert.Equal(
            Redacted,
            SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue("OTEL_EXPORTER_OTLP_ENDPOINT", "not a URI"));

    [Fact]
    public void NonSensitiveVariable_IsReturnedVerbatim()
        => Assert.Equal(
            "checkout-service",
            SelfDiagnosticsEnvironmentVariablePolicy.GetDisplayValue("OTEL_SERVICE_NAME", "checkout-service"));

    [Theory]
    [InlineData("OTEL_SERVICE_NAME", true)]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", true)]
    [InlineData("OTEL_DOTNET_AUTO_TRACES_MYINTEGRATION_INSTRUMENTATION_ENABLED", true)]
    [InlineData("OTEL_DOTNET_AUTO_AZUREAPPSERVICE_RESOURCE_DETECTOR_ENABLED", true)]
    [InlineData("OTEL_COLLECTOR_ONLY_SETTING", false)]
    public void IsAllowed_MatchesAllowlistAndDynamicPatterns(string name, bool expected)
        => Assert.Equal(expected, SelfDiagnosticsEnvironmentVariablePolicy.IsAllowed(name));
}
