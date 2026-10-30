// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

/// <summary>
/// Fuzz tests for the file system storage used to retry failed exports from disk. The storage directory can
/// be shared with (or written to by) other processes, so every name and every file found in it is untrusted.
/// </summary>
public class PersistentStorageFuzzTests
{
    private const int MaxTests = 100;
    private const int MaxBlobSize = 256;
    private const string TimestampFormat = "yyyy-MM-ddTHHmmss.fffffffZ";

    private static readonly Regex Timestamp = new("[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{6}[.][0-9]{7}Z", RegexOptions.CultureInvariant);

    // A blob is {timestamp}-{guid}.blob, a lease is {blob}@{timestamp}.lock (possibly repeated) and a temporary file is {blob or lease}.tmp.
    private static readonly Regex StorageFileName = new(
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{6}[.][0-9]{7}Z-[0-9a-f]{32}[.]blob(?:@[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{6}[.][0-9]{7}Z[.]lock)*(?:[.]tmp)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Blob = new(
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{6}[.][0-9]{7}Z-[0-9a-f]{32}[.]blob$",
        RegexOptions.CultureInvariant);

    private enum OperationKind
    {
        Write,
        WriteWithLease,
        Lease,
        Read,
    }

    [Property(MaxTest = MaxTests)]
    public Property NameParsersNeverThrowForArbitraryFileNames() => Prop.ForAll(
        Generators.FileNameArbitrary(),
        static (name) =>
        {
            var path = Path.Combine(Path.GetTempPath(), name);

            _ = PersistentStorageHelper.IsBlobFileName(name);
            _ = PersistentStorageHelper.GetDateTimeFromBlobName(path);
            _ = PersistentStorageHelper.GetDateTimeFromLeaseName(path);
            _ = PersistentStorageHelper.RemoveLeases(path);
            _ = PersistentStorageHelper.GetReleasedPaths(path);

            return true;
        });

    [Property(MaxTest = MaxTests)]
    public Property GeneratedBlobNamesAreRecognisedAndRoundTripTheirTimestamp() => Prop.ForAll(
        Generators.BlobNameArbitrary(),
        static (blob) =>
        {
            var path = Path.Combine(Path.GetTempPath(), blob.Name);

            return PersistentStorageHelper.IsBlobFileName(blob.Name) &&
                   PersistentStorageHelper.GetDateTimeFromBlobName(path).Ticks == blob.Timestamp.Ticks;
        });

    [Property(MaxTest = MaxTests)]
    public Property LeasesAreRemovedFromGeneratedBlobNames() => Prop.ForAll(
        Generators.LeasedBlobArbitrary(),
        static (leased) =>
        {
            var directory = Path.GetTempPath();
            var path = Path.Combine(directory, leased.LeasedName);
            var expected = Path.Combine(directory, leased.BlobName);

            var released = PersistentStorageHelper.GetReleasedPaths(path);

            return PersistentStorageHelper.RemoveLeases(path) == expected &&
                   released.Count == leased.Leases.Count &&
                   released[released.Count - 1] == expected &&
                   PersistentStorageHelper.GetDateTimeFromLeaseName(path).Ticks == leased.Leases[leased.Leases.Count - 1].Ticks;
        });

    [Property(MaxTest = MaxTests)]
    public Property MaintenanceNeverModifiesFilesWhichAreNotStorageFiles() => Prop.ForAll(
        Generators.DirectoryContentsArbitrary(),
        static (files) =>
        {
            return WithDirectory(directory =>
            {
                Populate(directory, files);

                // Everything old enough to have expired, so that anything that is recognised may be removed.
                PersistentStorageHelper.RemoveExpiredBlobs(directory, retentionPeriodInMilliseconds: 1, writeTimeoutInMilliseconds: 1);

                foreach (var file in files.Where(static f => !IsStorageFile(f.Name)))
                {
                    var path = Path.Combine(directory, file.Name);

                    if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Content))
                    {
                        return false;
                    }
                }

                return true;
            });
        });

    [Property(MaxTest = MaxTests)]
    public Property ProviderOnlyReturnsRecentBlobsAndOnlyReadsValidContent() => Prop.ForAll(
        Generators.DirectoryContentsArbitrary(),
        static (files) =>
        {
            return WithDirectory(directory =>
            {
                using var provider = new FileBlobProvider(directory, maxSizeInBytes: MaxBlobSize, maintenancePeriodInMilliseconds: 3_600_000);

                Populate(directory, files);

                var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(172_800_000);

                var expected = files
                    .Where(f => IsBlob(f.Name) && ParseTimestamp(Timestamp.Match(f.Name).Value) > retentionDeadline)
                    .ToDictionary(static f => f.Name, static f => f.Content, StringComparer.Ordinal);

                var actual = provider.GetBlobs().Cast<FileBlob>().ToList();

                if (actual.Count != expected.Count ||
                    !actual.All(b => expected.ContainsKey(Path.GetFileName(b.FullPath))))
                {
                    return false;
                }

                foreach (var blob in actual)
                {
                    var content = expected[Path.GetFileName(blob.FullPath)];
                    var readable = content.Length > 0 && content.Length <= MaxBlobSize;

                    // Content which is empty or too large for the storage must be rejected, and anything else returned as written.
                    if (blob.TryRead(out var buffer) != readable || (readable && !buffer.AsSpan().SequenceEqual(content)))
                    {
                        return false;
                    }
                }

                return true;
            });
        });

    [Property(MaxTest = MaxTests)]
    public Property BlobLifecycleMatchesModel() => Prop.ForAll(
        Generators.OperationsArbitrary(),
        static (operations) =>
        {
            return WithDirectory(directory =>
            {
                using var provider = new FileBlobProvider(directory, maxSizeInBytes: 1024 * 1024, maintenancePeriodInMilliseconds: 3_600_000);

                var content = operations.Initial;

                if (!provider.TryCreateBlob(content.AsSpan(), out var blob))
                {
                    return false;
                }

                var leased = false;

                foreach (var operation in operations.Steps)
                {
                    switch (operation.Kind)
                    {
                        case OperationKind.Write:
                            if (!blob.TryWrite(operation.Content.AsSpan()))
                            {
                                return false;
                            }

                            content = operation.Content;
                            leased = false;
                            break;

                        case OperationKind.WriteWithLease:
                            if (!blob.TryWrite(operation.Content.AsSpan(), leasePeriodMilliseconds: 60_000))
                            {
                                return false;
                            }

                            content = operation.Content;
                            leased = true;
                            break;

                        case OperationKind.Lease:
                            if (!blob.TryLease(60_000))
                            {
                                return false;
                            }

                            leased = true;
                            break;

                        default:
                            break;
                    }

                    // Only the blob itself is ever left in the directory (no temporary files), holding the latest content.
                    var files = Directory.GetFiles(directory);

                    if (files.Length != 1 ||
                        !blob.TryRead(out var buffer) ||
                        !buffer.AsSpan().SequenceEqual(content) ||
                        provider.GetBlobs().Any() == leased)
                    {
                        return false;
                    }
                }

                return blob.TryDelete() && Directory.GetFiles(directory).Length == 0;
            });
        });

    private static bool WithDirectory(Func<string, bool> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "otel-fuzz-storage-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);

        try
        {
            return test(directory);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void Populate(string directory, List<StorageFile> files)
    {
        foreach (var file in files)
        {
            File.WriteAllBytes(Path.Combine(directory, file.Name), file.Content);
        }
    }

    private static DateTime ParseTimestamp(string value)
        => DateTime.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static bool TimestampsAreValid(string name)
        => Timestamp.Matches(name).Cast<Match>().All(static m => DateTime.TryParseExact(m.Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _));

    private static bool IsStorageFile(string name) => StorageFileName.IsMatch(name) && TimestampsAreValid(name);

    private static bool IsBlob(string name) => Blob.IsMatch(name) && TimestampsAreValid(name);

    private static class Generators
    {
        private static readonly Gen<char> HexChar = Gen.Elements("0123456789abcdef".ToCharArray());

        private static readonly Gen<string> Guid32 =
            from chars in Gen.ArrayOf(HexChar, 32)
            select new string(chars);

        public static Arbitrary<string> FileNameArbitrary()
        {
            var fragments = new[]
            {
                ".blob",
                ".lock",
                ".tmp",
                "@",
                "-",
                ".",
                "T",
                "Z",
                "2026-10-30T120000.1234567Z",
                "2026-13-45T250000.1234567Z",
                "0123456789abcdef0123456789abcdef",
                "0123456789ABCDEF0123456789abcdef",
                "a",
                "1",
                "x",
                "_",
                "[",
                "]",
            };

            var gen =
                from count in Gen.Choose(0, 12)
                from tokens in Gen.ArrayOf(Gen.Elements(fragments), count)
                select string.Concat(tokens);

            var noise = Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min(size + 1, 64))
                from chars in Gen.ArrayOf(Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.@[]".ToCharArray()), length)
                select new string(chars));

            return Gen.OneOf(gen, noise).Select(static s => s.Length == 0 ? "x" : s).ToArbitrary();
        }

        public static Arbitrary<(string Name, DateTime Timestamp)> BlobNameArbitrary()
            => (from timestamp in TimestampGen()
                from guid in Guid32
                select (BlobName(timestamp, guid), timestamp)).ToArbitrary();

        public static Arbitrary<(string BlobName, string LeasedName, List<DateTime> Leases)> LeasedBlobArbitrary()
        {
            var gen =
                from timestamp in TimestampGen()
                from guid in Guid32
                from count in Gen.Choose(1, 3)
                from leases in Gen.ArrayOf(TimestampGen(), count)
                let blobName = BlobName(timestamp, guid)
                select (blobName, blobName + string.Concat(leases.Select(static l => "@" + Format(l) + ".lock")), leases.ToList());

            return gen.ToArbitrary();
        }

        public static Arbitrary<List<StorageFile>> DirectoryContentsArbitrary()
        {
            var gen =
                from count in Gen.Choose(1, 12)
                from names in Gen.ArrayOf(NameGen(), count)
                from files in Gen.ArrayOf(ContentGen(), count)
                select names
                    .Zip(files, static (n, c) => new StorageFile(n, c))
                    .GroupBy(static f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(static g => g.First())
                    .ToList();

            return gen.ToArbitrary();
        }

        public static Arbitrary<Operations> OperationsArbitrary()
        {
            var step =
                from kind in Gen.Elements(OperationKind.Write, OperationKind.WriteWithLease, OperationKind.Lease, OperationKind.Read)
                from content in NonEmptyContentGen()
                select new Operation(kind, content);

            var gen =
                from initial in NonEmptyContentGen()
                from count in Gen.Choose(0, 6)
                from steps in Gen.ArrayOf(step, count)
                select new Operations(initial, steps);

            return gen.ToArbitrary();
        }

        private static Gen<DateTime> TimestampGen() =>
            from ticks in Gen.Choose(0, int.MaxValue)
            from offset in Gen.Choose(0, 9999)
            select new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ticks).AddTicks(offset);

        private static string Format(DateTime timestamp) => timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        private static string BlobName(DateTime timestamp, string guid) => Format(timestamp) + "-" + guid + ".blob";

        private static Gen<byte[]> ContentGen() =>
            from length in Gen.Elements(0, 1, 2, 10, MaxBlobSize - 1, MaxBlobSize, MaxBlobSize + 1, MaxBlobSize * 2)
            from bytes in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), length)
            select bytes;

        private static Gen<byte[]> NonEmptyContentGen() =>
            from length in Gen.Choose(1, 200)
            from bytes in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), length)
            select bytes;

        // Names of files that look like those of the storage, either exactly or with a small mutation.
        private static Gen<string> NameGen() =>
            from kind in Gen.Choose(0, 5)
            from recent in Gen.Elements(true, false)
            from minutes in Gen.Choose(1, 3 * 24 * 60)
            from guid in Guid32
            from leaseCount in Gen.Choose(1, 2)
            from mutate in Gen.Elements(true, true, false)
            from mutation in Gen.Choose(0, 6)
            from position in Gen.Choose(0, 200)
            from replacement in Gen.Elements("abcXYZ-_@.T".ToCharArray())
            select Mutate(CreateName(kind, recent, minutes, guid, leaseCount), mutate, mutation, position, replacement);

        private static string CreateName(int kind, bool recent, int minutes, string guid, int leaseCount)
        {
            var created = DateTime.UtcNow - TimeSpan.FromMinutes(recent ? minutes % 600 : minutes + (3 * 24 * 60));
            var blob = BlobName(created, guid);
            var lease = "@" + Format(DateTime.UtcNow - TimeSpan.FromMinutes(minutes)) + ".lock";

            return kind switch
            {
                0 => blob,
                1 => blob + string.Concat(Enumerable.Repeat(lease, leaseCount)),
                2 => blob + ".tmp",
                3 => blob + lease + ".tmp",
                4 => "notes-" + guid + ".txt",
                _ => guid + ".blob",
            };
        }

        private static string ReplaceOrdinal(string value, string oldValue, string newValue)
            => string.Join(newValue, value.Split([oldValue], StringSplitOptions.None));

        private static string Mutate(string name, bool mutate, int mutation, int position, char replacement)
        {
            if (!mutate)
            {
                return name;
            }

            var index = position % name.Length;

            return mutation switch
            {
                0 => name.Substring(0, index) + replacement + name.Substring(index + 1),
                1 => name.Remove(index, 1),
                2 => name.Insert(index, replacement.ToString()),
                3 => name.ToUpperInvariant(),
                4 => ReplaceOrdinal(ReplaceOrdinal(name, ".blob", ".BLOB"), ".lock", ".Lock"),
                5 => name + ".bak",
                _ => ReplaceOrdinal(name, "-", string.Empty),
            };
        }
    }

    private sealed class StorageFile(string name, byte[] content)
    {
        public string Name { get; } = name;

        public byte[] Content { get; } = content;

        public override string ToString() => $"{this.Name} ({this.Content.Length} bytes)";
    }

    private sealed class Operation(OperationKind kind, byte[] content)
    {
        public OperationKind Kind { get; } = kind;

        public byte[] Content { get; } = content;
    }

    private sealed class Operations(byte[] initial, Operation[] steps)
    {
        public byte[] Initial { get; } = initial;

        public Operation[] Steps { get; } = steps;

        public override string ToString() => string.Join(", ", this.Steps.Select(static s => s.Kind));
    }
}
