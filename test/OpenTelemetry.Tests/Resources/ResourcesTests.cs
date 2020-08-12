﻿// <copyright file="ResourcesTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Resources
{
    public class ResourcesTests
    {
        [Fact]
        public void ServiceResource_ServiceName()
        {
            var resource = OpenTelemetry.Resources.Resources.CreateServiceResource("my-service");
            Assert.Equal(2, resource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNameKey, "my-service"), resource.Attributes);
            Assert.Single(resource.Attributes.Where(kvp => kvp.Key == Resource.ServiceNameKey));
            Assert.True(Guid.TryParse((string)resource.Attributes.Single(kvp => kvp.Key == Resource.ServiceInstanceIdKey).Value, out _));
        }

        [Fact]
        public void ServiceResource_ServiceNameAndInstance()
        {
            var resource = OpenTelemetry.Resources.Resources.CreateServiceResource("my-service", "123");
            Assert.Equal(2, resource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNameKey, "my-service"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceInstanceIdKey, "123"), resource.Attributes);
        }

        [Fact]
        public void ServiceResource_ServiceNameAndInstanceAndNamespace()
        {
            var resource = OpenTelemetry.Resources.Resources.CreateServiceResource("my-service", "123", "my-namespace");
            Assert.Equal(3, resource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNameKey, "my-service"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceInstanceIdKey, "123"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNamespaceKey, "my-namespace"), resource.Attributes);
        }

        [Fact]
        public void ServiceResource_ServiceNameAndInstanceAndNamespaceAndVersion()
        {
            var resource = OpenTelemetry.Resources.Resources.CreateServiceResource("my-service", "123", "my-namespace", "semVer:1.2.3");
            Assert.Equal(4, resource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNameKey, "my-service"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceInstanceIdKey, "123"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceNamespaceKey, "my-namespace"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>(Resource.ServiceVersionKey, "semVer:1.2.3"), resource.Attributes);
        }

        [Fact]
        public void ServiceResource_Empty()
        {
            var resource = OpenTelemetry.Resources.Resources.CreateServiceResource(null);
            Assert.Empty(resource.Attributes);
        }
    }
}
