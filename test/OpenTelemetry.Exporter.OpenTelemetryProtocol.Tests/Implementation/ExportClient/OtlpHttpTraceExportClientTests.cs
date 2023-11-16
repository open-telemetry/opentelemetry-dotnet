// <copyright file="OtlpHttpTraceExportClientTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
#if !NET6_0_OR_GREATER
using System.Net.Http;
#endif
using Moq;
using Moq.Protected;
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

        var httpHandlerMock = new Mock<HttpMessageHandler>();

        HttpRequestMessage httpRequest = null;
        var httpRequestContent = Array.Empty<byte>();

        httpHandlerMock.Protected()
#if NET6_0_OR_GREATER
            .Setup<HttpResponseMessage>("Send", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken token) =>
            {
                return new HttpResponseMessage();
            })
            .Callback<HttpRequestMessage, CancellationToken>(async (r, ct) =>
            {
                httpRequest = r;

                // We have to capture content as it can't be accessed after request is disposed inside of SendExportRequest method
                httpRequestContent = await r.Content.ReadAsByteArrayAsync(ct);
            })
#else
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                return new HttpResponseMessage();
            })
            .Callback<HttpRequestMessage, CancellationToken>(async (r, ct) =>
            {
                httpRequest = r;

                // We have to capture content as it can't be accessed after request is disposed inside of SendExportRequest method
                httpRequestContent = await r.Content.ReadAsByteArrayAsync();
            })
#endif
            .Verifiable();

        var exportClient = new OtlpHttpTraceExportClient(options, new HttpClient(httpHandlerMock.Object));

        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (includeServiceNameInResource)
        {
            resourceBuilder.AddAttributes(
                new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "service_name"),
                    new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "ns_1"),
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

        var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
        RunTest(batch);

        void RunTest(Batch<Activity> batch)
        {
            var request = new OtlpCollector.ExportTraceServiceRequest();

            request.AddBatch(DefaultSdkLimitOptions, resourceBuilder.Build().ToOtlpResource(), batch);

            // Act
            var result = exportClient.SendExportRequest(request);

            // Assert
            Assert.True(result);
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

            Assert.NotNull(httpRequest.Content);
            Assert.IsType<OtlpHttpTraceExportClient.ExportRequestContent>(httpRequest.Content);
            Assert.Contains(httpRequest.Content.Headers, h => h.Key == "Content-Type" && h.Value.First() == OtlpHttpTraceExportClient.MediaContentType);

            var exportTraceRequest = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(httpRequestContent);
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
