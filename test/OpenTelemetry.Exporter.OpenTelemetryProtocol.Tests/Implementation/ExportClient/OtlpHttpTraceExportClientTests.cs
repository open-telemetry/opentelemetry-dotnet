// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if !NET6_0_OR_GREATER
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpHttpTraceExportClientTests
{
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();

    static OtlpHttpTraceExportClientTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
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

        var client = new OtlpHttpTraceExportClient(options, options.HttpClientFactory());

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
        var evenTags = new[] { new KeyValuePair<string, object>("k0", "v0") };
        var oddTags = new[] { new KeyValuePair<string, object>("k1", "v1") };
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

        var testHttpHandler = new TestHttpMessageHandler();

        var httpRequestContent = Array.Empty<byte>();

        var httpClient = new HttpClient(testHttpHandler);

        var exportClient = new OtlpHttpTraceExportClient(options, httpClient);

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
        var processor = new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
        const int numOfSpans = 10;
        bool isEven;
        for (var i = 0; i < numOfSpans; i++)
        {
            isEven = i % 2 == 0;
            var source = sources[i % 2];
            var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
            var activityTags = isEven ? evenTags : oddTags;

            using Activity activity = source.StartActivity($"span-{i}", activityKind, parentContext: default, activityTags);
            processor.OnEnd(activity);
        }

        processor.Shutdown();

        var batch = new Batch<Activity>([.. exportedItems], exportedItems.Count);
        RunTest(batch);

        void RunTest(Batch<Activity> batch)
        {
            var deadlineUtc = DateTime.UtcNow.AddMilliseconds(httpClient.Timeout.TotalMilliseconds);
            var request = new OtlpCollector.ExportTraceServiceRequest();

            request.AddBatch(DefaultSdkLimitOptions, resourceBuilder.Build().ToOtlpResource(), batch);

            // Act
            var result = exportClient.SendExportRequest(request, deadlineUtc);

            var httpRequest = testHttpHandler.HttpRequestMessage;

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(httpRequest);
            Assert.Equal(HttpMethod.Post, httpRequest.Method);
            Assert.Equal("http://localhost:4317/", httpRequest.RequestUri.AbsoluteUri);
            Assert.Equal(OtlpExporterOptions.StandardHeaders.Length + 2, httpRequest.Headers.Count());
            Assert.Contains(httpRequest.Headers, h => h.Key == header1.Name && h.Value.First() == header1.Value);
            Assert.Contains(httpRequest.Headers, h => h.Key == header2.Name && h.Value.First() == header2.Value);

            for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
            {
                Assert.Contains(httpRequest.Headers, entry => entry.Key == OtlpExporterOptions.StandardHeaders[i].Key && entry.Value.First() == OtlpExporterOptions.StandardHeaders[i].Value);
            }

            Assert.NotNull(testHttpHandler.HttpRequestContent);
            Assert.IsType<OtlpHttpTraceExportClient.ExportRequestContent>(httpRequest.Content);
            Assert.Contains(httpRequest.Content.Headers, h => h.Key == "Content-Type" && h.Value.First() == OtlpHttpTraceExportClient.MediaContentType);

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
                Assert.Contains(resourceSpan.Resource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service:"));
            }
        }
    }
}
