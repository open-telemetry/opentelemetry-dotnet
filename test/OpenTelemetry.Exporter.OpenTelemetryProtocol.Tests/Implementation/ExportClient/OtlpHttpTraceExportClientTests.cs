// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if !NET
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public sealed class OtlpHttpTraceExportClientTests : IDisposable
{
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();

    private readonly ActivityListener activityListener;

    static OtlpHttpTraceExportClientTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public OtlpHttpTraceExportClientTests()
    {
        this.activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(this.activityListener);
    }

    public void Dispose()
    {
        this.activityListener.Dispose();
    }

    [Fact]
    public void NewOtlpHttpTraceExportClient_OtlpExporterOptions_ExporterHasCorrectProperties()
    {
        var header1 = new { Name = "hdr1", Value = "val1" };
        var header2 = new { Name = "hdr2", Value = "val2" };

        var options = new OtlpExporterOptions
        {
            Headers = $"{header1.Name}={header1.Value}, {header2.Name} = {header2.Value}",
        };

        var client = new OtlpHttpExportClient(options, options.HttpClientFactory(), "/v1/traces");

        Assert.NotNull(client.HttpClient);

        Assert.Equal(2 + OtlpExporterOptions.StandardHeaders.Length, client.Headers.Count);
        Assert.Contains(client.Headers, kvp => kvp.Key == header1.Name && kvp.Value == header1.Value);
        Assert.Contains(client.Headers, kvp => kvp.Key == header2.Name && kvp.Value == header2.Value);

        for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
        {
            Assert.Contains(client.Headers, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key && entry.Value == OtlpExporterOptions.StandardHeaders[i].Value);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_ExportTraceServiceRequest_SendsCorrectHttpRequest(bool includeServiceNameInResource)
    {
        // Arrange
        var evenTags = new[] { new KeyValuePair<string, object?>("k0", "v0") };
        var oddTags = new[] { new KeyValuePair<string, object?>("k1", "v1") };
        var sources = new[]
        {
            new ActivitySource("even", "2.4.6"),
            new ActivitySource("odd", "1.3.5"),
        };
        var header1 = new { Name = "hdr1", Value = "val1" };
        var header2 = new { Name = "hdr2", Value = "val2" };

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4317"),
            Headers = $"{header1.Name}={header1.Value}, {header2.Name} = {header2.Value}",
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var testHttpHandler = new TestHttpMessageHandler();
#pragma warning restore CA2000 // Dispose objects before losing scope

        using var httpClient = new HttpClient(testHttpHandler);

        var exportClient = new OtlpHttpExportClient(options, httpClient, string.Empty);

        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (includeServiceNameInResource)
        {
            resourceBuilder.AddAttributes(
                new List<KeyValuePair<string, object>>
                {
                    new(ResourceSemanticConventions.AttributeServiceName, "service_name"),
                    new(ResourceSemanticConventions.AttributeServiceNamespace, "ns_1"),
                });
        }

        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(sources[0].Name)
            .AddSource(sources[1].Name);

        using var openTelemetrySdk = builder.Build();

        var exportedItems = new List<Activity>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        var processor = new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope
        const int numOfSpans = 10;
        bool isEven;
        for (var i = 0; i < numOfSpans; i++)
        {
            isEven = i % 2 == 0;
            var source = sources[i % 2];
            var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
            var activityTags = isEven ? evenTags : oddTags;

            using Activity? activity = source.StartActivity($"span-{i}", activityKind, parentContext: default, activityTags);
            Assert.NotNull(activity);
            processor.OnEnd(activity);
        }

        processor.Shutdown();

        var batch = new Batch<Activity>([.. exportedItems], exportedItems.Count);
        RunTest(batch);

        void RunTest(Batch<Activity> batch)
        {
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(httpClient.Timeout.TotalMilliseconds);
            var request = new OtlpCollector.ExportTraceServiceRequest();

            var (buffer, contentLength) = CreateTraceExportRequest(DefaultSdkLimitOptions, batch, resourceBuilder.Build());

            // Act
            var result = exportClient.SendExportRequest(buffer, contentLength, deadlineUtc);

            var httpRequest = testHttpHandler.HttpRequestMessage;

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(httpRequest);
            Assert.Equal(HttpMethod.Post, httpRequest.Method);
            Assert.NotNull(httpRequest.RequestUri);
            Assert.Equal("http://localhost:4317/", httpRequest.RequestUri.AbsoluteUri);
            Assert.Equal(OtlpExporterOptions.StandardHeaders.Length + 2, httpRequest.Headers.Count());
            Assert.Contains(httpRequest.Headers, h => h.Key == header1.Name && h.Value.First() == header1.Value);
            Assert.Contains(httpRequest.Headers, h => h.Key == header2.Name && h.Value.First() == header2.Value);

            for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
            {
                Assert.Contains(httpRequest.Headers, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key && entry.Value.First() == OtlpExporterOptions.StandardHeaders[i].Value);
            }

            Assert.NotNull(testHttpHandler.HttpRequestContent);

            // TODO: Revisit once the HttpClient part is overridden.
            // Assert.IsType<ProtobufOtlpHttpExportClient.ExportRequestContent>(httpRequest.Content);
            Assert.NotNull(httpRequest.Content);
            Assert.Contains(httpRequest.Content.Headers, h => h.Key == "Content-Type" && h.Value.First() == OtlpHttpExportClient.MediaHeaderValue.ToString());

            var exportTraceRequest = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(testHttpHandler.HttpRequestContent);
            Assert.NotNull(exportTraceRequest);
            Assert.Single(exportTraceRequest.ResourceSpans);

            var resourceSpan = exportTraceRequest.ResourceSpans.First();
            if (includeServiceNameInResource)
            {
                Assert.Contains(resourceSpan.Resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service_name");
                Assert.Contains(resourceSpan.Resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns_1");
            }
            else
            {
                Assert.DoesNotContain(resourceSpan.Resource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName);
            }
        }
    }

    private static (byte[] Buffer, int ContentLength) CreateTraceExportRequest(SdkLimitOptions sdkOptions, in Batch<Activity> batch, Resource resource)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(ref buffer, 0, sdkOptions, resource, batch);
        return (buffer, writePosition);
    }
}
