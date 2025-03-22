// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SpanAttributesTests
{
    [Fact]
    public void ValidateConstructor()
    {
        var spanAttribute = new SpanAttributes();
        Assert.Empty(spanAttribute.Attributes);
    }

    [Fact]
    public void ValidateAddMethods()
    {
        var spanAttribute = new SpanAttributes();
        spanAttribute.Add("key_string", "string");
        spanAttribute.Add("key_a_string", ["string"]);

        spanAttribute.Add("key_double", 1.01);
        spanAttribute.Add("key_a_double", [1.01]);

        spanAttribute.Add("key_bool", true);
        spanAttribute.Add("key_a_bool", [true]);

        spanAttribute.Add("key_long", 1);
        spanAttribute.Add("key_a_long", [1L]);

        Assert.Equal(8, spanAttribute.Attributes.Count);
    }

    [Fact]
    public void ValidateNullKey()
    {
        var spanAttribute = new SpanAttributes();
        Assert.Throws<ArgumentNullException>(() => spanAttribute.Add(null!, "null key"));
    }

    [Fact]
    public void ValidateSameKey()
    {
        var spanAttribute = new SpanAttributes();
        spanAttribute.Add("key", "value1");
        spanAttribute.Add("key", "value2");
        Assert.Equal("value2", spanAttribute.Attributes["key"]);
    }

    [Fact]
    public void ValidateConstructorWithList()
    {
        var spanAttributes = new SpanAttributes(
            new List<KeyValuePair<string, object?>>
            {
            new("Span attribute int", 1),
            new("Span attribute string", "str"),
            });
        Assert.Equal(2, spanAttributes.Attributes.Count);
    }

    [Fact]
    public void ValidateConstructorWithNullList()
    {
        Assert.Throws<ArgumentNullException>(() => new SpanAttributes(null!));
    }
}
