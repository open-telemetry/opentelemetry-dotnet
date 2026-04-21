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

        for (var i = 0; i < numberOfItems; i++)
        {
            attributes.Add($"key{i}", i);
        }

        Assert.Equal(numberOfItems, attributes.Count);

        for (var i = 0; i < numberOfItems; i++)
        {
            var item = attributes[i];

            Assert.Equal($"key{i}", item.Key);
            Assert.NotNull(item.Value);
            Assert.Equal(i, (int)item.Value);
        }

        var index = 0;
        foreach (var item in attributes)
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

        for (var c = 0; c <= 1; c++)
        {
            for (var i = 0; i < numberOfItems; i++)
            {
                attributes.Add($"key{i}", i);
            }

            Assert.Equal(numberOfItems, attributes.Count);

            for (var i = 0; i < numberOfItems; i++)
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

    [Fact]
    public void ClearAfterOverflowThenReuseWithInlineCountTest()
    {
        LogRecordAttributeList attributes = default;

        for (var i = 0; i <= LogRecordAttributeList.OverflowMaxCount; i++)
        {
            attributes.Add($"key{i}", i);
        }

        Assert.NotNull(attributes.OverflowAttributes);

        attributes.Clear();
        attributes.Add("key0", 0);

        var item = attributes[0];
        Assert.Equal("key0", item.Key);
        Assert.Equal(0, (int)item.Value!);

        Assert.Collection(
            attributes,
            exportedItem =>
            {
                Assert.Equal("key0", exportedItem.Key);
                Assert.Equal(0, (int)exportedItem.Value!);
            });
    }

    [Fact]
    public void CreateFromEnumerableSmallCountThenAddToOverflowTest()
    {
        var sourceAttributes = new List<KeyValuePair<string, object?>>
        {
            new("key0", 0),
            new("key1", 1),
            new("key2", 2),
        };

        var attributes = LogRecordAttributeList.CreateFromEnumerable(sourceAttributes);

        for (var i = 3; i <= LogRecordAttributeList.OverflowMaxCount; i++)
        {
            attributes.Add($"key{i}", i);
        }

        Assert.Equal(LogRecordAttributeList.OverflowMaxCount + 1, attributes.Count);

        for (var i = 0; i < attributes.Count; i++)
        {
            var item = attributes[i];
            Assert.Equal($"key{i}", item.Key);
            Assert.Equal(i, (int)item.Value!);
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

        for (var i = 0; i < numberOfItems; i++)
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

        var index = 0;
        foreach (var item in exportedAttributes)
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
        var list = new LogRecordAttributeList
        {
            { "key1", new object() },
            { "key2", 2 },
        };

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void InitializerIndexesSyntaxTest()
    {
        var list = new LogRecordAttributeList
        {
            ["key1"] = new object(),
            ["key2"] = 2,
        };

        Assert.Equal(2, list.Count);
    }
}
