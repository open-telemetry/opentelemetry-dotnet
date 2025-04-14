// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpSpecConfigDefinitionTests : IEnumerable<object[]>
{
    internal static TestData DefaultData { get; } = new TestData(
        OtlpExporterOptionsConfigurationType.Default,
        OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName,
        "http://default_endpoint/",
        appendSignalPathToEndpoint: true,
        OtlpSpecConfigDefinitions.DefaultHeadersEnvVarName,
        "key1=value1",
        OtlpSpecConfigDefinitions.DefaultTimeoutEnvVarName,
        "1001",
        OtlpSpecConfigDefinitions.DefaultProtocolEnvVarName,
        "http/protobuf");

    internal static TestData LoggingData { get; } = new TestData(
        OtlpExporterOptionsConfigurationType.Logs,
        OtlpSpecConfigDefinitions.LogsEndpointEnvVarName,
        "http://logs_endpoint/",
        appendSignalPathToEndpoint: false,
        OtlpSpecConfigDefinitions.LogsHeadersEnvVarName,
        "key2=value2",
        OtlpSpecConfigDefinitions.LogsTimeoutEnvVarName,
        "1002",
        OtlpSpecConfigDefinitions.LogsProtocolEnvVarName,
        "http/protobuf");

    internal static MetricsTestData MetricsData { get; } = new MetricsTestData(
        OtlpSpecConfigDefinitions.MetricsEndpointEnvVarName,
        "http://metrics_endpoint/",
        appendSignalPathToEndpoint: false,
        OtlpSpecConfigDefinitions.MetricsHeadersEnvVarName,
        "key3=value3",
        OtlpSpecConfigDefinitions.MetricsTimeoutEnvVarName,
        "1003",
        OtlpSpecConfigDefinitions.MetricsProtocolEnvVarName,
        "http/protobuf",
        OtlpSpecConfigDefinitions.MetricsTemporalityPreferenceEnvVarName,
        "Delta");

    internal static TestData TracingData { get; } = new TestData(
        OtlpExporterOptionsConfigurationType.Traces,
        OtlpSpecConfigDefinitions.TracesEndpointEnvVarName,
        "http://traces_endpoint/",
        appendSignalPathToEndpoint: false,
        OtlpSpecConfigDefinitions.TracesHeadersEnvVarName,
        "key4=value4",
        OtlpSpecConfigDefinitions.TracesTimeoutEnvVarName,
        "1004",
        OtlpSpecConfigDefinitions.TracesProtocolEnvVarName,
        "http/protobuf");

    [Fact]
    public void VerifyKeyNamesMatchSpec()
    {
        Assert.Equal("OTEL_EXPORTER_OTLP_ENDPOINT", DefaultData.EndpointKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS", DefaultData.HeadersKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TIMEOUT", DefaultData.TimeoutKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_PROTOCOL", DefaultData.ProtocolKeyName);

        Assert.Equal("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT", LoggingData.EndpointKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_LOGS_HEADERS", LoggingData.HeadersKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_LOGS_TIMEOUT", LoggingData.TimeoutKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_LOGS_PROTOCOL", LoggingData.ProtocolKeyName);

        Assert.Equal("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", MetricsData.EndpointKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_METRICS_HEADERS", MetricsData.HeadersKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_METRICS_TIMEOUT", MetricsData.TimeoutKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_METRICS_PROTOCOL", MetricsData.ProtocolKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE", MetricsData.TemporalityKeyName);

        Assert.Equal("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", TracingData.EndpointKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TRACES_HEADERS", TracingData.HeadersKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TRACES_TIMEOUT", TracingData.TimeoutKeyName);
        Assert.Equal("OTEL_EXPORTER_OTLP_TRACES_PROTOCOL", TracingData.ProtocolKeyName);
    }

    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[]
        {
            DefaultData,
        };

        yield return new object[]
        {
            LoggingData,
        };

        yield return new object[]
        {
            MetricsData,
        };

        yield return new object[]
        {
            TracingData,
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    internal static IConfiguration ToConfiguration()
    {
        var configBuilder = new ConfigurationBuilder();

        DefaultData.AddToConfiguration(configBuilder);
        LoggingData.AddToConfiguration(configBuilder);
        MetricsData.AddToConfiguration(configBuilder);
        TracingData.AddToConfiguration(configBuilder);

        return configBuilder.Build();
    }

    internal static void SetEnvVars()
    {
        DefaultData.SetEnvVars();
        LoggingData.SetEnvVars();
        MetricsData.SetEnvVars();
        TracingData.SetEnvVars();
    }

    internal static void ClearEnvVars()
    {
        DefaultData.ClearEnvVars();
        LoggingData.ClearEnvVars();
        MetricsData.ClearEnvVars();
        TracingData.ClearEnvVars();
    }

    internal class TestData
    {
        public TestData(
            OtlpExporterOptionsConfigurationType configurationType,
            string endpointKeyName,
            string endpointValue,
            bool appendSignalPathToEndpoint,
            string headersKeyName,
            string headersValue,
            string timeoutKeyName,
            string timeoutValue,
            string protocolKeyName,
            string protocolValue)
        {
            this.ConfigurationType = configurationType;
            this.EndpointKeyName = endpointKeyName;
            this.EndpointValue = endpointValue;
            this.AppendSignalPathToEndpoint = appendSignalPathToEndpoint;
            this.HeadersKeyName = headersKeyName;
            this.HeadersValue = headersValue;
            this.TimeoutKeyName = timeoutKeyName;
            this.TimeoutValue = timeoutValue;
            this.ProtocolKeyName = protocolKeyName;
            this.ProtocolValue = protocolValue;
        }

        public OtlpExporterOptionsConfigurationType ConfigurationType { get; }

        public string EndpointKeyName { get; }

        public string EndpointValue { get; }

        public bool AppendSignalPathToEndpoint { get; }

        public string HeadersKeyName { get; }

        public string HeadersValue { get; }

        public string TimeoutKeyName { get; }

        public string TimeoutValue { get; }

        public string ProtocolKeyName { get; }

        public string ProtocolValue { get; }

        public IConfiguration ToConfiguration()
        {
            return this.AddToConfiguration(new ConfigurationBuilder()).Build();
        }

        public ConfigurationBuilder AddToConfiguration(ConfigurationBuilder configurationBuilder)
        {
            Dictionary<string, string?> dictionary = new();

            dictionary[this.EndpointKeyName] = this.EndpointValue;
            dictionary[this.HeadersKeyName] = this.HeadersValue;
            dictionary[this.TimeoutKeyName] = this.TimeoutValue;
            dictionary[this.ProtocolKeyName] = this.ProtocolValue;

            this.OnAddToDictionary(dictionary);

            configurationBuilder.AddInMemoryCollection(dictionary);

            return configurationBuilder;
        }

        public void SetEnvVars()
        {
            Environment.SetEnvironmentVariable(this.EndpointKeyName, this.EndpointValue);
            Environment.SetEnvironmentVariable(this.HeadersKeyName, this.HeadersValue);
            Environment.SetEnvironmentVariable(this.TimeoutKeyName, this.TimeoutValue);
            Environment.SetEnvironmentVariable(this.ProtocolKeyName, this.ProtocolValue);

            this.OnSetEnvVars();
        }

        public void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(this.EndpointKeyName, null);
            Environment.SetEnvironmentVariable(this.HeadersKeyName, null);
            Environment.SetEnvironmentVariable(this.TimeoutKeyName, null);
            Environment.SetEnvironmentVariable(this.ProtocolKeyName, null);

            this.OnClearEnvVars();
        }

        public void AssertMatches(IOtlpExporterOptions otlpExporterOptions)
        {
            Assert.Equal(new Uri(this.EndpointValue), otlpExporterOptions.Endpoint);
            Assert.Equal(this.HeadersValue, otlpExporterOptions.Headers);
            Assert.Equal(int.Parse(this.TimeoutValue), otlpExporterOptions.TimeoutMilliseconds);

            if (!OtlpExportProtocolParser.TryParse(this.ProtocolValue, out var protocol))
            {
                Assert.Fail();
            }

            Assert.Equal(protocol, otlpExporterOptions.Protocol);

            var concreteOptions = otlpExporterOptions as OtlpExporterOptions;
            Assert.NotNull(concreteOptions);
            Assert.Equal(this.AppendSignalPathToEndpoint, concreteOptions.AppendSignalPathToEndpoint);
        }

        protected virtual void OnSetEnvVars()
        {
        }

        protected virtual void OnClearEnvVars()
        {
        }

        protected virtual void OnAddToDictionary(Dictionary<string, string?> dictionary)
        {
        }
    }

    internal sealed class MetricsTestData : TestData
    {
        public MetricsTestData(
            string endpointKeyName,
            string endpointValue,
            bool appendSignalPathToEndpoint,
            string headersKeyName,
            string headersValue,
            string timeoutKeyName,
            string timeoutValue,
            string protocolKeyName,
            string protocolValue,
            string temporalityKeyName,
            string temporalityValue)
            : base(
                  OtlpExporterOptionsConfigurationType.Metrics,
                  endpointKeyName,
                  endpointValue,
                  appendSignalPathToEndpoint,
                  headersKeyName,
                  headersValue,
                  timeoutKeyName,
                  timeoutValue,
                  protocolKeyName,
                  protocolValue)
        {
            this.TemporalityKeyName = temporalityKeyName;
            this.TemporalityValue = temporalityValue;
        }

        public string TemporalityKeyName { get; }

        public string TemporalityValue { get; }

        public void AssertMatches(MetricReaderOptions metricReaderOptions)
        {
#if NET
            Assert.Equal(Enum.Parse<MetricReaderTemporalityPreference>(this.TemporalityValue), metricReaderOptions.TemporalityPreference);
#else
            Assert.Equal(Enum.Parse(typeof(MetricReaderTemporalityPreference), this.TemporalityValue), metricReaderOptions.TemporalityPreference);
#endif
        }

        protected override void OnSetEnvVars()
        {
            Environment.SetEnvironmentVariable(this.TemporalityKeyName, this.TemporalityValue);
        }

        protected override void OnClearEnvVars()
        {
            Environment.SetEnvironmentVariable(this.TemporalityKeyName, null);
        }

        protected override void OnAddToDictionary(Dictionary<string, string?> dictionary)
        {
            dictionary[this.TemporalityKeyName] = this.TemporalityValue;
        }
    }
}
