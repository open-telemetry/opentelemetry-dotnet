// <copyright file="HttpClientTests.Meter.cs" company="OpenTelemetry Authors">
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

using System.ComponentModel;
using System.Diagnostics;
using Moq;
using OpenTelemetry.Instrumentation.Http.Tests.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
    public partial class HttpClientTests
    {
        private const string MetricName = "http.client.duration";

        [Theory]
        [Description("Check that enrich delegate from options works")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MeterProvider_EnrichMetricWithTags_When_EnrichDelegateIsSet(bool shouldEnrich)
        {
            // given
            var metrics = new List<Metric>();
            const string enrichedTagKey = nameof(HttpClientInstrumentationMeterOptions.EnrichWithHttpRequestMessage);
            const string enrichedTagValue = "yes";
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation(o =>
                {
                    if (!shouldEnrich)
                    {
                        return;
                    }

                    o.EnrichWithHttpRequestMessage = (tags, _) =>
                        tags.Add(new KeyValuePair<string, object>(enrichedTagKey, enrichedTagValue));
                })
                .AddInMemoryExporter(metrics)
                .Build()!;

            // when
            var request = new HttpRequestMessage { RequestUri = new Uri(this.url), Method = new HttpMethod("GET"), };
            using var c = new HttpClient();
            await c.SendAsync(request);

            // then
            meterProvider.Dispose();
            var metricTags = ExtractMetricTags(metrics);

            AssertDefaultTags(metricTags);
            Assert.Equal(shouldEnrich ? 7 : 6, metricTags.Count);

            // validates that tags collection also contains enriched tags
            var enrichedTag = new KeyValuePair<string, object>(enrichedTagKey, enrichedTagValue);
            if (shouldEnrich)
            {
                Assert.Contains(enrichedTag, metricTags);
            }
            else
            {
                Assert.DoesNotContain(enrichedTag, metricTags);
            }
        }

        [Fact]
        [Description("Check that invalid enrich function has not effect on listener")]
        public async Task MeterProvider_HasNoEffect_When_EnrichDelegateThrows()
        {
            // given
            var metrics = new List<Metric>();
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation(o => o.EnrichWithHttpRequestMessage = (_, _) => throw new Exception())
                .AddInMemoryExporter(metrics)
                .Build()!;

            // when
            var request = new HttpRequestMessage { RequestUri = new Uri(this.url), Method = new HttpMethod("GET"), };
            using var c = new HttpClient();
            await c.SendAsync(request);

            // then
            meterProvider.Dispose();

            var metricTags = ExtractMetricTags(metrics);

            AssertDefaultTags(metricTags);
            Assert.Equal(6, metricTags.Count);
        }

        [Fact]
        public async Task MeterProvider_RequestNotCollected_When_InstrumentationFilterApplied()
        {
            // given
            var exportedItems = new List<Metric>();

            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation(
                    opt =>
                    {
                        opt.FilterHttpRequestMessage = _ => false;
                    })
                .AddInMemoryExporter(exportedItems)
                .Build()!;

            // when
            using var c = new HttpClient();
            await c.GetAsync(this.url);

            // then
            meterProvider.Dispose();

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task MeterProvider_RequestNotCollected_When_InstrumentationFilterDelegateThrows()
        {
            // given
            var exportedItems = new List<Metric>();

            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation(
                    opt =>
                    {
                        opt.FilterHttpRequestMessage = _ => throw new Exception();
                    })
                .AddInMemoryExporter(exportedItems)
                .Build()!;

            // when
            using var c = new HttpClient();
            await c.GetAsync(this.url);

            // then
            meterProvider.Dispose();

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task MeterProvider_RequestNotCollected_When_SdkIsSuppressed()
        {
            // given
            var exportedItems = new List<Metric>();

            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build()!;

            // when
            using (SuppressInstrumentationScope.Begin())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            // then
            meterProvider.Dispose();

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task MeterProvider_OthersListenersHasNoEffectOnMeterListener_When_OtherListenersExist()
        {
            // given
            var metrics = new List<Metric>();
            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(metrics)
                .Build()!;

            const string customTagKey = nameof(customTagKey);
            const string customTagValue = nameof(customTagValue);
            var processor = new Mock<BaseProcessor<Activity>>();
            var traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(o =>
                    o.EnrichWithHttpRequestMessage =
                        (activity, _) => activity.SetTag(customTagKey, customTagValue))
                .AddProcessor(processor.Object)
                .Build()!;

            // when
            using var c = new HttpClient();
            await c.GetAsync(this.url);

            // then
            traceProvider.Dispose();
            meterProvider.Dispose();

            var metricTags = ExtractMetricTags(metrics);

            AssertDefaultTags(metricTags);
            Assert.Equal(6, metricTags.Count);
            Assert.DoesNotContain(new KeyValuePair<string, object>(customTagKey, customTagValue), metricTags);
        }

        /// <summary>
        /// Validates that tags collection contains all default tags.
        /// </summary>
        /// <param name="metricTags">Tags from metric.</param>
        private static void AssertDefaultTags(IReadOnlyDictionary<string, object> metricTags)
        {
            Assert.Contains(SemanticConventions.AttributeHttpMethod, metricTags);
            Assert.Contains(SemanticConventions.AttributeHttpScheme, metricTags);
            Assert.Contains(SemanticConventions.AttributeHttpStatusCode, metricTags);
            Assert.Contains(SemanticConventions.AttributeHttpFlavor, metricTags);
            Assert.Contains(SemanticConventions.AttributeNetPeerName, metricTags);
            Assert.Contains(SemanticConventions.AttributeNetPeerPort, metricTags);
        }

        private static IReadOnlyDictionary<string, object> ExtractMetricTags(IEnumerable<Metric> metrics)
        {
            var metricTags = metrics
                .First(metric => metric.Name == MetricName)
                .GetMetricPoints()
                .AsEnumerable()
                .First()
                .Tags
                .AsEnumerable()
                .ToDictionary(k => k.Key, v => v.Value);

            return metricTags;
        }
    }
}
