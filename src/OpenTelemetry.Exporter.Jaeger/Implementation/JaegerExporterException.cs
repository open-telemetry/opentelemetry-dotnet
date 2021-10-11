// <copyright file="JaegerExporterException.cs" company="OpenTelemetry Authors">
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
using System;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
#pragma warning disable CA1032 // Implement standard exception constructors
#pragma warning disable CA1064 // Exceptions should be public
    internal sealed class JaegerExporterException : Exception
#pragma warning restore CA1064 // Exceptions should be public
#pragma warning restore CA1032 // Implement standard exception constructors
    {
        public JaegerExporterException(string message, Exception originalException)
            : base(message, originalException)
        {
        }
    }
}
