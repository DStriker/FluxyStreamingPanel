using Fluxy.Application.Backups.Services;
using Fluxy.Core.Backups.Interfaces;
using Fluxy.Core.Backups.Options;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Outcomes;
using Fluxy.Core.Settings.Enums;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.UnitTests.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxy.UnitTests.Backups
{
    /// <summary>
    /// The scheduler as a whole: the Testing no-op, the disabled switch, one archive per period
    /// and the rotation that follows a create.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The scheduler is started through <c>StartAsync</c>, which is the only public door a
    /// <c>BackgroundService</c> has</b>, and its ticks are then driven by advancing a clock the
    /// test owns. Each advance fires the timer <c>Task.Delay(interval, TimeProvider, ct)</c>
    /// registered; the test then waits until the loop is parked on its next delay again, which is
    /// exactly the point at which the previous tick finished. Nothing sleeps for a real minute.
    /// </para>
    /// <para>
    /// <b>The clock is hand-rolled rather than a package</b> - a dozen lines of timer registry, and
    /// the semantics of the test stay visible: a delay is a callback the test fires, not a thread
    /// that wakes up.
    /// </para>
    /// </remarks>
    public sealed class BackupSchedulerTests
    {
        private static readonly DateTimeOffset Start = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task TheTestingEnvironmentNeverWritesAFolder()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Testing", Start);

            await harness.RunAsync(ticks: 200);

            Assert.Empty(directory.Files());
            Assert.Equal(0, harness.Tool.Runs);
        }

        [Fact]
        public async Task ABackupIsTakenOnceADayAndNotAgainInsideIt()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Development", Start);

            harness.Settings.ApplicationSnapshot = Enabled(BackupPeriod.Daily, rotation: 10);

            // The first tick of the first day: one archive.
            await harness.RunAsync(ticks: 1);
            Assert.Single(directory.Files());

            // The rest of that day: nothing more, however many ticks pass.
            await harness.RunAsync(ticks: 120);
            Assert.Single(directory.Files());

            // The next day: exactly one more.
            await harness.RunAsync(ticks: (24 * 60) - 120);
            Assert.Equal(2, directory.Files().Count);
        }

        [Fact]
        public async Task ADisabledSwitchSkipsEveryTick()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Development", Start);

            harness.Settings.ApplicationSnapshot = Disabled();

            await harness.RunAsync(ticks: 200);

            Assert.Empty(directory.Files());
            Assert.Equal(0, harness.Tool.Runs);
        }

        [Fact]
        public async Task RotationKeepsOnlyTheNewestArchivesAfterTheCreate()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Development", Start);

            harness.Settings.ApplicationSnapshot = Enabled(BackupPeriod.Daily, rotation: 2);

            await harness.RunAsync(ticks: 1);
            await harness.RunAsync(ticks: 24 * 60);
            await harness.RunAsync(ticks: 24 * 60);

            var files = directory.Files();

            Assert.Equal(2, files.Count);

            // The two kept are the newest, which for this format is the lexicographically greatest.
            var expected = files.OrderBy(name => name, StringComparer.Ordinal).ToList();
            Assert.Equal(expected, files);
            Assert.Contains(files, name => name.Contains("20261124", StringComparison.Ordinal));
            Assert.Contains(files, name => name.Contains("20261125", StringComparison.Ordinal));
            Assert.DoesNotContain(files, name => name.Contains("20261123", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AZeroRotationCountKeepsEverything()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Development", Start);

            harness.Settings.ApplicationSnapshot = Enabled(BackupPeriod.Daily, rotation: 0);

            await harness.RunAsync(ticks: 1);
            await harness.RunAsync(ticks: 24 * 60);
            await harness.RunAsync(ticks: 24 * 60);

            Assert.Equal(3, directory.Files().Count);
        }

        [Fact]
        public async Task AWeeklyPeriodTakesOneArchiveForTheWholeIsoWeek()
        {
            using var directory = new TemporaryDirectory();
            await using var harness = new Harness(directory.Path, "Development", Start);

            harness.Settings.ApplicationSnapshot = Enabled(BackupPeriod.Weekly, rotation: 10);

            // 2026-11-23 is a Monday; two more days are the same ISO week.
            await harness.RunAsync(ticks: 1);
            await harness.RunAsync(ticks: 48 * 60);

            Assert.Single(directory.Files());
        }

        private static ApplicationSettingsSnapshot Enabled(BackupPeriod period, int rotation)
            => FakeSettingsSnapshotProvider.DefaultApplication() with
            {
                BackupEnabled = true,
                BackupPeriod = period,
                BackupRotationCount = rotation
            };

        private static ApplicationSettingsSnapshot Disabled()
            => FakeSettingsSnapshotProvider.DefaultApplication() with { BackupEnabled = false };

        /// <summary>
        /// The scheduler, its service, the fake clock and the tool, wired to a temp directory.
        /// </summary>
        private sealed class Harness : IAsyncDisposable
        {
            private readonly BackupScheduler _scheduler;
            private readonly ServiceProvider _provider;
            private readonly CancellationTokenSource _stopping = new();
            private bool _started;

            public Harness(string directoryPath, string environmentName, DateTimeOffset start)
            {
                Clock = new ManualTimeProvider(start);
                Settings = new FakeSettingsSnapshotProvider();
                Tool = new FakeTool();

                var services = new ServiceCollection();
                services.AddSingleton<ISettingsSnapshotProvider>(Settings);
                _provider = services.BuildServiceProvider();

                var options = new BackupsOptions
                {
                    ContainerName = "postgres",
                    Directory = directoryPath,
                    TickSeconds = 60
                };

                var service = new BackupService(
                    options,
                    Tool,
                    _provider.GetRequiredService<IServiceScopeFactory>(),
                    Clock,
                    NullLogger<BackupService>.Instance);

                _scheduler = new BackupScheduler(
                    Clock,
                    Settings,
                    service,
                    options,
                    new StubEnvironment(environmentName),
                    NullLogger<BackupScheduler>.Instance);
            }

            public ManualTimeProvider Clock { get; }

            public FakeSettingsSnapshotProvider Settings { get; }

            public FakeTool Tool { get; }

            /// <summary>
            /// Advances the clock one interval at a time. After each advance it waits until the
            /// loop is parked on its next delay, which means the tick it just ran is finished.
            /// </summary>
            public async Task RunAsync(int ticks)
            {
                if (!_started)
                {
                    await _scheduler.StartAsync(_stopping.Token);
                    _started = true;
                }

                for (var tick = 0; tick < ticks; tick++)
                {
                    // Nothing registered a delay: the loop returned (the Testing gate) and there
                    // is nothing left to drive.
                    if (!await Clock.WaitForPendingTimerAsync())
                    {
                        return;
                    }

                    Clock.Advance(TimeSpan.FromSeconds(60));

                    // Advancing fires the pending delay; the loop then runs its tick and parks on
                    // the next delay. Waiting for that next timer is what makes the tick's file
                    // write and rotation observable to the assertions that follow.
                    if (!await Clock.WaitForPendingTimerAsync())
                    {
                        return;
                    }
                }
            }

            public async ValueTask DisposeAsync()
            {
                _stopping.Cancel();

                try
                {
                    await _scheduler.StopAsync(CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _scheduler.Dispose();
                    _stopping.Dispose();
                    await _provider.DisposeAsync();
                }
            }
        }

        /// <summary>A tool that writes a placeholder archive, so the file appears for real.</summary>
        private sealed class FakeTool : IDatabaseBackupTool
        {
            private int _runs;

            public int Runs => Volatile.Read(ref _runs);

            public async Task<BackupToolResult> CreateDumpAsync(
                string destinationFilePath,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _runs);

                await File.WriteAllBytesAsync(
                    destinationFilePath,
                    "fake archive"u8.ToArray(),
                    cancellationToken);

                return BackupToolResult.Success;
            }

            public Task<BackupToolResult> RestoreDumpAsync(
                string dumpFilePath,
                CancellationToken cancellationToken)
                => Task.FromResult(BackupToolResult.Success);
        }

        /// <summary>A clock whose timers the test owns and fires by advancing time.</summary>
        private sealed class ManualTimeProvider : TimeProvider
        {
            private readonly object _gate = new();
            private readonly List<ManualTimer> _timers = [];
            private DateTimeOffset _now;

            public ManualTimeProvider(DateTimeOffset start) => _now = start;

            public override DateTimeOffset GetUtcNow() => _now;

            public override ITimer CreateTimer(
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                var timer = new ManualTimer(this, callback, state);

                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    timer.DueAt = _now + dueTime;

                    lock (_gate)
                    {
                        _timers.Add(timer);
                    }
                }

                return timer;
            }

            /// <summary>
            /// Completes once a timer is registered - or answers false when the loop has already
            /// finished, which is how the Testing gate's early return is observed.
            /// </summary>
            public async Task<bool> WaitForPendingTimerAsync()
            {
                for (var attempt = 0; attempt < 400; attempt++)
                {
                    lock (_gate)
                    {
                        if (_timers.Count > 0)
                        {
                            return true;
                        }
                    }

                    await Task.Delay(5);
                }

                return false;
            }

            /// <summary>Moves the clock forward and fires every timer that became due.</summary>
            public void Advance(TimeSpan delta)
            {
                List<ManualTimer> due;

                lock (_gate)
                {
                    _now += delta;

                    due = _timers.Where(timer => timer.DueAt <= _now).ToList();

                    foreach (var timer in due)
                    {
                        _timers.Remove(timer);
                    }
                }

                foreach (var timer in due)
                {
                    timer.Fire();
                }
            }

            /// <summary>Removes a timer that was disposed or changed before it fired.</summary>
            private void Forget(ManualTimer timer)
            {
                lock (_gate)
                {
                    _timers.Remove(timer);
                }
            }

            private sealed class ManualTimer : ITimer
            {
                private readonly ManualTimeProvider _owner;
                private readonly TimerCallback _callback;
                private readonly object? _state;
                private bool _fired;

                public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
                {
                    _owner = owner;
                    _callback = callback;
                    _state = state;
                }

                public DateTimeOffset DueAt { get; set; }

                public void Fire()
                {
                    if (_fired)
                    {
                        return;
                    }

                    _fired = true;
                    _callback(_state);
                }

                public bool Change(TimeSpan dueTime, TimeSpan period)
                {
                    if (dueTime == Timeout.InfiniteTimeSpan)
                    {
                        _owner.Forget(this);

                        return true;
                    }

                    DueAt = _owner.GetUtcNow() + dueTime;

                    return true;
                }

                public void Dispose()
                {
                    if (_fired)
                    {
                        return;
                    }

                    _fired = true;
                    _owner.Forget(this);
                }

                public ValueTask DisposeAsync()
                {
                    Dispose();

                    return ValueTask.CompletedTask;
                }
            }
        }

        private sealed class StubEnvironment : IHostEnvironment
        {
            public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "Fluxy.UnitTests";

            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }

        /// <summary>A directory under the temp path that is removed at the end of the test.</summary>
        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "fluxy-backups-" + Guid.NewGuid().ToString("N"));

                System.IO.Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public IReadOnlyList<string> Files()
                => System.IO.Directory.Exists(Path)
                    ? System.IO.Directory.EnumerateFiles(Path, "*.dump")
                        .Select(System.IO.Path.GetFileName)
                        .Select(name => name!)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList()
                    : [];

            public void Dispose()
            {
                if (System.IO.Directory.Exists(Path))
                {
                    System.IO.Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
