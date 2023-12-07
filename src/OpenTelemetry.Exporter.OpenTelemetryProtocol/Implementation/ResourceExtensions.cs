// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal static class ResourceExtensions
{
    public static OtlpResource.Resource ToOtlpResource(this Resource resource)
    {
        var processResource = new OtlpResource.Resource();

        foreach (KeyValuePair<string, object> attribute in resource.Attributes)
        {
            if (OtlpKeyValueTransformer.Instance.TryTransformTag(attribute, out var result))
            {
                processResource.Attributes.Add(result);
            }
        }

        if (!processResource.Attributes.Any(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName))
        {
            var serviceName = (string)ResourceBuilder.CreateDefault().Build().Attributes.FirstOrDefault(
                kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName).Value;
            processResource.Attributes.Add(new OtlpCommon.KeyValue
            {
                Key = ResourceSemanticConventions.AttributeServiceName,
                Value = new OtlpCommon.AnyValue { StringValue = serviceName },
            });
        }

        return processResource;
    }
}