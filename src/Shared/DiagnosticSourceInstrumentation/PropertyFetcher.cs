// <copyright file="PropertyFetcher.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#endif
using System.Reflection;

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// PropertyFetcher fetches a property from an object.
/// </summary>
/// <typeparam name="T">The type of the property being fetched.</typeparam>
internal sealed class PropertyFetcher<T>
{
#if NET6_0_OR_GREATER
    private const string TrimCompatibilityMessage = "PropertyFetcher is used to access properties on objects dynamically by design and cannot be made trim compatible.";
#endif
    private readonly string propertyName;
    private PropertyFetch innerFetcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyFetcher{T}"/> class.
    /// </summary>
    /// <param name="propertyName">Property name to fetch.</param>
    public PropertyFetcher(string propertyName)
    {
        this.propertyName = propertyName;
    }

    /// <summary>
    /// Try to fetch the property from the object.
    /// </summary>
    /// <param name="obj">Object to be fetched.</param>
    /// <param name="value">Fetched value.</param>
    /// <param name="skipObjNullCheck">Set this to <see langword= "true"/> if we know <paramref name="obj"/> is not <see langword= "null"/>.</param>
    /// <returns><see langword= "true"/> if the property was fetched.</returns>
#if NET6_0_OR_GREATER
    [RequiresUnreferencedCode(TrimCompatibilityMessage)]
#endif
    public bool TryFetch(object obj, out T value, bool skipObjNullCheck = false)
    {
        if (!skipObjNullCheck && obj == null)
        {
            value = default;
            return false;
        }

        if (this.innerFetcher == null)
        {
            this.innerFetcher = PropertyFetch.Create(obj.GetType().GetTypeInfo(), this.propertyName);
        }

        if (this.innerFetcher == null)
        {
            value = default;
            return false;
        }

        return this.innerFetcher.TryFetch(obj, out value);
    }

    // see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs
#if NET6_0_OR_GREATER
    [RequiresUnreferencedCode(TrimCompatibilityMessage)]
#endif
    private class PropertyFetch
    {
        public static PropertyFetch Create(TypeInfo type, string propertyName)
        {
            var property = type.DeclaredProperties.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase)) ?? type.GetProperty(propertyName);
            return CreateFetcherForProperty(property);

            static PropertyFetch CreateFetcherForProperty(PropertyInfo propertyInfo)
            {
                if (propertyInfo == null || !typeof(T).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    // returns null and wait for a valid payload to arrive.
                    return null;
                }

                if (
#if NET6_0_OR_GREATER
!RuntimeFeature.IsDynamicCodeSupported &&
#endif
                IsValueType(propertyInfo))
                {
                    return new BoxedValueTypePropertyFetch(propertyInfo);
                }
                else
                {
                    return CreateReferencedTypePropertyFetch(propertyInfo);
                }

                // IL3050 was generated here because of the call to MakeGenericType, which is problematic in AOT if one of the type parameters is a value type;
                // because the compiler might need to generate code specific to that type.
                // If ALL the type parameters are reference types, there will be no problem; because the generated code can be shared among all reference type instantiations.
#if NET6_0_OR_GREATER
                [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code guarantees that all the generic parameters are reference types")]
#endif
                static PropertyFetch CreateReferencedTypePropertyFetch(PropertyInfo propertyInfo)
                {
                    var typedPropertyFetcher = typeof(ReferenceTypedPropertyFetch<,>);
                    var instantiatedTypedPropertyFetcher = typedPropertyFetcher.MakeGenericType(
                        typeof(T), propertyInfo.DeclaringType, propertyInfo.PropertyType);
                    return (PropertyFetch)Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
                }

                static bool IsValueType(PropertyInfo propertyInfo)
                {
                    return propertyInfo.DeclaringType!.IsValueType || propertyInfo.PropertyType.IsValueType || typeof(T).IsValueType;
                }
            }
        }

        public virtual bool TryFetch(object obj, out T value)
        {
            value = default;
            return false;
        }

        // 1. ReferenceTypePropertyFetch is the optimized version because it uses CreateDelegate to get a Delegate directly to get the property.
        // 2. CreateDelegate is not AOT compatible if any of the types (DeclaringType, property or T) is a value type.
        private sealed class ReferenceTypedPropertyFetch<TDeclaredObject, TDeclaredProperty> : PropertyFetch
            where TDeclaredProperty : class, T
            where TDeclaredObject : class
        {
            private readonly string propertyName;
            private readonly Func<TDeclaredObject, TDeclaredProperty> propertyFetch;
            private PropertyFetch innerFetcher;

            public ReferenceTypedPropertyFetch(PropertyInfo property)
            {
                this.propertyName = property.Name;
                this.propertyFetch = (Func<TDeclaredObject, TDeclaredProperty>)property.GetMethod.CreateDelegate(typeof(Func<TDeclaredObject, TDeclaredProperty>));
            }

            public override bool TryFetch(object obj, out T value)
            {
                if (obj is TDeclaredObject o)
                {
                    value = this.propertyFetch(o);
                    return true;
                }

                this.innerFetcher ??= Create(obj.GetType().GetTypeInfo(), this.propertyName);

                if (this.innerFetcher == null)
                {
                    value = default;
                    return false;
                }

                return this.innerFetcher.TryFetch(obj, out value);
            }
        }

#if NET6_0_OR_GREATER
        [RequiresUnreferencedCode(TrimCompatibilityMessage)]
#endif
        private sealed class BoxedValueTypePropertyFetch : PropertyFetch
        {
            private readonly string propertyName;
            private readonly Func<object, T> propertyFetch;
            private readonly Type payloadType;
            private PropertyFetch innerFetcher;

            public BoxedValueTypePropertyFetch(PropertyInfo property)
            {
                this.propertyName = property.Name;
                this.propertyFetch = payload => (T)property.GetValue(payload);
                this.payloadType = property.DeclaringType;
            }

            public override bool TryFetch(object obj, out T value)
            {
                if (obj.GetType() == this.payloadType)
                {
                    value = this.propertyFetch(obj);
                    return true;
                }

                this.innerFetcher ??= Create(obj.GetType().GetTypeInfo(), this.propertyName);

                if (this.innerFetcher == null)
                {
                    value = default;
                    return false;
                }

                return this.innerFetcher.TryFetch(obj, out value);
            }
        }
    }
}
