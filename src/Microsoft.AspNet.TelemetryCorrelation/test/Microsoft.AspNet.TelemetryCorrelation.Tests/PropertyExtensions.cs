// <copyright file="PropertyExtensions.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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