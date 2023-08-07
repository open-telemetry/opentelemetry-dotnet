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

#nullable enable
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
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
    private PropertyFetch? innerFetcher;

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
    public bool TryFetch(object obj, out T? value, bool skipObjNullCheck = false)
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
            return CreateFetcherForProperty(property!);

            static PropertyFetch CreateFetcherForProperty(PropertyInfo propertyInfo)
            {
                if (propertyInfo == null || !typeof(T).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    // returns null and wait for a valid payload to arrive.
                    return null;
                }

                var declaringType = propertyInfo.DeclaringType;
                if (declaringType!.IsValueType)
                {
                    throw new NotSupportedException(
                        $"Type: {declaringType.FullName} is a value type. PropertyFetcher can only operate on reference payload types.");
                }

                if (declaringType == typeof(object))
                {
                    // This is only necessary on .NET 7.
                    // It was used to force the AOT compiler to create an instantiation of the method with a reference type.
                    // The code for that instantiation can then be reused at runtime to create instantiation over any other reference.
                    // TODO: when adding net8.0 as the project target:
                    // This is not needed in.NET 8, because the compiler is improved and call into MakeGenericMethod will be AOT-compatible.
                    return CreateInstantiated<object>(propertyInfo);
                }
                else
                {
                    return DynamicInstantiationHelper(declaringType, propertyInfo);
                }

                // Separated as local function to be able to target the suppression to just this call
                // IL3050 was generated here because of the call to MakeGenericType, which is problematic in AOT if one of the type parameters is a value type;
                // because the compiler might need to generate code specific to that type.
                // If the type parameter is reference type, there will be no problem; because the generated code can be shared among all reference type instantiations.
#if NET6_0_OR_GREATER
                [RequiresUnreferencedCode(TrimCompatibilityMessage)]
                [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The code guarantees that all the generic parameters are reference types.")]
#endif
                static PropertyFetch DynamicInstantiationHelper(Type declaringType, PropertyInfo propertyInfo)
                {
                    return (PropertyFetch)typeof(PropertyFetch)
                        .GetMethod(nameof(CreateInstantiated), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(declaringType) // This is validated in the earlier call chain to be a reference type.
                        .Invoke(null, new object[] { propertyInfo })!;
                }
            }
        }

        public virtual bool TryFetch(object obj, out T? value)
        {
            value = default;
            return false;
        }

        // Goal: make PropertyFetcher AOT-compatible.
        // AOT compiler can't guarantee correctness when call into MakeGenericType or MakeGenericMethod
        // if one of the generic parameters is a value type (reference types are OK.)
        // For PropertyFetcher, the decision was made to only support reference type payloads, i.e.:
        // the object from which to get the property value MUST be a reference type.
        // Create generics with the declared object type as a generic parameter is OK, but we need the return type
        // of the property to be a value type (on top of reference types.)
        // Normally, we would have a helper class like `PropertyFetchInstantiated` that takes 2 generic parameters,
        // the declared object type, and the type of the property value.
        // But that would mean calling MakeGenericType, with value type parameters which AOT won't support.
        //
        // As a workaround, Generic instantiation was split into:
        // 1. The property value type comes from the PropertyFetcher generic parameter.
        //    Compiler supports it even if it is a value type; the type is known statically during compilation
        //    since PropertyFetcher is used with it.
        // 2. Then, the declared object type was passed as a generic parameter to a generic method on PropertyFetcher<T> (or nested type.)
        //    Therefore, calling into MakeGenericMethod will only require specifying one parameter - the declared object type.
        //    The declared object type is guaranteed to be a reference type (throw on value type.) Thus, MakeGenericMethod is AOT compatible.
        private static PropertyFetch CreateInstantiated<TDeclaredObject>(PropertyInfo propertyInfo)
            where TDeclaredObject : class
            => new PropertyFetchInstantiated<TDeclaredObject>(propertyInfo);

        // ReferenceTypePropertyFetch is the optimized version because it uses CreateDelegate to get a Delegate directly to get the property.
#if NET6_0_OR_GREATER
        [RequiresUnreferencedCode(TrimCompatibilityMessage)]
#endif
        private sealed class PropertyFetchInstantiated<TDeclaredObject> : PropertyFetch
            where TDeclaredObject : class
        {
            private readonly string propertyName;
            private readonly Func<TDeclaredObject, T> propertyFetch;
            private PropertyFetch? innerFetcher;

            public PropertyFetchInstantiated(PropertyInfo property)
            {
                this.propertyName = property.Name;
                this.propertyFetch = (Func<TDeclaredObject, T>)property.GetMethod!.CreateDelegate(typeof(Func<TDeclaredObject, T>));
            }

            public override bool TryFetch(object obj, out T? value)
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
    }
}
