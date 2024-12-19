// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

internal class ExampleService(ILogger<ExampleService> logger)
{
    public void DoSomeWork()
    {
        logger.FoodPriceChanged("artichoke", 9.99);

        logger.FoodRecallNotice(
            brandName: "Contoso",
            productDescription: "Salads",
            productType: "Food & Beverages",
            recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
            companyName: "Contoso Fresh Vegetables, Inc.");
    }
}
