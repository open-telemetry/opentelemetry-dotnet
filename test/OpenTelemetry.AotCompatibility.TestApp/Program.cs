// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.AotCompatibility.TestApp;

try
{
    PropertyFetcherAotTest.Test();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return -1;
}

Console.WriteLine("Passed.");
return 0;
