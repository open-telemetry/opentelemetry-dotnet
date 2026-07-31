// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OtlpCollectorMetrics = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

/// <summary>
/// Tests covering the exception-safety of the <c>[ThreadStatic]</c> batch state
/// held by the OTLP serializers.
/// </summary>
/// <remarks>
/// <para><c>WriteTraceData</c>/<c>WriteLogsData</c>/<c>WriteMetricsData</c> group the
/// batch into thread-static dictionaries, serialize, then release that state in a
/// <c>finally</c>. Serialization deliberately rethrows when
/// <c>ProtobufSerializer.IncreaseBufferSize</c> refuses to grow past its 100 MiB
/// cap, and any other escaped exception takes the same path. These tests assert
/// that after such a failure the thread-static dictionaries are empty, pooled
/// <see cref="LogRecord"/> references are balanced, serialization buffers are not
/// retained, and the next export on the same thread serializes only its own batch -
/// the contract that prevents one
/// failed export from permanently poisoning the batch processor's dedicated
/// thread.</para>
/// <para>Reporting the dropped batch is the exporter's job, so it is covered by
/// the exporter tests rather than here.</para>
/// <para>Each test runs on its own thread so that any thread-static state left
/// behind cannot leak into unrelated tests.</para>
/// </remarks>
public class ProtobufOtlpSerializerExceptionSafetyTests
    : IClassFixture<MaxSizeSerializationBufferFixture>
{
    private readonly MaxSizeSerializationBufferFixture maxSizeBuffer;

    public ProtobufOtlpSerializerExceptionSafetyTests(MaxSizeSerializationBufferFixture maxSizeBuffer)
    {
        this.maxSizeBuffer = maxSizeBuffer;
    }

    [Fact]
    public void WriteTraceData_AfterFailedSerialization_DoesNotCarryStaleBatchIntoNextExport()
    {
        var failingBuffer = this.maxSizeBuffer.GetBuffer();

        RunOnDedicatedThread(() =>
        {
            using var sourceA = new ActivitySource(new ActivitySourceOptions($"{nameof(ProtobufOtlpSerializerExceptionSafetyTests)}.A"));
            using var sourceB = new ActivitySource(new ActivitySourceOptions($"{nameof(ProtobufOtlpSerializerExceptionSafetyTests)}.B"));
            using var listener = CreateActivityListener(sourceA, sourceB);

            using var activityA = sourceA.StartActivity("activity-a");
            using var activityB = sourceB.StartActivity("activity-b");

            Assert.NotNull(activityA);
            Assert.NotNull(activityB);

            var batchA = new Batch<Activity>([activityA], 1);

            // The first export fails: the buffer is already at the maximum size,
            // so the serializer cannot grow it and rethrows.
            Assert.Throws<IndexOutOfRangeException>(() => ProtobufOtlpTraceSerializer.WriteTraceData(
                ref failingBuffer,
                failingBuffer.Length,
                new SdkLimitOptions(),
                Resource.Empty,
                batchA));

            // The next export on the same thread must serialize only its own batch.
            var buffer = new byte[8 * 1024];
            var batchB = new Batch<Activity>([activityB], 1);
            var writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(
                ref buffer,
                0,
                new SdkLimitOptions(),
                Resource.Empty,
                batchB);

            using var stream = new MemoryStream(buffer, 0, writePosition);
            var tracesData = OtlpTrace.TracesData.Parser.ParseFrom(stream);

            var resourceSpans = Assert.Single(tracesData.ResourceSpans);
            var scopeSpans = Assert.Single(resourceSpans.ScopeSpans);
            Assert.Equal(sourceB.Name, scopeSpans.Scope?.Name);
            var span = Assert.Single(scopeSpans.Spans);
            Assert.Equal("activity-b", span.Name);
        });

        this.maxSizeBuffer.AssertNotSwapped(failingBuffer);
    }

    [Fact]
    public void WriteLogsData_AfterFailedSerialization_DoesNotCarryStaleBatchIntoNextExport()
    {
        var failingBuffer = this.maxSizeBuffer.GetBuffer();

        RunOnDedicatedThread(() =>
        {
            var logRecords = new List<LogRecord>();

            using (var loggerProvider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(logRecords)
                .Build())
            {
                loggerProvider.GetLogger("LoggerA").EmitLog(new LogRecordData());
                loggerProvider.GetLogger("LoggerB").EmitLog(new LogRecordData());
            }

            Assert.Equal(2, logRecords.Count);

            var batchA = new Batch<LogRecord>([logRecords[0]], 1);

            Assert.Throws<IndexOutOfRangeException>(() => ProtobufOtlpLogSerializer.WriteLogsData(
                ref failingBuffer,
                failingBuffer.Length,
                new SdkLimitOptions(),
                new ExperimentalOptions(),
                Resource.Empty,
                batchA));

            AssertOnlySecondBatchIsSerialized(logRecords[1], "LoggerB");
        });

        this.maxSizeBuffer.AssertNotSwapped(failingBuffer);
    }

    [Fact]
    public void WriteMetricsData_AfterFailedSerialization_DoesNotCarryStaleBatchIntoNextExport()
    {
        var failingBuffer = this.maxSizeBuffer.GetBuffer();

        RunOnDedicatedThread(() =>
        {
            var meterName = $"{nameof(ProtobufOtlpSerializerExceptionSafetyTests)}.{nameof(this.WriteMetricsData_AfterFailedSerialization_DoesNotCarryStaleBatchIntoNextExport)}";
            var exportedMetrics = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meterName)
                .AddInMemoryExporter(exportedMetrics)
                .Build();

            using var meter = new Meter(new MeterOptions(meterName));
            meter.CreateCounter<long>("counter").Add(1);

            Assert.True(meterProvider.ForceFlush());
            Assert.Single(exportedMetrics);

            var batch = new Batch<Metric>([exportedMetrics[0]], 1);

            Assert.Throws<IndexOutOfRangeException>(() => ProtobufOtlpMetricSerializer.WriteMetricsData(
                ref failingBuffer,
                failingBuffer.Length,
                Resource.Empty,
                batch));

            var buffer = new byte[8 * 1024];
            var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(
                ref buffer,
                0,
                Resource.Empty,
                batch);

            using var stream = new MemoryStream(buffer, 0, writePosition);
            var request = OtlpCollectorMetrics.ExportMetricsServiceRequest.Parser.ParseFrom(stream);

            var resourceMetrics = Assert.Single(request.ResourceMetrics);
            var scopeMetrics = Assert.Single(resourceMetrics.ScopeMetrics);

            // The stale batch would be serialized a second time alongside the new one.
            Assert.Single(scopeMetrics.Metrics);
        });

        this.maxSizeBuffer.AssertNotSwapped(failingBuffer);
    }

    [Fact]
    public void WriteLogsData_AfterFailedSerialization_ReleasesPooledLogRecordReference()
    {
        var failingBuffer = this.maxSizeBuffer.GetBuffer();

        RunOnDedicatedThread(() =>
        {
            // WriteLogsData calls AddReference on every pooled LogRecord it
            // groups; cleanup must Return that reference even when serialization
            // fails, or the record stays out of the shared pool.
            var logRecord = LogRecordSharedPool.Current.Rent();
            try
            {
                var referenceCountBefore = logRecord.PoolReferenceCount;

                var batch = new Batch<LogRecord>([logRecord], 1);

                Assert.Throws<IndexOutOfRangeException>(() => ProtobufOtlpLogSerializer.WriteLogsData(
                    ref failingBuffer,
                    failingBuffer.Length,
                    new SdkLimitOptions(),
                    new ExperimentalOptions(),
                    Resource.Empty,
                    batch));

                Assert.Equal(referenceCountBefore, logRecord.PoolReferenceCount);
            }
            finally
            {
                // Releases this test's own reference from Rent above.
                LogRecordSharedPool.Current.Return(logRecord);
            }
        });

        this.maxSizeBuffer.AssertNotSwapped(failingBuffer);
    }

    [Fact]
    public void WriteLogsData_WhenNonSizeExceptionEscapes_CleansUpBatchState()
        => RunOnDedicatedThread(static () =>
        {
            // A failure that is not a buffer-size problem takes the same path out
            // of TryWriteResourceLogs: LogRecord.ForEachScope invokes a
            // caller-supplied IExternalScopeProvider, and nothing between it and
            // the exporter catches an unexpected exception type.
            using var loggerProvider = Sdk.CreateLoggerProviderBuilder().Build();

            var logRecord = LogRecordSharedPool.Current.Rent();
            LogRecord? recordB = null;
            try
            {
                logRecord.Logger = loggerProvider.GetLogger("LoggerA");
                logRecord.ILoggerData.BufferedScopes = null;
                logRecord.ILoggerData.ScopeProvider = new ThrowingScopeProvider();

                var referenceCountBefore = logRecord.PoolReferenceCount;

                var failingBuffer = new byte[8 * 1024];
                var batchA = new Batch<LogRecord>([logRecord], 1);

                Assert.Throws<InvalidOperationException>(() => ProtobufOtlpLogSerializer.WriteLogsData(
                    ref failingBuffer,
                    0,
                    new SdkLimitOptions(),
                    new ExperimentalOptions(),
                    Resource.Empty,
                    batchA));

                Assert.Equal(referenceCountBefore, logRecord.PoolReferenceCount);

                recordB = LogRecordSharedPool.Current.Rent();
                recordB.Logger = loggerProvider.GetLogger("LoggerB");
                recordB.ILoggerData.BufferedScopes = null;
                recordB.ILoggerData.ScopeProvider = null;

                AssertOnlySecondBatchIsSerialized(recordB, "LoggerB");
            }
            finally
            {
                // LogRecordPoolHelper.Clear does not reset ILoggerData, so the
                // throwing provider has to be detached before the record can go
                // back into the process-wide shared pool.
                logRecord.ILoggerData.ScopeProvider = null;
                LogRecordSharedPool.Current.Return(logRecord);

                if (recordB != null)
                {
                    LogRecordSharedPool.Current.Return(recordB);
                }
            }
        });

    [Fact]
    public void WriteLogsData_AfterFailedSerialization_DoesNotRetainSerializationBuffer()
        => RunOnDedicatedThread(static () =>
        {
            var bufferReference = CreateFailedLogSerializationBufferWeakReference();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(bufferReference.TryGetTarget(out _), "The failed serialization buffer remained rooted.");
        });

    [Fact]
    public void ReturnActivityListToPool_WithNoGroupedBatch_DoesNotThrow()
        => RunOnDedicatedThread(static () =>
        {
            // Cleanup must be a no-op when WriteTraceData has never grouped a
            // batch on this thread (thread-static dictionary still null).
            ProtobufOtlpTraceSerializer.ReturnActivityListToPool();
        });

    [Fact]
    public void ReturnLogRecordListToPool_WithNoGroupedBatch_DoesNotThrow()
        => RunOnDedicatedThread(static () =>
        {
            ProtobufOtlpLogSerializer.ReturnLogRecordListToPool();
        });

    // Note: ProtobufOtlpMetricSerializer.ReturnMetricListToPool uses the same
    // null-safe guard but is private, and no production path can reach the null
    // state, so it is covered only by consistency with the other two serializers.

    private static void AssertOnlySecondBatchIsSerialized(LogRecord logRecord, string expectedScopeName)
    {
        var buffer = new byte[8 * 1024];
        var batch = new Batch<LogRecord>([logRecord], 1);
        var writePosition = ProtobufOtlpLogSerializer.WriteLogsData(
            ref buffer,
            0,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
            Resource.Empty,
            batch);

        using var stream = new MemoryStream(buffer, 0, writePosition);
        var logsData = OtlpLogs.LogsData.Parser.ParseFrom(stream);

        var resourceLogs = Assert.Single(logsData.ResourceLogs);
        var scopeLogs = Assert.Single(resourceLogs.ScopeLogs);
        Assert.Equal(expectedScopeName, scopeLogs.Scope?.Name);
        Assert.Single(scopeLogs.LogRecords);
    }

    private static ActivityListener CreateActivityListener(params ActivitySource[] sources)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => Array.IndexOf(sources, source) >= 0,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<byte[]> CreateFailedLogSerializationBufferWeakReference()
    {
        using var loggerProvider = Sdk.CreateLoggerProviderBuilder().Build();

        var logRecord = LogRecordSharedPool.Current.Rent();
        try
        {
            logRecord.Logger = loggerProvider.GetLogger("Logger");
            logRecord.ILoggerData.BufferedScopes = null;
            logRecord.ILoggerData.ScopeProvider = new ThrowingScopeProvider();

            var buffer = new byte[8 * 1024];
            var bufferReference = new WeakReference<byte[]>(buffer);
            var batch = new Batch<LogRecord>([logRecord], 1);

            Assert.Throws<InvalidOperationException>(() => ProtobufOtlpLogSerializer.WriteLogsData(
                ref buffer,
                0,
                new SdkLimitOptions(),
                new ExperimentalOptions(),
                Resource.Empty,
                batch));

            return bufferReference;
        }
        finally
        {
            logRecord.ILoggerData.ScopeProvider = null;
            LogRecordSharedPool.Current.Return(logRecord);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on a dedicated thread, mirroring the single
    /// export thread used by the batch processors, and keeping any thread-static
    /// serializer state the action leaves behind out of the test runner's threads.
    /// </summary>
    /// <param name="action">The test body.</param>
    private static void RunOnDedicatedThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = nameof(RunOnDedicatedThread),
        };

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "The test thread did not complete.");

        failure?.Throw();
    }

    private sealed class ThrowingScopeProvider : IExternalScopeProvider
    {
        public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
            => throw new InvalidOperationException("Scope enumeration failed.");

        public IDisposable Push(object? state)
            => throw new NotSupportedException();
    }
}
