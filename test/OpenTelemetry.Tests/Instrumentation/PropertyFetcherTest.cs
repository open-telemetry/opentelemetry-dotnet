// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Tests;

public class PropertyFetcherTest
{
    [Fact]
    public void FetchValidProperty()
    {
        using var activity = new Activity("test");

        var fetch = new PropertyFetcher<string>("DisplayName");

        Assert.Equal(0, fetch.NumberOfInnerFetchers);

        Assert.True(fetch.TryFetch(activity, out string result));
        Assert.Equal(activity.DisplayName, result);

        Assert.Equal(1, fetch.NumberOfInnerFetchers);

        Assert.True(fetch.TryFetch(activity, out result));
        Assert.Equal(activity.DisplayName, result);

        Assert.Equal(1, fetch.NumberOfInnerFetchers);
    }

    [Fact]
    public void FetchInvalidProperty()
    {
        using var activity = new Activity("test");
        var fetch = new PropertyFetcher<string>("DisplayName2");
        Assert.False(fetch.TryFetch(activity, out string result));

        var fetchInt = new PropertyFetcher<int>("DisplayName2");
        Assert.False(fetchInt.TryFetch(activity, out int resultInt));

        Assert.Equal(default, result);
        Assert.Equal(default, resultInt);
    }

    [Fact]
    public void FetchNullProperty()
    {
        var fetch = new PropertyFetcher<string>("null");
        Assert.False(fetch.TryFetch(null, out _));
    }

    [Fact]
    public void FetchPropertyMultiplePayloadTypes()
    {
        var fetch = new PropertyFetcher<string>("Property");

        Assert.Equal(0, fetch.NumberOfInnerFetchers);

        Assert.True(fetch.TryFetch(new PayloadTypeA(), out string propertyValue));
        Assert.Equal("A", propertyValue);

        Assert.Equal(1, fetch.NumberOfInnerFetchers);

        Assert.True(fetch.TryFetch(new PayloadTypeB(), out propertyValue));
        Assert.Equal("B", propertyValue);

        Assert.Equal(2, fetch.NumberOfInnerFetchers);

        Assert.False(fetch.TryFetch(new PayloadTypeC(), out _));

        Assert.Equal(2, fetch.NumberOfInnerFetchers);

        Assert.False(fetch.TryFetch(null, out _));

        Assert.Equal(2, fetch.NumberOfInnerFetchers);
    }

    [Fact]
    public void FetchPropertyMultiplePayloadTypes_IgnoreTypesWithoutExpectedPropertyName()
    {
        var fetch = new PropertyFetcher<string>("Property");

        Assert.False(fetch.TryFetch(new PayloadTypeC(), out _));

        Assert.True(fetch.TryFetch(new PayloadTypeA(), out string propertyValue));
        Assert.Equal("A", propertyValue);
    }

    [Fact]
    public void FetchPropertyWithDerivedInstanceType()
    {
        var fetch = new PropertyFetcher<BaseType>("Property");

        Assert.True(fetch.TryFetch(new PayloadTypeWithBaseType(), out BaseType value));
        Assert.IsType<DerivedType>(value);
    }

    [Fact]
    public void FetchPropertyWithDerivedDeclaredType()
    {
        var fetch = new PropertyFetcher<BaseType>("Property");

        Assert.True(fetch.TryFetch(new PayloadTypeWithDerivedType(), out BaseType value));
        Assert.IsType<DerivedType>(value);
    }

    [Fact]
    public void FetchPropertyWhenPayloadIsValueType()
    {
        var fetch = new PropertyFetcher<BaseType>("Property");
        var ex = Assert.Throws<NotSupportedException>(() => fetch.TryFetch(new PayloadTypeIsValueType(), out BaseType value));
        Assert.Contains("PropertyFetcher can only operate on reference payload types.", ex.Message);
    }

    private struct PayloadTypeIsValueType
    {
        public PayloadTypeIsValueType()
        {
        }

        public DerivedType Property { get; set; } = new DerivedType();
    }

    private class PayloadTypeA
    {
        public string Property { get; set; } = "A";
    }

    private class PayloadTypeB
    {
        public string Property { get; set; } = "B";
    }

    private class PayloadTypeC
    {
    }

    private class BaseType
    {
    }

    private class DerivedType : BaseType
    {
    }

    private class PayloadTypeWithBaseType
    {
        public BaseType Property { get; set; } = new DerivedType();
    }

    private class PayloadTypeWithDerivedType
    {
        public DerivedType Property { get; set; } = new DerivedType();
    }
}
