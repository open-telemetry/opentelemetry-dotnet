// <copyright file="ResourceTest.cs" company="OpenTelemetry Authors">
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
using Xunit;
using OpenTelemetry.Resources;
using System.Collections.Generic;
using System;
using System.Linq;

namespace OpenTelemetry.Impl.Resources
{
    public class ResourceTest
    {
        private const string KeyName = "key";
        private const string ValueName = "value";
        private static readonly Random Random = new Random();

        [Fact]
        public static void CreateResource_NullAttributeCollection()
        {
            // Act and Assert
            Assert.Throws<ArgumentNullException>(() => new Resource(null));
        }

        [Fact]
        public void CreateResource_NullAttributeValue()
        {
            // Arrange
            var attributeCount = 3;
            var attributes = this.CreateAttributes(attributeCount);
            attributes.Add("NullValue", null);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(attributes));

            // Assert
            Assert.Equal("Attribute value should be a string with a length not exceeding 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_EmptyAttributeKey()
        {
            // Arrange
            var attributes = new Dictionary<string, string> { { string.Empty, "value" } };

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(attributes));

            // Assert
            Assert.Equal("Attribute key should be a string with a length greater than 0 and not exceeding 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_EmptyAttributeValue()
        {
            // Arrange
            var attributes = new Dictionary<string, string> {{"EmptyValue", string.Empty}};

            // does not throw
            var resource = new Resource(attributes);

            // Assert
            Assert.Single(resource.Attributes);
            Assert.Contains(new KeyValuePair<string, string>("EmptyValue", string.Empty), resource.Attributes);
        }

        [Fact]
        public void CreateResource_ExceedsLengthAttributeValue()
        {
            // Arrange
            var attributes = new Dictionary<string, string> { { "ExceedsLengthValue", RandomString(256) }};

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(attributes));

            // Assert
            Assert.Equal("Attribute value should be a string with a length not exceeding 255 characters.", ex.Message);
        }


        [Fact]
        public void CreateResource_MaxLengthAttributeValue()
        {
            // Arrange
            var attributes = new Dictionary<string, string> { { "ExceedsLengthValue", RandomString(255) } };

            // Act
            var resource = new Resource(attributes);

            // Assert
            Assert.NotNull(resource);
            Assert.NotNull(resource.Attributes);
            Assert.Single( resource.Attributes);
            Assert.Contains(attributes.Single(), resource.Attributes);
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
            var sourceAttributes = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "value1"),
                new KeyValuePair<string, string>("key1", "value1.1"),
            };
            var sourceResource = new Resource(sourceAttributes);

            var otherAttributes = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key2", "value2"),
            };

            var otherResource = new Resource(otherAttributes);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);

            Assert.Equal(2, newResource.Attributes.Count());
            Assert.Contains(new KeyValuePair<string, string>("key1", "value1"), newResource.Attributes);
            Assert.Contains(new KeyValuePair<string, string>("key2", "value2"), newResource.Attributes);
        }

        [Fact]
        public void MergeResource_SecondaryCanOverridePrimaryEmptyAttributeValue()
        {
            // Arrange
            var primaryAttributes = new Dictionary<string, string> { { "value", string.Empty } };
            var secondaryAttributes = new Dictionary<string, string> { { "value", "not empty" } };
            var primaryResource = new Resource(primaryAttributes);
            var secondaryResource = new Resource(secondaryAttributes);

            var newResource = primaryResource.Merge(secondaryResource);

            // Assert
            Assert.Single(newResource.Attributes);
            Assert.Contains(new KeyValuePair<string, string>("value", "not empty"), newResource.Attributes);
        }

        private static void AddAttributes(Dictionary<string, string> attributes, int attributeCount, int startIndex = 0)
        {
            for (var i = startIndex; i < attributeCount + startIndex; ++i)
            {
                attributes.Add($"{KeyName}{i}", $"{ValueName}{i}");
            }
        }

        private Dictionary<string, string> CreateAttributes(int attributeCount, int startIndex = 0)
        {
            var attributes = new Dictionary<string, string>();
            AddAttributes(attributes, attributeCount, startIndex);
            return attributes;
        }

        private static void ValidateAttributes(IEnumerable<KeyValuePair<string, string>> attributes, int startIndex = 0)
        {
            var keyValuePairs = attributes as KeyValuePair<string, string>[] ?? attributes.ToArray();
            for (var i = startIndex; i < keyValuePairs.Length; ++i)
            {
                Assert.Contains(new KeyValuePair<string, string>(
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

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }
    }
}
