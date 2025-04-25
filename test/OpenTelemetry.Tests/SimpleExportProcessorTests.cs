// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class SimpleExportProcessorTests
{
    [Fact]
    public void Verify_SimpleExportProcessor_HandlesException()
    {
        int counter = 0;

        // here our exporter will throw an exception.
        var testExporter = new DelegatingExporter<object>
        {
            OnExportFunc = (batch) =>
            {
                counter++;
                throw new Exception("test exception");
            },
        };

        var testSimpleExportProcessor = new TestSimpleExportProcessor(testExporter);

        // Verify that the Processor catches and suppresses the exception.
        testSimpleExportProcessor.OnEnd(new object());

        // verify Exporter OnExport wall called.
        Assert.Equal(1, counter);
    }

    /// <summary>
    /// Testable class for abstract <see cref="SimpleExportProcessor{T}"/>.
    /// </summary>
    private class TestSimpleExportProcessor : SimpleExportProcessor<object>
    {
        public TestSimpleExportProcessor(BaseExporter<object> exporter)
            : base(exporter)
        {
        }
    }
}
