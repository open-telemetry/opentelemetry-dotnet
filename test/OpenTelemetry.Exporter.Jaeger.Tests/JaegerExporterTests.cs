// <copyright file="JaegerExporterTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Exporter.Jaeger.Implementation.Tests;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Thrift.Protocol;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class JaegerExporterTests
    {
        [Fact]
        public void AddJaegerExporterNamedOptionsSupported()
        {
            int defaultExporterOptionsConfigureOptionsInvocations = 0;
            int namedExporterOptionsConfigureOptionsInvocations = 0;

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<JaegerExporterOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                    services.Configure<JaegerExporterOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
                })
                .AddJaegerExporter()
                .AddJaegerExporter("Exporter2", o => { })
                .Build();

            Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
            Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
        }

        [Fact]
        public void JaegerExporter_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddJaegerExporter());
        }

        [Fact]
        public void JaegerTraceExporter_ctor_NullServiceNameAllowed()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            Assert.NotNull(jaegerTraceExporter);
        }

        [Fact]
        public void UserHttpFactoryCalled()
        {
            JaegerExporterOptions options = new JaegerExporterOptions();

            var defaultFactory = options.HttpClientFactory;

            int invocations = 0;
            options.Protocol = JaegerExportProtocol.HttpBinaryThrift;
            options.HttpClientFactory = () =>
            {
                invocations++;
                return defaultFactory();
            };

            using (var exporter = new JaegerExporter(options))
            {
                Assert.Equal(1, invocations);
            }

            using (var provider = Sdk.CreateTracerProviderBuilder()
                .AddJaegerExporter(o =>
                {
                    o.Protocol = JaegerExportProtocol.HttpBinaryThrift;
                    o.HttpClientFactory = options.HttpClientFactory;
                })
                .Build())
            {
                Assert.Equal(2, invocations);
            }

            options.HttpClientFactory = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new JaegerExporter(options);
            });

            options.HttpClientFactory = () => null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new JaegerExporter(options);
            });
        }

        [Fact]
        public void ServiceProviderHttpClientFactoryInvoked()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddHttpClient();

            int invocations = 0;

            services.AddHttpClient("JaegerExporter", configureClient: (client) => invocations++);

            services.AddOpenTelemetry().WithTracing(builder => builder
                .AddJaegerExporter(o => o.Protocol = JaegerExportProtocol.HttpBinaryThrift));

            using var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

            Assert.Equal(1, invocations);
        }

        [Theory]
        [InlineData("/api/traces")]
        [InlineData("/foo/bar")]
        [InlineData("/")]
        public void HttpClient_Posts_To_Configured_Endpoint(string uriPath)
        {
            // Arrange
            ConcurrentDictionary<Guid, string> responses = new ConcurrentDictionary<Guid, string>();
            using var testServer = TestHttpServer.RunServer(
                context =>
                {
                    context.Response.StatusCode = 200;

                    using StreamReader readStream = new StreamReader(context.Request.InputStream);

                    string requestContent = readStream.ReadToEnd();

                    responses.TryAdd(
                        Guid.Parse(context.Request.QueryString["requestId"]),
                        context.Request.Url.LocalPath);

                    context.Response.OutputStream.Close();
                },
                out var testServerHost,
                out var testServerPort);

            var requestId = Guid.NewGuid();
            var options = new JaegerExporterOptions
            {
                Endpoint = new Uri($"http://{testServerHost}:{testServerPort}{uriPath}?requestId={requestId}"),
                Protocol = JaegerExportProtocol.HttpBinaryThrift,
                ExportProcessorType = ExportProcessorType.Simple,
            };

            using var jaegerExporter = new JaegerExporter(options);

            // Act
            jaegerExporter.SetResourceAndInitializeBatch(Resource.Empty);
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.SendCurrentBatch();

            // Assert
            Assert.True(responses.ContainsKey(requestId));
            Assert.Equal(uriPath, responses[requestId]);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_UpdatesServiceName()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResourceAndInitializeBatch(Resource.Empty);

            Assert.StartsWith("unknown_service:", process.ServiceName);

            jaegerTraceExporter.SetResourceAndInitializeBatch(ResourceBuilder.CreateEmpty().AddService("MyService").Build());

            Assert.Equal("MyService", process.ServiceName);

            jaegerTraceExporter.SetResourceAndInitializeBatch(ResourceBuilder.CreateEmpty().AddService("MyService", "MyNamespace").Build());

            Assert.Equal("MyNamespace.MyService", process.ServiceName);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_CreatesTags()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResourceAndInitializeBatch(ResourceBuilder.CreateEmpty().AddAttributes(new Dictionary<string, object>
            {
                ["Tag"] = "value",
            }).Build());

            Assert.NotNull(process.Tags);
            Assert.Single(process.Tags);
            Assert.Equal("value", process.Tags["Tag"].VStr);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_CombinesTags()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            JaegerTagTransformer.Instance.TryTransformTag(new KeyValuePair<string, object>("Tag1", "value1"), out var result);
            process.Tags = new Dictionary<string, JaegerTag> { ["Tag1"] = result };

            jaegerTraceExporter.SetResourceAndInitializeBatch(ResourceBuilder.CreateEmpty().AddAttributes(new Dictionary<string, object>
            {
                ["Tag2"] = "value2",
            }).Build());

            Assert.NotNull(process.Tags);
            Assert.Equal(2, process.Tags.Count);
            Assert.Equal("value1", process.Tags["Tag1"].VStr);
            Assert.Equal("value2", process.Tags["Tag2"].VStr);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_IgnoreServiceResources()
        {
            using var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());
            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResourceAndInitializeBatch(ResourceBuilder.CreateEmpty().AddAttributes(new Dictionary<string, object>
            {
                [ResourceSemanticConventions.AttributeServiceName] = "servicename",
                [ResourceSemanticConventions.AttributeServiceNamespace] = "servicenamespace",
            }).Build());

            Assert.Null(process.Tags);
        }

        [Fact]
        public void JaegerTraceExporter_SetResource_UpdatesServiceNameFromIConfiguration()
        {
            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services =>
                {
                    Dictionary<string, string> configuration = new()
                    {
                        ["OTEL_SERVICE_NAME"] = "myservicename",
                    };

                    services.AddSingleton<IConfiguration>(
                        new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
                });

            var jaegerTraceExporter = new JaegerExporter(new JaegerExporterOptions());

            tracerProviderBuilder.AddProcessor(new BatchActivityExportProcessor(jaegerTraceExporter));

            using var provider = tracerProviderBuilder.Build();

            var process = jaegerTraceExporter.Process;

            jaegerTraceExporter.SetResourceAndInitializeBatch(Resource.Empty);

            Assert.Equal("myservicename", process.ServiceName);
        }

        [Fact]
        public void JaegerTraceExporter_BuildBatchesToTransmit_FlushedBatch()
        {
            // Arrange
            using var jaegerExporter = new JaegerExporter(new JaegerExporterOptions { MaxPayloadSizeInBytes = 1500 });
            jaegerExporter.SetResourceAndInitializeBatch(Resource.Empty);

            // Act
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());
            jaegerExporter.AppendSpan(CreateTestJaegerSpan());

            // Assert
            Assert.Equal(1U, jaegerExporter.NumberOfSpansInCurrentBatch);
        }

        [Theory]
        [InlineData("Compact", 1500)]
        [InlineData("Binary", 2200)]
        public void JaegerTraceExporter_SpansSplitToBatches_SpansIncludedInBatches(string protocolType, int maxPayloadSizeInBytes)
        {
            TProtocolFactory protocolFactory = protocolType == "Compact"
                ? new TCompactProtocol.Factory()
                : new TBinaryProtocol.Factory();
            var client = new TestJaegerClient();

            // Arrange
            using var jaegerExporter = new JaegerExporter(
                new JaegerExporterOptions { MaxPayloadSizeInBytes = maxPayloadSizeInBytes },
                protocolFactory,
                client);
            jaegerExporter.SetResourceAndInitializeBatch(Resource.Empty);

            // Create six spans, each taking more space than the previous one
            var spans = new JaegerSpan[6];
            for (int i = 0; i < 6; i++)
            {
                spans[i] = CreateTestJaegerSpan(
                    additionalAttributes: new Dictionary<string, object>
                    {
                        ["foo"] = new string('_', 10 * i),
                    });
            }

            var protocol = protocolFactory.GetProtocol();
            var serializedSpans = spans.Select(s =>
            {
                s.Write(protocol);
                var data = protocol.WrittenData.ToArray();
                protocol.Clear();
                return data;
            }).ToArray();

            // Act
            var sentBatches = new List<byte[]>();
            foreach (var span in spans)
            {
                jaegerExporter.AppendSpan(span);
                var sentBatch = client.LastWrittenData;
                if (sentBatch != null)
                {
                    sentBatches.Add(sentBatch);
                    client.LastWrittenData = null;
                }
            }

            // Assert

            // Appending the six spans will send two batches with the first four spans
            Assert.Equal(2, sentBatches.Count);
            Assert.True(
                ContainsSequence(sentBatches[0], serializedSpans[0]),
                "Expected span data not found in sent batch");
            Assert.True(
                ContainsSequence(sentBatches[0], serializedSpans[1]),
                "Expected span data not found in sent batch");

            Assert.True(
                ContainsSequence(sentBatches[1], serializedSpans[2]),
                "Expected span data not found in sent batch");
            Assert.True(
                ContainsSequence(sentBatches[1], serializedSpans[3]),
                "Expected span data not found in sent batch");

            // jaegerExporter.Batch should contain the two remaining spans
            Assert.Equal(2U, jaegerExporter.NumberOfSpansInCurrentBatch);
            jaegerExporter.SendCurrentBatch();
            Assert.True(client.LastWrittenData != null);
            var serializedBatch = client.LastWrittenData;
            Assert.True(
                ContainsSequence(serializedBatch, serializedSpans[4]),
                "Expected span data not found in unsent batch");
            Assert.True(
                ContainsSequence(serializedBatch, serializedSpans[5]),
                "Expected span data not found in unsent batch");
        }

        internal static JaegerSpan CreateTestJaegerSpan(
            bool setAttributes = true,
            Dictionary<string, object> additionalAttributes = null,
            bool addEvents = true,
            bool addLinks = true,
            Resource resource = null,
            ActivityKind kind = ActivityKind.Client)
        {
            return JaegerActivityConversionTest
                .CreateTestActivity(
                    setAttributes, additionalAttributes, addEvents, addLinks, resource, kind)
                .ToJaegerSpan();
        }

        private static bool ContainsSequence(byte[] source, byte[] pattern)
        {
            for (var start = 0; start < (source.Length - pattern.Length + 1); start++)
            {
                if (source.Skip(start).Take(pattern.Length).SequenceEqual(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TestJaegerClient : IJaegerClient
        {
            public bool Connected => true;

            public byte[] LastWrittenData { get; set; }

            public void Close()
            {
            }

            public void Connect()
            {
            }

            public void Dispose()
            {
            }

            public int Send(byte[] buffer, int offset, int count)
            {
                this.LastWrittenData = new ArraySegment<byte>(buffer, offset, count).ToArray();
                return count;
            }
        }
    }
}
