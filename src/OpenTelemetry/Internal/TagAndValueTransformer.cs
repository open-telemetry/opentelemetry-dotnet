// <copyright file="TagAndValueTransformer.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Internal;

internal abstract class TagAndValueTransformer<T, TValue> : TagTransformer<T>
{
    protected TValue TransformValue(object value)
    {
        Debug.Assert(value != null, $"{nameof(value)} was null");

        switch (value)
        {
            case char:
            case string:
                return this.TransformStringValue(Convert.ToString(value));
            case bool b:
                return this.TransformBooleanValue(b);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                return this.TransformIntegralValue(Convert.ToInt64(value));
            case float:
            case double:
                return this.TransformFloatingPointValue(Convert.ToDouble(value));
            default:
                // This could throw an exception. The caller is expected to handle.
                return this.TransformStringValue(value.ToString());
        }
    }

    protected abstract TValue TransformIntegralValue(long value);

    protected abstract TValue TransformFloatingPointValue(double value);

    protected abstract TValue TransformBooleanValue(bool value);

    protected abstract TValue TransformStringValue(string value);

    protected abstract TValue TransformArrayValue(Array value);
}
