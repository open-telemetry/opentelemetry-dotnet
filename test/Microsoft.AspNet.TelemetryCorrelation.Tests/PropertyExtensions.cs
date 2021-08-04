// <copyright file="PropertyExtensions.cs" company="OpenTelemetry Authors">
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

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System.Reflection;

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object obj, string propertyName)
        {
            return obj.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(obj);
        }
    }
}