// <copyright file="PropertyFetcherAotTest.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
