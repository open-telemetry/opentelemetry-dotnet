// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordAttributeListTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(64)]
    public void ReadWriteTest(int numberOfItems)
    {
        LogRecordAttributeList attributes = default;

        for (int i = 0; i < numberOfItems; i++)
        {
            attributes.Add($"key{i}", i);
        }

        Assert.Equal(numberOfItems, attributes.Count);

        for (int i = 0; i < numberOfItems; i++)
        {
            var item = attributes[i];

            Assert.Equal($"key{i}", item.Key);
            Assert.NotNull(item.Value);
            Assert.Equal(i, (int)item.Value);
        }

        int index = 0;
        foreach (KeyValuePair<string, object?> item in attributes)
        {
            Assert.Equal($"key{index}", item.Key);
            Assert.NotNull(item.Value);
            Assert.Equal(index, (int)item.Value);
            index++;
        }

        if (attributes.Count <= LogRecordAttributeList.OverflowMaxCount)
        {
            Assert.Null(attributes.OverflowAttributes);
        }
        else
        {
            Assert.NotNull(attributes.OverflowAttributes);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(64)]
    public void ClearTest(int numberOfItems)
    {
        LogRecordAttributeList attributes = default;

        for (int c = 0; c <= 1; c++)
        {
            for (int i = 0; i < numberOfItems; i++)
            {
                attributes.Add($"key{i}", i);
            }

            Assert.Equal(numberOfItems, attributes.Count);

            for (int i = 0; i < numberOfItems; i++)
            {
                var item = attributes[i];

                Assert.Equal($"key{i}", item.Key);
                Assert.NotNull(item.Value);
                Assert.Equal(i, (int)item.Value);
            }

            attributes.Clear();

            Assert.Empty(attributes);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(64)]
    public void ExportTest(int numberOfItems)
    {
        LogRecordAttributeList attributes = default;

        for (int i = 0; i < numberOfItems; i++)
        {
            attributes.Add($"key{i}", i);
        }

        List<KeyValuePair<string, object?>>? storage = null;

        var exportedAttributes = attributes.Export(ref storage);

        if (numberOfItems == 0)
        {
            Assert.Empty(exportedAttributes);
            Assert.Null(storage);
            return;
        }

        Assert.NotNull(exportedAttributes);

        if (numberOfItems <= LogRecordAttributeList.OverflowMaxCount)
        {
            Assert.NotNull(storage);
            Assert.True(ReferenceEquals(storage, exportedAttributes));
        }
        else
        {
            Assert.Null(storage);
        }

        int index = 0;
        foreach (KeyValuePair<string, object?> item in exportedAttributes)
        {
            Assert.Equal($"key{index}", item.Key);
            Assert.NotNull(item.Value);
            Assert.Equal(index, (int)item.Value);
            index++;
        }
    }

    [Fact]
    public void InitializerAddSyntaxTest()
    {
        LogRecordAttributeList list = new LogRecordAttributeList
        {
            { "key1", new object() },
            { "key2", 2 },
        };

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void InitializerIndexesSyntaxTest()
    {
        LogRecordAttributeList list = new LogRecordAttributeList
        {
            ["key1"] = new object(),
            ["key2"] = 2,
        };

        Assert.Equal(2, list.Count);
    }
}
