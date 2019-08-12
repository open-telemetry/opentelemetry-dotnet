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
        private const string keyName = "key";
        private const string valueName = "value";
        private static readonly Random random = new Random();

        [Fact]
        public static void CreateResource_NullLabelCollection()
        {
            // Act and Assert
            Exception ex = Assert.Throws<ArgumentNullException>(() => Resource.Create(null));
        }

        [Fact]
        public void CreateResource_NullLabelValue()
        {
            // Arrange
            var labelCount = 3;
            var labels = CreateLabels(labelCount);
            labels.Add("NullValue", null);

            // Act
            Exception ex = Assert.Throws<ArgumentException>(() => Resource.Create(labels));

            // Assert
            Assert.Equal("Label value should be a string with a length greater than 0 and not exceed 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_EmptyLabelValue()
        {
            // Arrange
            var labelCount = 3;
            var labels = CreateLabels(labelCount);
            labels.Add("EmptyValue", string.Empty);

            // Act
            Exception ex = Assert.Throws<ArgumentException>(() => Resource.Create(labels));

            // Assert
            Assert.Equal("Label value should be a string with a length greater than 0 and not exceed 255 characters.", ex.Message);
        }

        [Fact]
        public void CreateResource_ExceedsLengthLabelValue()
        {
            // Arrange
            var labelCount = 3;
            var labels = CreateLabels(labelCount);
            labels.Add("ExceedsLengthValue", RandomString(256));

            // Act
            Exception ex = Assert.Throws<ArgumentException>(() => Resource.Create(labels));

            // Assert
            Assert.Equal("Label value should be a string with a length greater than 0 and not exceed 255 characters.", ex.Message);
        }


        [Fact]
        public void CreateResource_MaxLengthLabelValue()
        {
            // Arrange
            var labelCount = 3;
            var labels = CreateLabels(labelCount);
            labels.Add("MaxLengthValue", RandomString(255));

            // Act
            var resource = Resource.Create(labels);

            // Assert
            Assert.NotNull(resource);
            Assert.NotNull(resource.Labels);
            Assert.True(resource.Labels.Count == labelCount + 1);
        }

        [Fact]
        public void CreateResource_EmptyLabel()
        {
            // Arrange
            var labelCount = 0;
            var labels = CreateLabels(labelCount);

            // Act
            var resource = Resource.Create(labels);

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
            var resource = Resource.Create(labels);

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
            var resource = Resource.Create(labels);

            // Assert
            ValidateResource(resource, labelCount);
        }

        [Fact]
        public void MergeResource_EmptyLabelSource_MultiLabelTarget()
        {
            // Arrange
            var sourceLabelCount = 0;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = Resource.Create(sourceLabels);

            var targetLabelCount = 3;
            var targetLabels = CreateLabels(targetLabelCount);
            var targetResource = Resource.Create(targetLabels);

            // Act
            targetResource.Merge(sourceResource);

            // Assert
            ValidateResource(targetResource, sourceLabelCount + targetLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_EmptyLabelTarget()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = Resource.Create(sourceLabels);

            var targetLabelCount = 0;
            var targetLabels = CreateLabels(targetLabelCount);
            var targetResource = Resource.Create(targetLabels);

            // Act
            targetResource.Merge(sourceResource);

            // Assert
            ValidateResource(targetResource, sourceLabelCount + targetLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_NoOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = Resource.Create(sourceLabels);

            var targetLabelCount = 3;
            var targetLabels = CreateLabels(targetLabelCount, sourceLabelCount);
            var targetResource = Resource.Create(targetLabels);

            // Act
            targetResource.Merge(sourceResource);

            // Assert
            ValidateResource(targetResource, sourceLabelCount + targetLabelCount);
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_SingleOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = Resource.Create(sourceLabels);

            var targetLabelCount = 3;
            var targetLabels = CreateLabels(targetLabelCount, sourceLabelCount - 1);
            var targetResource = Resource.Create(targetLabels);

            // Act
            targetResource.Merge(sourceResource);

            // Assert
            ValidateResource(targetResource, sourceLabelCount + targetLabelCount - 1);

            // Also verify target labels were not overwritten
            foreach (var targetLabel in targetLabels)
            {
                Assert.True(targetResource.Labels.ContainsKey(targetLabel.Key));
                Assert.Equal(targetLabel.Value, targetResource.Labels[targetLabel.Key]);
            }
        }

        [Fact]
        public void MergeResource_MultiLabelSource_MultiLabelTarget_FullOverlap()
        {
            // Arrange
            var sourceLabelCount = 3;
            var sourceLabels = CreateLabels(sourceLabelCount);
            var sourceResource = Resource.Create(sourceLabels);

            var targetLabelCount = 3;
            var targetLabels = CreateLabels(targetLabelCount);
            var targetResource = Resource.Create(targetLabels);

            // Act
            targetResource.Merge(sourceResource);

            // Assert
            ValidateResource(targetResource, targetLabelCount);

            // Also verify target labels were not overwritten
            foreach (var targetLabel in targetLabels)
            {
                Assert.True(targetResource.Labels.ContainsKey(targetLabel.Key));
                Assert.Equal(targetLabel.Value, targetResource.Labels[targetLabel.Key]);
            }
        }

        private static void AddLabels(Dictionary<string, string> labels, int labelCount, int startIndex = 0)
        {
            for (var i = startIndex; i < labelCount + startIndex; ++i)
            {
                labels.Add($"{keyName}{i}", $"{valueName}{i}");
            }
        }

        private Dictionary<string, string> CreateLabels(int labelCount, int startIndex = 0)
        {
            var labels = new Dictionary<string, string>();
            AddLabels(labels, labelCount, startIndex);
            return labels;
        }

        private static void ValidateLabels(IReadOnlyDictionary<string, string> labels, int startIndex = 0)
        {
            for (var i = startIndex; i < labels.Count; ++i)
            {
                Assert.True(labels.ContainsKey($"{keyName}{i}"));
                Assert.Equal($"{valueName}{i}", labels[$"{keyName}{i}"]);
            }
        }

        private static void ValidateResource(Resource resource, int labelCount)
        {
            Assert.NotNull(resource);
            Assert.NotNull(resource.Labels);
            Assert.True(resource.Labels.Count == labelCount);
            ValidateLabels(resource.Labels);
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
