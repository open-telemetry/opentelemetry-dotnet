// <copyright file="IgnoresAccessChecksToAttribute.cs" company="OpenTelemetry Authors">
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

namespace System.Runtime.CompilerServices;

/// <summary>
/// IgnoresAccessChecksToAttribute tells the runtime to bypass visibility checks
/// to some assembly. See: <see
/// href="https://github.com/dotnet/runtime/discussions/48024" />.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class IgnoresAccessChecksToAttribute : Attribute
{
    public IgnoresAccessChecksToAttribute(string assemblyName)
        => this.AssemblyName = assemblyName;

    public string AssemblyName { get; }
}
