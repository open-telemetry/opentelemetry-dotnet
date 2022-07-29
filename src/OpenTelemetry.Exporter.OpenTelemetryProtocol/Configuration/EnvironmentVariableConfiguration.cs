// <copyright file="EnvironmentVariableConfiguration.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration;

internal class EnvironmentVariableConfiguration
{
    public static void InitializeDefaultConfigurationFromEnvironment(SdkConfiguration sdkConfiguration)
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#attribute-limits
        SetIntConfigValue("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => sdkConfiguration.AttributeValueLengthLimit = value);
        SetIntConfigValue("OTEL_ATTRIBUTE_COUNT_LIMIT", value => sdkConfiguration.AttributeCountLimit = value);

        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#span-limits
        SetIntConfigValue("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", value => sdkConfiguration.SpanAttributeValueLengthLimit = value);
        SetIntConfigValue("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", value => sdkConfiguration.SpanAttributeCountLimit = value);
        SetIntConfigValue("OTEL_SPAN_EVENT_COUNT_LIMIT", value => sdkConfiguration.SpanEventCountLimit = value);
        SetIntConfigValue("OTEL_SPAN_LINK_COUNT_LIMIT", value => sdkConfiguration.SpanLinkCountLimit = value);
        SetIntConfigValue("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", value => sdkConfiguration.EventAttributeCountLimit = value);
        SetIntConfigValue("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", value => sdkConfiguration.LinkAttributeCountLimit = value);
    }

    private static void SetIntConfigValue(string key, Action<int> setter)
    {
        if (EnvironmentVariableHelper.LoadNumeric(key, out var result))
        {
            setter(result);
        }
    }
}
