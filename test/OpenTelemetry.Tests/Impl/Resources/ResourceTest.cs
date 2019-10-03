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

namespace OpenTelemetry.Impl.Resources
{
    using Xunit;
    using OpenTelemetry.Resources;
    using System.Collections.Generic;
    using System;
    using System.Linq;

    public class ResourceTest
    {
        private const string KeyName = "key";
        private const string ValueName = "value";
        private static readonly Random Random = new Random();

        [Fact]
        public static void CreateResource_NullLabelCollection()
        {
            // Act and Assert
            Assert.Throws<ArgumentNullException>(() => new Resource(null));
        }

        [Fact]
        public void CreateResource_NullLabelValue()
        {
            // Arrange
            var labelCount = 3;
            var labels = CreateLabels(labelCount);
            labels.Add("NullValue", null);

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(labels));

            // Assert
            Assert.Equal("Label value should be a string with a length not exceeding 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_EmptyLabelKey()
        {
            // Arrange
            var labels = new Dictionary<string, string> { { string.Empty, "value" } };

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(labels));

            // Assert
            Assert.Equal("Label key should be a string with a length greater than 0 and not exceeding 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_EmptyLabelValue()
        {
            // Arrange
            var labels = new Dictionary<string, string> {{"EmptyValue", string.Empty}};

            // does not throw
            var resource = new Resource(labels);

            // Assert
            Assert.Single(resource.Labels);
            Assert.Contains(new KeyValuePair<string, string>("EmptyValue", string.Empty), resource.Labels);
        }

        [Fact]
        public void CreateResource_ExceedsLengthLabelValue()
        {
            // Arrange
            var labels = new Dictionary<string, string> { { "ExceedsLengthValue", RandomString(256) }};

            // Act
            var ex = Assert.Throws<ArgumentException>(() => new Resource(labels));

            // Assert
            Assert.Equal("Label value should be a string with a length not exceeding 255 characters.", ex.Message);
        }


        [Fact]
        public void CreateResource_MaxLengthLabelValue()
        {
            // Arrange
            var labels = new Dictionary<string, string> { { "ExceedsLengthValue", RandomString(255) } };

            // Act
            var resource = new Resource(labels);

            // Assert
            Assert.NotNull(resource);
            Assert.NotNull(resource.Labels);
            Assert.Single( resource.Labels);
            Assert.Contains(labels.Single(), resource.Labels);
        }

        [Fact]
        public void CreateResource_EmptyLabel()
        {
            // Arrange
            var labelCount = 0;
            var labels = CreateLabels(labelCount);

            // Act
            var resource = new Resource(labels);

            // Assert
            ValidateResource(resource, labelCount);
        }

        [Fact]
        public void CreateResource_SingleLabel()
        {
            // Arrange
            var labelCount = 1;
            var labels = CreateLabels(labelCount);

            // Act
            var resource = new Resource(labels);

            // Assert
            ValidateResource(resource, labelCount);
        }

        [Fact]
        public void CreateResource_MultipleLabel()
        {
            // Arrange
            var labelCount = 5;
            var labels = CreateLabels(labelCount);

            // Act
            var resource = new Resource(labels);

            // Assert
            ValidateResource(resource, labelCount);
        }

        [Fact]
        public void MergeResource_EmptyLabelSource_MultiLabelTarget()
        {
            // Arrange
            var sourceLabelCount = 0;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = new Resource(sourceLabels);

            var otherLabelCount = 3;
            var otherLabels = CreateLabels(otherLabelCount);
            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);

            ValidateResource(newResource, sourceLabelCount + otherLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_EmptyLabelTarget()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = new Resource(sourceLabels);

            var otherLabelCount = 0;
            var otherLabels = CreateLabels(otherLabelCount);
            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceLabelCount + otherLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_NoOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = new Resource(sourceLabels);

            var otherLabelCount = 3;
            var otherLabels = CreateLabels(otherLabelCount, sourceLabelCount);
            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceLabelCount + otherLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_SingleOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = new Resource(sourceLabels);

            var otherLabelCount = 3;
            var otherLabels = CreateLabels(otherLabelCount, sourceLabelCount - 1);
            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, sourceLabelCount + otherLabelCount - 1);

            // Also verify target labels were not overwritten
            foreach (var otherLabel in otherLabels)
            {
                Assert.Contains(otherLabel, otherResource.Labels);
            }
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_FullOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = new Resource(sourceLabels);

            var otherLabelCount = 3;
            var otherLabels = CreateLabels(otherLabelCount);
            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);
            ValidateResource(newResource, otherLabelCount);

            // Also verify target labels were not overwritten
            foreach (var otherLabel in otherLabels)
            {
                Assert.Contains(otherLabel, otherResource.Labels);
            }
        }

        [Fact]
        public void MergeResource_MultiLabelSource_DuplicatedKeysInPrimary()
        {
            // Arrange
            var sourceLabels = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "value1"),
                new KeyValuePair<string, string>("key1", "value1.1"),
            };
            var sourceResource = new Resource(sourceLabels);

            var otherLabels = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key2", "value2"),
            };

            var otherResource = new Resource(otherLabels);

            // Act
            var newResource = sourceResource.Merge(otherResource);

            // Assert
            Assert.NotSame(otherResource, newResource);
            Assert.NotSame(sourceResource, newResource);

            Assert.Equal(2, newResource.Labels.Count());
            Assert.Contains(new KeyValuePair<string, string>("key1", "value1"), newResource.Labels);
            Assert.Contains(new KeyValuePair<string, string>("key2", "value2"), newResource.Labels);
        }

        [Fact]
        public void MergeResource_SecondaryCanOverridePrimaryEmptyLabelValue()
        {
            // Arrange
            var primaryLabels = new Dictionary<string, string> { { "value", string.Empty } };
            var secondaryLabels = new Dictionary<string, string> { { "value", "not empty" } };
            var primaryResource = new Resource(primaryLabels);
            var secondaryResource = new Resource(secondaryLabels);

            var newResource = primaryResource.Merge(secondaryResource);

            // Assert
            Assert.Single(newResource.Labels);
            Assert.Contains(new KeyValuePair<string, string>("value", "not empty"), newResource.Labels);
        }

        private static void AddLabels(Dictionary<string, string> labels, int labelCount, int startIndex = 0)
        {
            for (var i = startIndex; i < labelCount + startIndex; ++i)
            {
                labels.Add($"{KeyName}{i}", $"{ValueName}{i}");
            }
        }

        private Dictionary<string, string> CreateLabels(int labelCount, int startIndex = 0)
        {
            var labels = new Dictionary<string, string>();
            AddLabels(labels, labelCount, startIndex);
            return labels;
        }

        private static void ValidateLabels(IEnumerable<KeyValuePair<string, string>> labels, int startIndex = 0)
        {
            var keyValuePairs = labels as KeyValuePair<string, string>[] ?? labels.ToArray();
            for (var i = startIndex; i < keyValuePairs.Length; ++i)
            {
                Assert.Contains(new KeyValuePair<string, string>(
                    $"{KeyName}{i}", $"{ValueName}{i}"), keyValuePairs);
            }
        }

        private static void ValidateResource(Resource resource, int labelCount)
        {
            Assert.NotNull(resource);
            Assert.NotNull(resource.Labels);
            Assert.Equal(labelCount, resource.Labels.Count());
            ValidateLabels(resource.Labels);
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }
    }
}
