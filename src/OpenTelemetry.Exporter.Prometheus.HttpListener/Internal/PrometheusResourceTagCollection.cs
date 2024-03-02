// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus;

internal readonly struct PrometheusResourceTagCollection
{
    private readonly Resource resource;
    private readonly Predicate<string> resourceAttributeFilter;

    public PrometheusResourceTagCollection(Resource resource, Predicate<string> resourceAttributeFilter = null)
    {
        this.resource = resource;
        this.resourceAttributeFilter = resourceAttributeFilter;
    }

    public IEnumerable<KeyValuePair<string, object>> Attributes
    {
        get
        {
            if (this.resource == null || this.resourceAttributeFilter == null)
            {
                return Enumerable.Empty<KeyValuePair<string, object>>();
            }

            var attributeFilter = this.resourceAttributeFilter;

            return this.resource?.Attributes
                .Where(attribute => attributeFilter(attribute.Key));
        }
    }
}
