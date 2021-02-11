// <copyright file="ResourceTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Resources.Tests
{
    public class ResourceTest : IDisposable
    {
        private const string KeyName = "key";
        private const string ValueName = "value";
        private const string OtelEnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";

        public ResourceTest()
        {
            Environment.SetEnvironmentVariable(OtelEnvVarKey, null);
        }

        [Fact]
        public static void CreateResource_NullAttributeCollection()
        {
            // Act and Assert
            var resource = new Resource(null);
            Assert.Empty(resource.Attributes);
        }

        [Fact]
        public void CreateResource_NullAttributeValue()
        {
            // Arrange
            var attributes = new Dictionary<string, object> { { "NullValue", null } };

            // Act and Assert
            Assert.Throws<ArgumentException>(() => new Resource(attributes));
        }

        [Fact]
        public void CreateResource_EmptyAttributeKey()
        {
            // Arrange
            var attributes = new Dictionary<string, object> { { string.Empty, "value" } };

            // Act
            var resource = new Resource(attributes);

            // Assert
            Assert.Single(resource.Attributes);

            var attribute = resource.Attributes.Single();
            Assert.Empty(attribute.Key);
            Assert.Equal("value", attribute.Value);
        }

        [Fact]
        public void CreateResource_EmptyAttributeValue()
        {
            // Arrange
            var attributes = new Dictionary<string, object> { { "EmptyValue", string.Empty } };

            // does not throw
            var resource = new Resource(attributes);

            // Assert
            Assert.Single(resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("EmptyValue", string.Empty), resource.Attributes);
        }

        [Fact]
        public void CreateResource_EmptyAttribute()
        {
            // Arrange
            var attributeCount = 0;
            var attributes = this.CreateAttributes(attributeCount);

            // Act
            var resource = new Resource(attributes);

            // Assert
            ValidateResource(resource, attributeCount);
        }

        [Fact]
        public void CreateResource_SingleAttribute()
        {
            // Arrange
            var attributeCount = 1;
            var attributes = this.CreateAttributes(attributeCount);

            // Act
            var resource = new Resource(attributes);

            // Assert
            ValidateResource(resource, attributeCount);
        }

        [Fact]
        public void CreateResource_MultipleAttribute()
        {
            // Arrange
            var attributeCount = 5;
            var attributes = this.CreateAttributes(attributeCount);

            // Act
            var resource = new Resource(attributes);

            // Assert
            ValidateResource(resource, attributeCount);
        }

        [Fact]
        public void CreateResource_SupportedAttributeTypes()
        {
            var attributes = new Dictionary<string, object>
            {
                { "string", "stringValue" },
                { "long", 1L },
                { "bool", true },
                { "double", 0.1d },

                // int and float supported by conversion to long and double
                { "int", 1 },
                { "short", (short)1 },
                { "float", 0.1f },
            };

            var resource = new Resource(attributes);

            Assert.Equal(7, resource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>("string", "stringValue"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("long", 1L), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("bool", true), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("double", 0.1d), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("int", 1L), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("short", 1L), resource.Attributes);

            double convertedFloat = Convert.ToDouble(0.1f, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains(new KeyValuePair<string, object>("float", convertedFloat), resource.Attributes);
        }

        [Fact]
        public void CreateResource_NotSupportedAttributeTypes()
        {
            var attributes = new Dictionary<string, object>
            {
                { "dynamic", new { } },
                { "array", new int[1] },
                { "complex", this },
            };

            Assert.Throws<ArgumentException>(() => new Resource(attributes));
        }

        [Fact]
        public void MergeResource_EmptyAttributeSource_MultiAttributeTarget()
        {
            // Arrange
            var sourceAttributeCount = 0;
            var sourceAttributes = this.CreateAttributes(sourceAttributeCount);
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributeCount = 3;
            var otherAttributes = this.CreateAttributes(otherAttributeCount);
            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);

            ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
        }

        [Fact]
        public void MergeResource_MultiAttributeSource_EmptyAttributeTarget()
        {
            // Arrange
            var sourceAttributeCount = 3;
            var sourceAttributes = this.CreateAttributes(sourceAttributeCount);
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributeCount = 0;
            var otherAttributes = this.CreateAttributes(otherAttributeCount);
            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
        }

        [Fact]
        public void MergeResource_MultiAttributeSource_MultiAttributeTarget_NoOverlap()
        {
            // Arrange
            var sourceAttributeCount = 3;
            var sourceAttributes = this.CreateAttributes(sourceAttributeCount);
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributeCount = 3;
            var otherAttributes = this.CreateAttributes(otherAttributeCount, sourceAttributeCount);
            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
        }

        [Fact]
        public void MergeResource_MultiAttributeSource_MultiAttributeTarget_SingleOverlap()
        {
            // Arrange
            var sourceAttributeCount = 3;
            var sourceAttributes = this.CreateAttributes(sourceAttributeCount);
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributeCount = 3;
            var otherAttributes = this.CreateAttributes(otherAttributeCount, sourceAttributeCount - 1);
            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceAttributeCount + otherAttributeCount - 1);

            // Also verify target attributes were not overwritten
            foreach (var otherAttribute in otherAttributes)
            {
                Assert.Contains(otherAttribute, otherResource.Attributes);
            }
        }

        [Fact]
        public void MergeResource_MultiAttributeSource_MultiAttributeTarget_FullOverlap()
        {
            // Arrange
            var sourceAttributeCount = 3;
            var sourceAttributes = this.CreateAttributes(sourceAttributeCount);
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributeCount = 3;
            var otherAttributes = this.CreateAttributes(otherAttributeCount);
            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, otherAttributeCount);

            // Also verify target attributes were not overwritten
            foreach (var otherAttribute in otherAttributes)
            {
                Assert.Contains(otherAttribute, otherResource.Attributes);
            }
        }

        [Fact]
        public void MergeResource_MultiAttributeSource_DuplicatedKeysInPrimary()
        {
            // Arrange
            var sourceAttributes = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key1", "value1.1"),
            };
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributes = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("key2", "value2"),
            };

            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);

            Assert.Equal(2, newResource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, object>("key1", "value1"), newResource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("key2", "value2"), newResource.Attributes);
        }

        [Fact]
        public void MergeResource_UpdatingResourceOverridesCurrentResource()
        {
            // Arrange
            var currentAttributes = new Dictionary<string, object> { { "value", "currentValue" } };
            var updatingAttributes = new Dictionary<string, object> { { "value", "updatedValue" } };
            var currentResource = new Resource(currentAttributes);
            var updatingResource = new Resource(updatingAttributes);

            var newResource = currentResource.Merge(updatingResource);

            // Assert
            Assert.Single(newResource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("value", "updatedValue"), newResource.Attributes);
        }

        [Fact]
        public void GetResourceWithTelemetrySDKAttributes()
        {
            // Arrange
            var resource = ResourceBuilder.CreateDefault().AddTelemetrySdk().AddEnvironmentVariableDetector().Build();

            // Assert
            var attributes = resource.Attributes;
            Assert.Equal(4, attributes.Count());
            ValidateTelemetrySdkAttributes(attributes);
        }

        [Fact]
        public void GetResourceWithDefaultAttributes_EmptyResource()
        {
            // Arrange
            var resource = ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector().Build();

            // Assert
            var attributes = resource.Attributes;
            Assert.Single(attributes);
            ValidateDefaultAttributes(attributes);
        }

        [Fact]
        public void GetResourceWithDefaultAttributes_ResourceWithAttrs()
        {
            // Arrange
            var resource = ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector().AddAttributes(this.CreateAttributes(2)).Build();

            // Assert
            var attributes = resource.Attributes;
            Assert.Equal(3, attributes.Count());
            ValidateAttributes(attributes, 0, 1);
            ValidateDefaultAttributes(attributes);
        }

        [Fact]
        public void GetResourceWithDefaultAttributes_WithEnvVar()
        {
            // Arrange
            Environment.SetEnvironmentVariable(OtelEnvVarKey, "EVKey1=EVVal1,EVKey2=EVVal2");
            var resource = ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector().AddAttributes(this.CreateAttributes(2)).Build();

            // Assert
            var attributes = resource.Attributes;
            Assert.Equal(5, attributes.Count());
            ValidateAttributes(attributes, 0, 1);
            ValidateDefaultAttributes(attributes);
            Assert.Contains(new KeyValuePair<string, object>("EVKey1", "EVVal1"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("EVKey2", "EVVal2"), attributes);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(OtelEnvVarKey, null);
        }

        private static void AddAttributes(Dictionary<string, object> attributes, int attributeCount, int startIndex = 0)
        {
            for (var i = startIndex; i < attributeCount + startIndex; ++i)
            {
                attributes.Add($"{KeyName}{i}", $"{ValueName}{i}");
            }
        }

        private static void ValidateAttributes(IEnumerable<KeyValuePair<string, object>> attributes, int startIndex = 0, int endIndex = 0)
        {
            var keyValuePairs = attributes as KeyValuePair<string, object>[] ?? attributes.ToArray();
            var endInd = endIndex == 0 ? keyValuePairs.Length - 1 : endIndex;
            for (var i = startIndex; i <= endInd; ++i)
            {
                Assert.Contains(
                    new KeyValuePair<string, object>(
                    $"{KeyName}{i}", $"{ValueName}{i}"), keyValuePairs);
            }
        }

        private static void ValidateResource(Resource resource, int attributeCount)
        {
            Assert.NotNull(resource);
            Assert.NotNull(resource.Attributes);
            Assert.Equal(attributeCount, resource.Attributes.Count());
            ValidateAttributes(resource.Attributes);
        }

        private static void ValidateTelemetrySdkAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.name", "opentelemetry"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.language", "dotnet"), attributes);
            var versionAttribute = attributes.Where(pair => pair.Key.Equals("telemetry.sdk.version"));
            Assert.Single(versionAttribute);
        }

        private static void ValidateDefaultAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            var serviceName = attributes.Where(pair => pair.Key.Equals("service.name"));
            Assert.Single(serviceName);
            Assert.Contains("unknown_service", serviceName.FirstOrDefault().Value as string);
        }

        private Dictionary<string, object> CreateAttributes(int attributeCount, int startIndex = 0)
        {
            var attributes = new Dictionary<string, object>();
            AddAttributes(attributes, attributeCount, startIndex);
            return attributes;
        }
    }
}
