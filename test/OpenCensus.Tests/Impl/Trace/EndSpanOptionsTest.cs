// <copyright file="EndSpanOptionsTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using Xunit;

    public class EndSpanOptionsTest
    {
        [Fact]
        public void EndSpanOptions_DefaultOptions()
        {
            Assert.Null(EndSpanOptions.Default.Status);
            Assert.False(EndSpanOptions.Default.SampleToLocalSpanStore);
        }

        [Fact]
        public void SetStatus_Ok()
        {
            EndSpanOptions endSpanOptions = EndSpanOptions.Builder().SetStatus(Status.Ok).Build();
            Assert.Equal(Status.Ok, endSpanOptions.Status);
        }

        [Fact]
        public void SetStatus_Error()
        {
            EndSpanOptions endSpanOptions =
                EndSpanOptions.Builder()
                    .SetStatus(Status.Cancelled.WithDescription("ThisIsAnError"))
                    .Build();
            Assert.Equal(Status.Cancelled.WithDescription("ThisIsAnError"), endSpanOptions.Status);
        }

        [Fact]
        public void SetSampleToLocalSpanStore()
        {
            EndSpanOptions endSpanOptions =
                EndSpanOptions.Builder().SetSampleToLocalSpanStore(true).Build();
            Assert.True(endSpanOptions.SampleToLocalSpanStore);
        }

        [Fact]
        public void EndSpanOptions_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(
            //    EndSpanOptions.builder()
            //        .setStatus(Status.CANCELLED.withDescription("ThisIsAnError"))
            //        .build(),
            //    EndSpanOptions.builder()
            //        .setStatus(Status.CANCELLED.withDescription("ThisIsAnError"))
            //        .build());
            // tester.addEqualityGroup(EndSpanOptions.builder().build(), EndSpanOptions.DEFAULT);
            // tester.testEquals();
        }

        [Fact]
        public void EndSpanOptions_ToString()
        {
            EndSpanOptions endSpanOptions =
                EndSpanOptions.Builder()
                    .SetStatus(Status.Cancelled.WithDescription("ThisIsAnError"))
                    .Build();
            Assert.Contains("ThisIsAnError", endSpanOptions.ToString());
        }
    }
}
