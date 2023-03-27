// <copyright file="NullableAttributes.cs" company="OpenTelemetry Authors">
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

// The following code was borrowed from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/NullableAttributes.cs
// These classes are available in .NET versions after NetStandard2.0
// For more information see: https://learn.microsoft.com/dotnet/csharp/language-reference/attributes/nullable-analysis

#nullable enable

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Specifies that an output is not <see langword="null"/> even if
    /// the corresponding type allows it. Specifies that an input argument was
    /// not <see langword="null"/> when the call returns.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute
    {
    }

    /// <summary>Specifies that null is allowed as an input even if the corresponding type disallows it.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false)]
    internal sealed class AllowNullAttribute : Attribute
    {
    }

    /// <summary>Specifies that an output may be null even if the corresponding type disallows it.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class MaybeNullAttribute : Attribute
    {
    }

    /// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter will not be null even if the corresponding type allows it.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="NotNullWhenAttribute"/> class with the specified return value condition.</summary>
        /// <param name="returnValue">
        /// The return value condition. If the method returns this value, the associated parameter will not be null.
        /// </param>
        public NotNullWhenAttribute(bool returnValue) => this.ReturnValue = returnValue;

        /// <summary>Gets a value indicating whether the return value condition is not null.</summary>
        public bool ReturnValue { get; }
    }
}
#endif
