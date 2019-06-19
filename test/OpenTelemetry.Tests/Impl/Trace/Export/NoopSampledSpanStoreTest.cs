// <copyright file="NoopSampledSpanStoreTest.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace.Export.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class NoopSampledSpanStoreTest
    {
        // @Rule public final ExpectedException thrown = ExpectedException.none();
        private static readonly ISampledPerSpanNameSummary EMPTY_PER_SPAN_NAME_SUMMARY =
            SampledPerSpanNameSummary.Create(new Dictionary<ISampledLatencyBucketBoundaries, int>(), new Dictionary<CanonicalCode, int>());

        [Fact]
        public void NoopSampledSpanStore_RegisterUnregisterAndGetSummary()
        {
            // should return empty before register
            var sampledSpanStore =
                ((NoopExportComponent)ExportComponent.NewNoopExportComponent).SampledSpanStore;
            var summary = sampledSpanStore.Summary;
            Assert.Empty(summary.PerSpanNameSummary);

            // should return non-empty summaries with zero latency/error sampled spans after register
            sampledSpanStore.RegisterSpanNamesForCollection(
                new List<string>() { "TestSpan1", "TestSpan2", "TestSpan3" });
            summary = sampledSpanStore.Summary;
            Assert.Equal(3, summary.PerSpanNameSummary.Count);
            Assert.Contains(summary.PerSpanNameSummary, (item) =>
            {
                return (item.Key == "TestSpan1" || item.Key == "TestSpan2" || item.Key == "TestSpan3") &&
                item.Value.Equals(EMPTY_PER_SPAN_NAME_SUMMARY); 
            });

            // should unregister specific spanNames
            sampledSpanStore.UnregisterSpanNamesForCollection(new List<string>() { "TestSpan1", "TestSpan3" });
            summary = sampledSpanStore.Summary;
            Assert.Equal(1, summary.PerSpanNameSummary.Count);
            Assert.Contains(summary.PerSpanNameSummary, (item) =>
            {
                return (item.Key == "TestSpan2") && item.Value.Equals(EMPTY_PER_SPAN_NAME_SUMMARY);
            });

        }

        [Fact]
        public void NoopSampledSpanStore_GetLatencySampledSpans()
        {
            var sampledSpanStore = ((NoopExportComponent)ExportComponent.NewNoopExportComponent).SampledSpanStore;
            var latencySampledSpans =
                sampledSpanStore.GetLatencySampledSpans(
                    SampledSpanStoreLatencyFilter.Create("TestLatencyFilter", TimeSpan.Zero, TimeSpan.Zero, 0));
            Assert.Empty(latencySampledSpans);
        }

        [Fact]
        public void NoopSampledSpanStore_GetErrorSampledSpans()
        {
            var sampledSpanStore = ((NoopExportComponent)ExportComponent.NewNoopExportComponent).SampledSpanStore;
            var errorSampledSpans =
                sampledSpanStore.GetErrorSampledSpans(
                    SampledSpanStoreErrorFilter.Create("TestErrorFilter", null, 0));
            Assert.Empty(errorSampledSpans);
        }

        [Fact]
        public void NoopSampledSpanStore_GetRegisteredSpanNamesForCollection()
        {
            var sampledSpanStore = ((NoopExportComponent)ExportComponent.NewNoopExportComponent).SampledSpanStore;
            sampledSpanStore.RegisterSpanNamesForCollection(new List<string>() { "TestSpan3", "TestSpan4" });
            var registeredSpanNames = sampledSpanStore.RegisteredSpanNamesForCollection;
            Assert.Equal(2, registeredSpanNames.Count);
            Assert.Contains(registeredSpanNames, (item) =>
            {
                return (item == "TestSpan3" || item == "TestSpan4");
            });
        }
    }
}
