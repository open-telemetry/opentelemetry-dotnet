// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.InteropServices;
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

/// <summary>
/// Tests against the real operating system. On Linux (x64, arm64) these create
/// real mappings and inspect them through /proc/self; elsewhere they check the
/// no-op path. They share the process wide default writer, so they run serially.
/// </summary>
[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class RealPlatformTests
{
    private static readonly string[] MappingNames = ["[anon_shmem:OTEL_CTX]", "[anon:OTEL_CTX]", "/memfd:OTEL_CTX"];

    private static bool IsSupportedPlatform =>
        OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;

    [Fact]
    public void NonLinuxIsASilentNoOp()
    {
        if (IsSupportedPlatform)
        {
            Assert.Skip("Process context is supported here; see the Linux tests.");
        }

        Assert.False(ProcessContextPublisher.IsSupported);
        Assert.False(ProcessContextPublisher.Publish(Resource.Empty));
        ProcessContextPublisher.Unpublish();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessContext(o => o.Enabled = true)
            .Build();

        Assert.Equal(0, ProcessContextWriter.Default.MappingAddress);
    }

    [Fact]
    public void LinuxPublishCreatesNamedMappingVisibleInProcMaps()
    {
        SkipUnlessLinux();
        var writer = new ProcessContextWriter(new LinuxProcessContextPlatform());

        Assert.True(writer.Publish(ProcessContextWriterTests.Payload("linux-test")));
        try
        {
            var address = writer.MappingAddress;
            var line = FindMapping(address);
            Assert.NotNull(line);
            var name = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(5).FirstOrDefault() ?? string.Empty;
            Assert.Contains(MappingNames, n => name.StartsWith(n, StringComparison.Ordinal));

            // Page aligned, as the eBPF prctl hook requires.
            Assert.Equal(0, address % Environment.SystemPageSize);

            // MADV_DONTFORK shows as the "dc" VmFlag.
            Assert.Contains("dc", ReadVmFlags(address));

            var header = HeaderReader.Read(address);
            Assert.Equal("OTEL_CTX", header.Signature);
            Assert.Equal(2u, header.Version);
            Assert.NotEqual(0UL, header.PublishedAt);
            Assert.Equal("linux-test", header.DecodePayload().Resource.Attributes[0].Value.StringValue);
        }
        finally
        {
            writer.Drop();
        }
    }

    [Fact]
    public void LinuxTimestampIsBootTimeAndChangesOnUpdate()
    {
        SkipUnlessLinux();
        var platform = new LinuxProcessContextPlatform();
        var writer = new ProcessContextWriter(platform);

        var before = platform.GetBootTimeNanoseconds();
        Assert.True(writer.Publish(ProcessContextWriterTests.Payload("v1")));
        try
        {
            var address = writer.MappingAddress;
            var first = HeaderReader.Read(address).PublishedAt;
            Assert.InRange(first, before, platform.GetBootTimeNanoseconds());

            ulong? whileMutating = null;
            writer.OnMutatingForTesting = () => whileMutating = HeaderReader.Read(address).PublishedAt;
            Assert.True(writer.Publish(ProcessContextWriterTests.Payload("v2")));

            Assert.Equal(0UL, whileMutating);
            var second = HeaderReader.Read(address);
            Assert.True(second.PublishedAt > first);
            Assert.Equal(address, writer.MappingAddress);
            Assert.Equal("v2", second.DecodePayload().Resource.Attributes[0].Value.StringValue);
            Assert.NotNull(FindMapping(address));
        }
        finally
        {
            writer.Drop();
        }
    }

    [Fact]
    public void LinuxDropRemovesMapping()
    {
        SkipUnlessLinux();
        var writer = new ProcessContextWriter(new LinuxProcessContextPlatform());
        Assert.True(writer.Publish(ProcessContextWriterTests.Payload("drop")));
        var address = writer.MappingAddress;

        writer.Drop();

        Assert.Null(FindContextMapping(address));
    }

    [Fact]
    public void LinuxProviderPublishesThroughDefaultWriter()
    {
        SkipUnlessLinux();

        var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => r.AddService("provider-test", serviceInstanceId: "abc"))
            .AddProcessContext(o => o.Enabled = true)
            .Build();

        var address = ProcessContextWriter.Default.MappingAddress;
        Assert.NotEqual(0, address);
        var attributes = HeaderReader.Read(address).DecodePayload().Resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.StringValue);
        Assert.Equal("provider-test", attributes["service.name"]);
        Assert.Equal("abc", attributes["service.instance.id"]);

        provider.Dispose();

        Assert.Equal(0, ProcessContextWriter.Default.MappingAddress);
        Assert.Null(FindContextMapping(address));
    }

    private static void SkipUnlessLinux()
    {
        if (!IsSupportedPlatform)
        {
            Assert.Skip("Process context publication needs Linux on x64 or arm64.");
        }
    }

    private static string? FindMapping(nint address)
    {
        var start = ((long)address).ToString("x", CultureInfo.InvariantCulture);
        return File.ReadLines("/proc/self/maps").FirstOrDefault(l => l.StartsWith(start + "-", StringComparison.Ordinal));
    }

    // After munmap the address range can be reused by an unrelated mapping, so
    // "gone" means no OTEL_CTX mapping at that address.
    private static string? FindContextMapping(nint address)
    {
        var line = FindMapping(address);
        return line != null && line.Contains("OTEL_CTX", StringComparison.Ordinal) ? line : null;
    }

    private static string[] ReadVmFlags(nint address)
    {
        var start = ((long)address).ToString("x", CultureInfo.InvariantCulture) + "-";
        var inMapping = false;
        foreach (var line in File.ReadLines("/proc/self/smaps"))
        {
            // A mapping starts with "start-end perms ...", where start and end are hex.
            var range = line.Split(' ', 2)[0];
            var dash = range.IndexOf('-', StringComparison.Ordinal);
            if (dash > 0 && range.All(c => c == '-' || char.IsAsciiHexDigit(c)))
            {
                inMapping = line.StartsWith(start, StringComparison.Ordinal);
            }
            else if (inMapping && line.StartsWith("VmFlags:", StringComparison.Ordinal))
            {
                return line["VmFlags:".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return [];
    }
}
