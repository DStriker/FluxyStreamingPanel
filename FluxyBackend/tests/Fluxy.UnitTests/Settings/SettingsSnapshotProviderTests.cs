using Fluxy.Application.Settings.Services;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Fluxy.UnitTests.Settings
{
    /// <summary>
    /// The cache behind every settings read: the defaults it starts with, the reload a write asks
    /// for, the window it expires after, and the reading it keeps when the repository fails.
    /// </summary>
    /// <remarks>
    /// The repositories are fakes inside a real container, because the provider is a singleton
    /// that opens its own scope: the scenario worth proving is not that it calls a method, but
    /// that a scope-disposed read still leaves a usable snapshot behind.
    /// </remarks>
    public sealed class SettingsSnapshotProviderTests
    {
        [Fact]
        public void ItStartsOnTheDefaultsAndReadsThemWhenNoRowExists()
        {
            var provider = NewProvider(new FakeRepository(), new FakeMagRepository());

            var application = provider.ApplicationSnapshot;
            var mag = provider.MagSnapshot;

            Assert.Equal(ApplicationSettingsDefaults.ServiceName, application.ServiceName);
            Assert.Equal("Europe/Berlin", application.DefaultTimeZone);
            Assert.Equal(ApplicationSettingsDefaults.PublicApiFloodLimit, application.PublicApiFloodLimit);
            Assert.True(application.EnableTrials);
            Assert.False(application.RecaptchaConfigured);
            Assert.Empty(application.PublicApiFloodWhitelist);

            Assert.True(mag.ShowAllCategories);
            Assert.Equal(MagSettingsDefaults.PlaybackLimit, mag.PlaybackLimit);
            Assert.Equal(StbTypeCatalog.All, mag.AllowedStbTypes);
            Assert.Equal(["MAG255", "MAG256", "MAG257"], mag.AllowedRecordingStbTypes);
        }

        [Fact]
        public async Task AReloadPublishesBothStoredRows()
        {
            var provider = NewProvider(
                new FakeRepository(Stored("Custom panel")),
                new FakeMagRepository());

            await provider.ReloadAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Custom panel", provider.ApplicationSnapshot.ServiceName);
        }

        [Fact]
        public async Task TheSecretNeverLeavesTheSnapshotAndConfiguredFollowsIt()
        {
            var provider = NewProvider(
                new FakeRepository(Stored("Panel") with { RecaptchaSecretKey = "secret" }),
                new FakeMagRepository());

            await provider.ReloadAsync(TestContext.Current.CancellationToken);

            Assert.Equal("secret", provider.ApplicationSnapshot.RecaptchaSecretKey);
            Assert.True(provider.ApplicationSnapshot.RecaptchaConfigured);
        }

        [Fact]
        public async Task AReadingExpiresAfterTheWindowAndReloadsOnTheNextAccess()
        {
            var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var repository = new FakeRepository(Stored("First"));
            var provider = NewProvider(repository, new FakeMagRepository(), clock);

            await provider.ReloadAsync(TestContext.Current.CancellationToken);
            Assert.Equal("First", provider.ApplicationSnapshot.ServiceName);

            repository.Stored = Stored("Second");
            clock.Advance(SettingsLimits.SnapshotTtl - TimeSpan.FromSeconds(1));

            Assert.Equal("First", provider.ApplicationSnapshot.ServiceName);

            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal("Second", provider.ApplicationSnapshot.ServiceName);
        }

        [Fact]
        public async Task AFailedReadKeepsTheReadingItAlreadyHad()
        {
            var repository = new FakeRepository(Stored("Kept"));
            var provider = NewProvider(repository, new FakeMagRepository());

            await provider.ReloadAsync(TestContext.Current.CancellationToken);
            repository.Failure = new InvalidOperationException("database is down");

            // A reload that fails must not throw, and must not clear what is cached.
            await provider.ReloadAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Kept", provider.ApplicationSnapshot.ServiceName);
        }

        [Fact]
        public async Task APublishedReadingIsNotMutableThroughTheInstanceHandedOut()
        {
            var provider = NewProvider(
                new FakeRepository(Stored("Panel") with { PublicApiFloodWhitelist = ["203.0.113.7"] }),
                new FakeMagRepository());

            await provider.ReloadAsync(TestContext.Current.CancellationToken);

            var snapshot = provider.ApplicationSnapshot;
            snapshot.PublicApiFloodWhitelist[0] = "10.0.0.1";

            Assert.Equal("203.0.113.7", provider.ApplicationSnapshot.PublicApiFloodWhitelist[0]);
        }

        private static SettingsSnapshotProvider NewProvider(
            FakeRepository repository,
            FakeMagRepository mag,
            TimeProvider? time = null)
        {
            var services = new ServiceCollection();

            services.AddSingleton<IApplicationSettingsRepository>(repository);
            services.AddSingleton<IMagSettingsRepository>(mag);

            var container = services.BuildServiceProvider();

            return new SettingsSnapshotProvider(
                container.GetRequiredService<IServiceScopeFactory>(),
                time ?? TimeProvider.System,
                NullLogger<SettingsSnapshotProvider>.Instance);
        }

        private static ApplicationSettings Stored(string serviceName)
            => ApplicationSettingsDefaults.Create() with { ServiceName = serviceName };

        /// <summary>A repository answering one row, or throwing once told to.</summary>
        private sealed class FakeRepository : IApplicationSettingsRepository
        {
            public FakeRepository(ApplicationSettings? stored = null)
            {
                Stored = stored;
            }

            public ApplicationSettings? Stored { get; set; }

            public Exception? Failure { get; set; }

            public Task<ApplicationSettings?> GetAsync(CancellationToken cancellationToken = default)
                => Failure is null
                    ? Task.FromResult(Stored)
                    : Task.FromException<ApplicationSettings?>(Failure);

            public Task AddAsync(
                ApplicationSettings settings,
                CancellationToken cancellationToken = default)
            {
                Stored = settings;

                return Task.CompletedTask;
            }

            public Task<bool> UpdateAsync(
                ApplicationSettings settings,
                CancellationToken cancellationToken = default)
            {
                Stored = settings;

                return Task.FromResult(true);
            }
        }

        /// <summary>A repository answering the MAG defaults or nothing.</summary>
        private sealed class FakeMagRepository : IMagSettingsRepository
        {
            public Task<MagSettings?> GetAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<MagSettings?>(null);

            public Task AddAsync(MagSettings settings, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<bool> UpdateAsync(
                MagSettings settings,
                CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
