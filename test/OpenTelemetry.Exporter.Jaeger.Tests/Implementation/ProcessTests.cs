// <copyright file="ProcessTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class ProcessTests
    {
        [Fact]
        public void Process_ApplyLibraryResource_UpdatesServiceName()
        {
            var process = new Process("TestService", null);

            process.ApplyLibraryResource(Resources.Resource.Empty);

            Assert.Equal("TestService", process.ServiceName);

            process.ApplyLibraryResource(Resources.Resources.CreateServiceResource("MyService"));

            Assert.Equal("MyService", process.ServiceName);

            process.ApplyLibraryResource(Resources.Resources.CreateServiceResource("MyService", serviceNamespace: "MyNamespace"));

            Assert.Equal("MyNamespace.MyService", process.ServiceName);
        }

        [Fact]
        public void Process_ApplyLibraryResource_CreatesTags()
        {
            var process = new Process("TestService", null);

            process.ApplyLibraryResource(new Resource(new Dictionary<string, object>
            {
                ["Tag"] = "value",
            }));

            Assert.NotNull(process.Tags);
            Assert.Single(process.Tags);
            Assert.Equal("value", process.Tags.Where(t => t.Key == "Tag").Select(t => t.VStr).FirstOrDefault());
        }

        [Fact]
        public void Process_ApplyLibraryResource_CombinesTags()
        {
            var process = new Process("TestService", new Dictionary<string, object>
            {
                ["Tag1"] = "value1",
            });

            process.ApplyLibraryResource(new Resource(new Dictionary<string, object>
            {
                ["Tag2"] = "value2",
            }));

            Assert.NotNull(process.Tags);
            Assert.Equal(2, process.Tags.Count);
            Assert.Equal("value1", process.Tags.Where(t => t.Key == "Tag1").Select(t => t.VStr).FirstOrDefault());
            Assert.Equal("value2", process.Tags.Where(t => t.Key == "Tag2").Select(t => t.VStr).FirstOrDefault());
        }

        [Fact]
        public void Process_ApplyLibraryResource_IgnoreLibraryResources()
        {
            var process = new Process("TestService", null);

            process.ApplyLibraryResource(new Resource(new Dictionary<string, object>
            {
                [Resource.LibraryNameKey] = "libname",
                [Resource.LibraryVersionKey] = "libversion",
            }));

            Assert.Null(process.Tags);
        }
    }
}
