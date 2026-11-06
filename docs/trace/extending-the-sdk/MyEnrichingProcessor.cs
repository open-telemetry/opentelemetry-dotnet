// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;

internal sealed class MyEnrichingProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        // Enrich activity with additional tags.
        data.SetTag("myCustomTag", "myCustomTagValue");

        // Enriching from Baggage.
        // The below snippet adds every Baggage item.
        foreach (var baggage in Baggage.GetBaggage())
        {
            data.SetTag(baggage.Key, baggage.Value);
        }

        // The below snippet adds specific Baggage item.
        var deviceTypeFromBaggage = Baggage.GetBaggage("device.type");
        if (deviceTypeFromBaggage != null)
        {
            data.SetTag("device.type", deviceTypeFromBaggage);
        }
    }
}
