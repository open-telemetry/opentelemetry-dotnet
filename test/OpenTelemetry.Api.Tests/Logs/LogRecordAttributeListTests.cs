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

    [Fact]
    public void Equals_Object_ReturnsTrueForSameAttributes()
    {
        var left = new LogRecordAttributeList
        {
            ["key1"] = "value1",
            ["key2"] = 123,
        };

        var right = new LogRecordAttributeList
        {
            ["key1"] = "value1",
            ["key2"] = 123,
        };

        Assert.True(left.Equals((object)right));
        Assert.True(((object)left).Equals(right));
    }

    [Fact]
    public void Equals_Object_ReturnsFalseForDifferentAttributes()
    {
        var left = new LogRecordAttributeList
        {
            ["key1"] = "value1",
            ["key2"] = 123,
        };

        var right = new LogRecordAttributeList
        {
            ["key1"] = "value2",
            ["key2"] = 123,
        };

        Assert.False(left.Equals((object)right));
        Assert.False(((object)left).Equals(right));
    }

    [Fact]
    public void Equals_Object_ReturnsFalseForAnotherType()
    {
        var left = new LogRecordAttributeList
        {
            ["key1"] = "value1",
            ["key2"] = 123,
        };

        var right = "foo";

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsTrueForSameAttributes()
    {
        var left = new LogRecordAttributeList
        {
            ["a"] = 1,
        };

        var right = new LogRecordAttributeList
        {
            ["a"] = 1,
        };

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferentCount()
    {
        var left = new LogRecordAttributeList
        {
            ["a"] = 1,
        };

        var right = default(LogRecordAttributeList);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Operator_Equality_ReturnsTrueForEqualLists()
    {
        var left = new LogRecordAttributeList
        {
            ["x"] = 42,
        };
        var right = new LogRecordAttributeList
        {
            ["x"] = 42,
        };

        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Fact]
    public void Operator_Equality_ReturnsFalseForDifferentLists()
    {
        var left = new LogRecordAttributeList
        {
            ["x"] = 42,
        };
        var right = new LogRecordAttributeList
        {
            ["x"] = 43,
        };

        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void GetHashCode_SameForEqualLists()
    {
        var left = new LogRecordAttributeList
        {
            ["foo"] = "bar",
        };
        var right = new LogRecordAttributeList
        {
            ["foo"] = "bar",
        };

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentLists()
    {
        var left = new LogRecordAttributeList
        {
            ["foo"] = "bar",
        };
        var right = new LogRecordAttributeList
        {
            ["foo"] = "baz",
        };

        Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
    }
}
