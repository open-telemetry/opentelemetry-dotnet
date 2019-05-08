// <copyright file="ExportComponentBaseTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Export.Test
{
    using Xunit;

    public class ExportComponentBaseTest
    {
        private readonly IExportComponent exportComponent = ExportComponentBase.NewNoopExportComponent;

        [Fact]
        public void ImplementationOfSpanExporter()
        {
            Assert.Equal(SpanExporter.NoopSpanExporter, exportComponent.SpanExporter);
        }

        [Fact]
        public void ImplementationOfActiveSpans()
        {
            Assert.Equal(RunningSpanStoreBase.NoopRunningSpanStore, exportComponent.RunningSpanStore);
        }

        [Fact]
        public void ImplementationOfSampledSpanStore()
        {
            Assert.Equal(SampledSpanStoreBase.NewNoopSampledSpanStore.GetType(), exportComponent.SampledSpanStore.GetType());
        }
    }
}
