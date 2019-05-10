// <copyright file="PropertyFetcher.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Collector.AspNetCore.Common
{
    using System;
    using System.Reflection;

    internal class PropertyFetcher
    {
        private readonly string propertyName;
        private PropertyFetch innerFetcher;

        public PropertyFetcher(string propertyName)
        {
            this.propertyName = propertyName;
        }

        public object Fetch(object obj)
        {
            if (this.innerFetcher == null)
            {
                var type = obj.GetType().GetTypeInfo();
                var property = type.GetDeclaredProperty(this.propertyName);
                if (property == null)
                {
                    property = type.GetProperty(this.propertyName);
                }

                this.innerFetcher = PropertyFetch.FetcherForProperty(property);
            }

            return this.innerFetcher?.Fetch(obj);
        }

        // see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs
        private class PropertyFetch
        {
            /// <summary>
            /// Create a property fetcher from a .NET Reflection PropertyInfo class that
            /// represents a property of a particular type.
            /// </summary>
            public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
            {
                if (propertyInfo == null)
                {
                    // returns null on any fetch.
                    return new PropertyFetch();
                }

                var typedPropertyFetcher = typeof(TypedFetchProperty<,>);
                var instantiatedTypedPropertyFetcher = typedPropertyFetcher.GetTypeInfo().MakeGenericType(
                    propertyInfo.DeclaringType, propertyInfo.PropertyType);
                return (PropertyFetch)Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
            }

            /// <summary>
            /// Given an object, fetch the property that this propertyFetch represents.
            /// </summary>
            public virtual object Fetch(object obj)
            {
                return null;
            }

            private class TypedFetchProperty<TObject, TProperty> : PropertyFetch
            {
                private readonly Func<TObject, TProperty> propertyFetch;

                public TypedFetchProperty(PropertyInfo property)
                {
                    this.propertyFetch = (Func<TObject, TProperty>)property.GetMethod.CreateDelegate(typeof(Func<TObject, TProperty>));
                }

                public override object Fetch(object obj)
                {
                    return this.propertyFetch((TObject)obj);
                }
            }
        }
    }
}