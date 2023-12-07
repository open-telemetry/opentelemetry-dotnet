// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Instrumentation;

namespace OpenTelemetry.AotCompatibility.TestApp;

internal class PropertyFetcherAotTest
{
    [UnconditionalSuppressMessage("", "IL2026", Justification = "Property presence guaranteed by explicit hints.")]
    public static void Test()
    {
        var fetcher = new PropertyFetcher<BaseType>("Property");

        GuaranteeProperties<PayloadTypeWithBaseType>();
        var r = fetcher.TryFetch(new PayloadTypeWithBaseType(), out var value);
        Assert(r, "TryFetch base did not return true.");
        Assert(value!.GetType() == typeof(DerivedType), "TryFetch base value is not a derived type.");

        GuaranteeProperties<PayloadTypeWithDerivedType>();
        r = fetcher.TryFetch(new PayloadTypeWithDerivedType(), out value);
        Assert(r, "TryFetch derived did not return true.");
        Assert(value!.GetType() == typeof(DerivedType), "TryFetch derived value is not a derived type.");
    }

    private static void GuaranteeProperties<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>()
    {
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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