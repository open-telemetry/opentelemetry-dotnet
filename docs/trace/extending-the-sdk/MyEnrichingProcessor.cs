// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
#pragma warning disable CS0618 // Type or member is obsolete

internal class MyEnrichingProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        // Enrich activity with additional tags.
        activity.SetTag("myCustomTag", "myCustomTagValue");

        // Enriching from Baggage.
        // The below snippet adds every Baggage item.
        foreach (var baggage in Baggage.GetBaggage())
        {
            activity.SetTag(baggage.Key, baggage.Value);
        }

        // The below snippet adds specific Baggage item.
        var deviceTypeFromBaggage = Baggage.GetBaggage("device.type");
        if (deviceTypeFromBaggage != null)
        {
            activity.SetTag("device.type", deviceTypeFromBaggage);
        }
    }
}
