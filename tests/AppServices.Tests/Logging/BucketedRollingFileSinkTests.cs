using Bennewitz.Ninja.AppServices.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace AppServices.Tests.Logging;

/// <summary>
/// Tests for <see cref="BucketedRollingFileSink"/>.
/// <para>
/// Covers bucket-start floor maths, filename formatting, parsing, retention
/// pruning (including the boundary case), construction-time validation of
/// bucket size / retention / prefix, and basic Emit behaviour against a
/// pinned clock.
/// </para>
/// </summary>
public sealed class BucketedRollingFileSinkTests : IDisposable
{
    private readonly string _dir;

    // Ported from MSTest's [TestInitialize]: xunit v3 builds a fresh instance per test, so the
    // constructor runs before each test exactly as TestInitialize did.
    public BucketedRollingFileSinkTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "logs");
        Directory.CreateDirectory(_dir);
    }

    // Ported from MSTest's [TestCleanup].
    public void Dispose()
    {
        try
        {
            string parent = Path.GetDirectoryName(_dir)!;
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
        catch
        {
            /* best-effort */
        }
    }

    // -----------------------------------------------------------------------
    // BucketStart (default 8-hour buckets: 00, 08, 16)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)] // midnight → bucket 00
    [InlineData(1, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 8)] // 08:xx → bucket 08
    [InlineData(9, 8)]
    [InlineData(15, 8)]
    [InlineData(16, 16)] // 16:xx → bucket 16
    [InlineData(17, 16)]
    [InlineData(23, 16)]
    public void BucketStart_DefaultEightHour_FloorsToBucketHour(int inputHour, int expectedBucketHour)
    {
        DateTime utcNow = new(2026, 4, 22, inputHour, 30, 0, DateTimeKind.Utc);
        DateTime start = BucketedRollingFileSink.BucketStart(utcNow, 8);

        Assert.Equal(expectedBucketHour, start.Hour);
        Assert.Equal(0, start.Minute);
        Assert.Equal(0, start.Second);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(utcNow.Date, start.Date);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 5, 5)]
    [InlineData(1, 23, 23)]
    [InlineData(6, 5, 0)]
    [InlineData(6, 6, 6)]
    [InlineData(6, 11, 6)]
    [InlineData(6, 12, 12)]
    [InlineData(6, 18, 18)]
    [InlineData(12, 11, 0)]
    [InlineData(12, 12, 12)]
    [InlineData(24, 23, 0)]
    public void BucketStart_VariousBucketSizes_FloorCorrectly(int bucketHours, int inputHour, int expectedBucketHour)
    {
        DateTime utcNow = new(2026, 4, 22, inputHour, 17, 42, DateTimeKind.Utc);
        DateTime start = BucketedRollingFileSink.BucketStart(utcNow, bucketHours);

        Assert.Equal(expectedBucketHour, start.Hour);
    }

    // -----------------------------------------------------------------------
    // FileName
    // -----------------------------------------------------------------------

    [Fact]
    public void FileName_FormatsCorrectly_ForBucket00()
    {
        using BucketedRollingFileSink sink = new(_dir,
            clock: () => new DateTime(2026, 4, 22, 3, 0, 0, DateTimeKind.Utc));
        DateTime utcNow = new(2026, 4, 22, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal("app-20260422-00.txt", sink.FileName(utcNow));
    }

    [Fact]
    public void FileName_FormatsCorrectly_ForBucket08()
    {
        using BucketedRollingFileSink sink = new(_dir,
            clock: () => new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc));
        DateTime utcNow = new(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal("app-20260422-08.txt", sink.FileName(utcNow));
    }

    [Fact]
    public void FileName_FormatsCorrectly_ForBucket16()
    {
        using BucketedRollingFileSink sink = new(_dir,
            clock: () => new DateTime(2026, 4, 22, 23, 59, 0, DateTimeKind.Utc));
        DateTime utcNow = new(2026, 4, 22, 23, 59, 0, DateTimeKind.Utc);
        Assert.Equal("app-20260422-16.txt", sink.FileName(utcNow));
    }

    [Fact]
    public void FileName_UsesCustomPrefix_WhenProvided()
    {
        using BucketedRollingFileSink sink = new(_dir,
            fileNamePrefix: "myapp",
            clock: () => new DateTime(2026, 4, 22, 9, 0, 0, DateTimeKind.Utc));
        DateTime utcNow = new(2026, 4, 22, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal("myapp-20260422-08.txt", sink.FileName(utcNow));
    }

    [Fact]
    public void FileName_UsesCustomBucketSize_WhenProvided()
    {
        using BucketedRollingFileSink sink = new(_dir,
            bucketSize: TimeSpan.FromHours(6),
            clock: () => new DateTime(2026, 4, 22, 13, 0, 0, DateTimeKind.Utc));
        // 13:xx with 6-hour buckets → bucket 12
        DateTime utcNow = new(2026, 4, 22, 13, 0, 0, DateTimeKind.Utc);
        Assert.Equal("app-20260422-12.txt", sink.FileName(utcNow));
    }

    // -----------------------------------------------------------------------
    // TryParseBucketStamp
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParseBucketStamp_ValidFile_ReturnsTrue()
    {
        bool ok = BucketedRollingFileSink.TryParseBucketStamp("app", "app-20260101-08.txt", out DateTime stamp);
        Assert.True(ok);
        Assert.Equal(new DateTime(2026, 1, 1), stamp);
    }

    [Fact]
    public void TryParseBucketStamp_RespectsCustomPrefix()
    {
        bool ok = BucketedRollingFileSink.TryParseBucketStamp("myapp", "myapp-20260101-16.txt", out DateTime stamp);
        Assert.True(ok);
        Assert.Equal(new DateTime(2026, 1, 1), stamp);
    }

    [Fact]
    public void TryParseBucketStamp_PrefixMismatch_ReturnsFalse()
    {
        // File has the right shape but a different prefix.
        Assert.False(BucketedRollingFileSink.TryParseBucketStamp("app", "other-20260101-00.txt", out DateTime _));
    }

    [Theory]
    [InlineData("notalogfile.txt")]
    [InlineData("app-baddate-00.txt")]
    [InlineData("")]
    public void TryParseBucketStamp_InvalidFile_ReturnsFalse(string fileName)
    {
        Assert.False(BucketedRollingFileSink.TryParseBucketStamp("app", fileName, out DateTime _));
    }

    // -----------------------------------------------------------------------
    // Retention pruning
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Pruning_DeletesFilesOlderThanRetentionWindow()
    {
        // Create three old files (4 days ago) and two recent ones (1 day ago).
        DateTime now = new(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);

        string[] oldFiles =
        [
            "app-20260418-00.txt",
            "app-20260418-08.txt",
            "app-20260418-16.txt",
        ];
        string[] recentFiles =
        [
            "app-20260421-08.txt", // 1 day ago — inside retention window
            "app-20260422-08.txt", // today
        ];

        foreach (string f in oldFiles.Concat(recentFiles))
        {
            File.WriteAllText(Path.Combine(_dir, f), "data");
        }

        // Clock pinned to 'now'; constructing the sink triggers the startup sweep,
        // which is now deferred to a background task. Await it
        // so the assertions below see the post-prune filesystem state.
        using BucketedRollingFileSink sink = new(_dir, clock: () => now);
        await sink.StartupPruneTask;

        foreach (string f in oldFiles)
        {
            Assert.False(File.Exists(Path.Combine(_dir, f)), $"{f} should be pruned");
        }

        foreach (string f in recentFiles)
        {
            Assert.True(File.Exists(Path.Combine(_dir, f)), $"{f} should be retained");
        }
    }

    [Fact]
    public async Task Pruning_RetainsFilesExactlyAtRetentionBoundary()
    {
        // A file dated exactly Retention ago (cutoff = now.Date - Retention)
        // sits on the boundary: pruning uses strict <, so the boundary file lives.
        DateTime now = new(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);
        TimeSpan retention = BucketedRollingFileSink.DefaultRetention;
        DateTime boundaryDate = now.Date - retention;
        string boundaryFile = $"app-{boundaryDate:yyyyMMdd}-00.txt";
        File.WriteAllText(Path.Combine(_dir, boundaryFile), "data");

        using BucketedRollingFileSink sink = new(_dir, clock: () => now);
        await sink.StartupPruneTask;

        Assert.True(File.Exists(Path.Combine(_dir, boundaryFile)), "File exactly at boundary should be retained (strict < cutoff).");
    }

    [Fact]
    public async Task Pruning_RespectsCustomRetention()
    {
        DateTime now = new(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);

        // 2-day retention; 3-day-old file should be pruned, 1-day-old kept.
        string oldFile = "app-20260419-00.txt"; // 3 days old
        string recentFile = "app-20260421-08.txt"; // 1 day old
        File.WriteAllText(Path.Combine(_dir, oldFile), "data");
        File.WriteAllText(Path.Combine(_dir, recentFile), "data");

        using BucketedRollingFileSink sink = new(
            _dir,
            retention: TimeSpan.FromDays(2),
            clock: () => now);
        await sink.StartupPruneTask;

        Assert.False(File.Exists(Path.Combine(_dir, oldFile)), $"{oldFile} should be pruned under 2-day retention.");
        Assert.True(File.Exists(Path.Combine(_dir, recentFile)), $"{recentFile} should be retained.");
    }

    [Fact]
    public async Task Pruning_OnlyTouchesFilesWithMatchingPrefix()
    {
        DateTime now = new(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);

        string oldOurs = "app-20260418-00.txt"; // 4 days, ours: should be pruned
        string oldOthers = "other-20260418-00.txt"; // 4 days, different prefix: untouched
        File.WriteAllText(Path.Combine(_dir, oldOurs), "data");
        File.WriteAllText(Path.Combine(_dir, oldOthers), "data");

        using BucketedRollingFileSink sink = new(_dir, clock: () => now);
        await sink.StartupPruneTask;

        Assert.False(File.Exists(Path.Combine(_dir, oldOurs)), "Matching-prefix file should be pruned.");
        Assert.True(File.Exists(Path.Combine(_dir, oldOthers)), "Foreign-prefix file should be left alone.");
    }

    // -----------------------------------------------------------------------
    // Constructor validation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(5)] // not a divisor of 24
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(48)] // larger than 24
    public void Ctor_InvalidBucketSizeHours_Throws(int hours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BucketedRollingFileSink(_dir, bucketSize: TimeSpan.FromHours(hours)));
    }

    [Fact]
    public void Ctor_FractionalBucketSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BucketedRollingFileSink(_dir, bucketSize: TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void Ctor_ZeroBucketSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BucketedRollingFileSink(_dir, bucketSize: TimeSpan.Zero));
    }

    [Fact]
    public void Ctor_NonPositiveRetention_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BucketedRollingFileSink(_dir, retention: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BucketedRollingFileSink(_dir, retention: TimeSpan.FromDays(-1)));
    }

    [Theory]
    [InlineData("has-dash")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/slash")]
    public void Ctor_InvalidPrefix_Throws(string prefix)
    {
        Assert.Throws<ArgumentException>(() =>
            new BucketedRollingFileSink(_dir, fileNamePrefix: prefix));
    }

    [Fact]
    public void Ctor_NullLogsDirectory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BucketedRollingFileSink(logsDirectory: null!));
    }

    // -----------------------------------------------------------------------
    // Public surface defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void Defaults_AreEightHoursAndThreeDaysAndAppPrefix()
    {
        Assert.Equal(TimeSpan.FromHours(8), BucketedRollingFileSink.DefaultBucketSize);
        Assert.Equal(TimeSpan.FromDays(3), BucketedRollingFileSink.DefaultRetention);
        // MSTEST0032: const-vs-literal, so the compiler folds it to always-true.
        // Pinning the published default is the point — changing the const must fail here.
#pragma warning disable MSTEST0032
        Assert.Equal("app", BucketedRollingFileSink.DefaultFileNamePrefix);
#pragma warning restore MSTEST0032
    }

    [Fact]
    public void InstanceProperties_ReflectConstructorArgs()
    {
        using BucketedRollingFileSink sink = new(
            _dir,
            bucketSize: TimeSpan.FromHours(6),
            retention: TimeSpan.FromDays(7),
            fileNamePrefix: "myapp",
            clock: () => new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromHours(6), sink.BucketSize);
        Assert.Equal(TimeSpan.FromDays(7), sink.Retention);
        Assert.Equal("myapp", sink.FileNamePrefix);
    }

    // -----------------------------------------------------------------------
    // Emit creates and writes to a bucket file
    // -----------------------------------------------------------------------

    [Fact]
    public void Emit_CreatesExpectedBucketFile()
    {
        DateTime now = new(2026, 4, 22, 9, 0, 0, DateTimeKind.Utc); // bucket 08

        using (BucketedRollingFileSink sink = new(_dir, clock: () => now))
        {
            LogEvent logEvent = new(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                exception: null,
                new MessageTemplate("Hello {Name}", [
                    new TextToken("Hello "),
                    new PropertyToken("Name", "{Name}"),
                ]),
                [new LogEventProperty("Name", new ScalarValue("World"))]);

            sink.Emit(logEvent);
        } // dispose flushes

        string expected = Path.Combine(_dir, "app-20260422-08.txt");
        Assert.True(File.Exists(expected), $"Expected bucket file '{expected}' to exist.");
    }

    [Fact]
    public void Emit_UpdatesCurrentFilePath()
    {
        DateTime now = new(2026, 4, 22, 9, 0, 0, DateTimeKind.Utc);

        using BucketedRollingFileSink sink = new(_dir, clock: () => now);
        Assert.True(sink.CurrentFilePath is null, "CurrentFilePath should be null before first Emit.");

        LogEvent logEvent = new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate("hi", [new TextToken("hi")]),
            []);
        sink.Emit(logEvent);

        Assert.NotNull(sink.CurrentFilePath);
        Assert.Equal(Path.Combine(_dir, "app-20260422-08.txt"), sink.CurrentFilePath);
    }

    [Fact]
    public void Emit_DoesNotThrow_WhenCalledMultipleTimes()
    {
        DateTime now = new(2026, 4, 22, 1, 0, 0, DateTimeKind.Utc);

        using BucketedRollingFileSink sink = new(_dir, clock: () => now);

        for (int i = 0; i < 20; i++)
        {
            LogEvent logEvent = new(
                DateTimeOffset.UtcNow,
                LogEventLevel.Debug,
                exception: null,
                new MessageTemplate($"msg {i}", [new TextToken($"msg {i}")]),
                []);
            sink.Emit(logEvent);
        }
        // No assertion needed — absence of exception is the pass condition.
    }

    [Fact]
    public void Emit_RotatesFile_WhenBucketChanges()
    {
        // Mutable clock so we can step it across a bucket boundary (08 → 16).
        DateTime clock = new(2026, 4, 22, 9, 0, 0, DateTimeKind.Utc);

        using BucketedRollingFileSink sink = new(_dir, clock: () => clock);

        // First emit lands in the 08 bucket.
        sink.Emit(MakeEvent("first"));
        Assert.Equal(Path.Combine(_dir, "app-20260422-08.txt"), sink.CurrentFilePath);

        // Advance clock past the bucket boundary; next emit must rotate.
        clock = new DateTime(2026, 4, 22, 16, 5, 0, DateTimeKind.Utc);
        sink.Emit(MakeEvent("second"));

        Assert.Equal(Path.Combine(_dir, "app-20260422-16.txt"), sink.CurrentFilePath);
    }

    private static LogEvent MakeEvent(string message)
    {
        return new LogEvent(DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            new MessageTemplate(message, [new TextToken(message)]),
            []);
    }
}