// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

        var exportedItem = Assert.Single(attributes);
        Assert.Equal("key0", exportedItem.Key);
        Assert.Equal(0, (int)exportedItem.Value!);
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
    public void CreateFromArrayTest(int numberOfItems)
    {
        var sourceAttributes = Enumerable.Range(0, numberOfItems)
            .Select(i => new KeyValuePair<string, object?>($"key{i}", i))
            .ToArray();

        var attributes = LogRecordAttributeList.CreateFromEnumerable(sourceAttributes);

        Assert.Equal(sourceAttributes.Length, attributes.Count);
        for (var i = 0; i < attributes.Count; i++)
        {
            Assert.Equal(sourceAttributes[i], attributes[i]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
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

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(20)]
    public void IndexerSetterReplacesItems(int numberOfItems)
    {
        LogRecordAttributeList attributes = default;

        for (var i = 0; i < numberOfItems; i++)
        {
            attributes.Add($"key{i}", i);
        }

        for (var i = 0; i < numberOfItems; i++)
        {
            attributes[i] = new KeyValuePair<string, object?>($"new{i}", -i);
        }

        Assert.Equal(numberOfItems, attributes.Count);

        for (var i = 0; i < numberOfItems; i++)
        {
            Assert.Equal($"new{i}", attributes[i].Key);
            Assert.Equal(-i, (int)attributes[i].Value!);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    public void IndexerThrowsWhenIndexIsOutOfRange(int numberOfItems)
    {
        LogRecordAttributeList attributes = default;

        for (var i = 0; i < numberOfItems; i++)
        {
            attributes.Add($"key{i}", i);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => attributes[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => attributes[numberOfItems]);
        Assert.Throws<ArgumentOutOfRangeException>(() => attributes[-1] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => attributes[numberOfItems] = default);
    }

    [Fact]
    public void RecordExceptionAddsExceptionAttributes()
    {
        LogRecordAttributeList attributes = default;

        attributes.RecordException(new InvalidOperationException("boom"));

        Assert.Equal(3, attributes.Count);

        Assert.Equal(Trace.SemanticConventions.AttributeExceptionType, attributes[0].Key);
        Assert.Equal(nameof(InvalidOperationException), attributes[0].Value);

        Assert.Equal(Trace.SemanticConventions.AttributeExceptionMessage, attributes[1].Key);
        Assert.Equal("boom", attributes[1].Value);

        Assert.Equal(Trace.SemanticConventions.AttributeExceptionStacktrace, attributes[2].Key);
        Assert.Contains("boom", Assert.IsType<string>(attributes[2].Value), StringComparison.Ordinal);
    }

    [Fact]
    public void RecordExceptionThrowsForNullException()
    {
        LogRecordAttributeList attributes = default;

        Assert.Throws<ArgumentNullException>(() => attributes.RecordException(null!));
    }

    [Fact]
    public void CreateFromEnumerableThrowsForNullAttributes()
    {
        Assert.Throws<ArgumentNullException>(() => LogRecordAttributeList.CreateFromEnumerable(null!));
    }

    [Fact]
    public void InterfacesEnumerateAndIndexAttributes()
    {
        LogRecordAttributeList attributes = default;
        attributes.Add("key0", 0);
        attributes.Add("key1", 1);

        var list = Assert.IsType<IReadOnlyList<KeyValuePair<string, object?>>>(attributes, exactMatch: false);
        Assert.Equal(2, list.Count);
        Assert.Equal("key1", list[1].Key);
        Assert.Equal(["key0", "key1"], list.Select(x => x.Key));

        System.Collections.IEnumerable nonGeneric = attributes;
        var enumerator = nonGeneric.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<string, object?>("key0", 0), Assert.IsType<KeyValuePair<string, object?>>(enumerator.Current));
        Assert.Throws<NotSupportedException>(enumerator.Reset);
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
