using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Repositories;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Content
{
    /// <summary>
    /// The persistence rules of the blacklist table: a row per list round-trips, and the unique
    /// index over (type, value) refuses a duplicate inside one list while allowing the same
    /// text in another.
    /// </summary>
    /// <remarks>
    /// The rows are planted through the Core repository rather than through SQL, the way every
    /// other fixture here is: the repository is the thing under test, and the index is what the
    /// service's pre-check is only a friendly message for.
    /// </remarks>
    public sealed class BlacklistEntryRepositoryTests : IntegrationTestBase
    {
        /// <summary>
        /// The name of the unique index, written out because the assertion is about the
        /// database's own name rather than about a constant the service happens to share.
        /// </summary>
        private const string TypeValueIndex = "ux_blacklist_entries_type_value";

        public BlacklistEntryRepositoryTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AnEntryPerListRoundTrips()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var entries = scope.ServiceProvider.GetRequiredService<IBlacklistEntryRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cancellationToken = TestContext.Current.CancellationToken;

            var ip = await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "203.0.113.9", Note = "scanner" },
                cancellationToken);
            var isp = await entries.AddAsync(
                BlacklistType.Isp,
                new BlacklistEntryWrite { Value = "Example Telecom" },
                cancellationToken);
            var agent = await entries.AddAsync(
                BlacklistType.UserAgent,
                new BlacklistEntryWrite { Value = "curl/8.0" },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var stored = await entries.GetAsync(ip, cancellationToken);

            Assert.NotNull(stored);
            Assert.Equal(BlacklistType.Ip, stored.Type);
            Assert.Equal("203.0.113.9", stored.Value);
            Assert.Equal("scanner", stored.Note);

            // A new entry has blocked nothing, and no endpoint has a way to change that.
            Assert.Equal(0, stored.BlockedAttempts);

            // The added date is stamped by the context, and both audit stamps agree on a row
            // that has never been touched.
            Assert.Equal(stored.CreatedAt, stored.UpdatedAt);
            Assert.NotEqual(default, stored.CreatedAt);

            var ispStored = await entries.GetAsync(isp, cancellationToken);
            var agentStored = await entries.GetAsync(agent, cancellationToken);

            Assert.Equal(BlacklistType.Isp, ispStored?.Type);
            Assert.Null(ispStored?.Note);
            Assert.Equal(BlacklistType.UserAgent, agentStored?.Type);
        }

        [Fact]
        public async Task TheIndexRefusesADuplicateInsideOneList()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var entries = scope.ServiceProvider.GetRequiredService<IBlacklistEntryRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cancellationToken = TestContext.Current.CancellationToken;

            await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "203.0.113.10" },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "203.0.113.10" },
                cancellationToken);

            var violation = await Assert.ThrowsAsync<UniqueViolationException>(
                () => unitOfWork.SaveChangesAsync(cancellationToken));

            Assert.True(violation.IsConstraint(TypeValueIndex));
        }

        [Fact]
        public async Task TheIndexAllowsTheSameTextInAnotherList()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var entries = scope.ServiceProvider.GetRequiredService<IBlacklistEntryRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cancellationToken = TestContext.Current.CancellationToken;

            await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "203.0.113.11" },
                cancellationToken);
            await entries.AddAsync(
                BlacklistType.Isp,
                new BlacklistEntryWrite { Value = "203.0.113.11" },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            Assert.True(await entries.IsValueTakenAsync(
                BlacklistType.Ip, "203.0.113.11", null, cancellationToken));
            Assert.True(await entries.IsValueTakenAsync(
                BlacklistType.Isp, "203.0.113.11", null, cancellationToken));
            Assert.False(await entries.IsValueTakenAsync(
                BlacklistType.UserAgent, "203.0.113.11", null, cancellationToken));
        }

        [Fact]
        public async Task AChangeKeepsTheCounterAndTheAddedDate()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var entries = scope.ServiceProvider.GetRequiredService<IBlacklistEntryRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cancellationToken = TestContext.Current.CancellationToken;

            var id = await entries.AddAsync(
                BlacklistType.Isp,
                new BlacklistEntryWrite { Value = "Original provider", Note = "first" },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var before = await entries.GetAsync(id, cancellationToken);

            // The one write path the API has: value and note only.
            Assert.True(await entries.UpdateAsync(
                id,
                new BlacklistEntryWrite { Value = "Renamed provider", Note = null },
                cancellationToken));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await entries.GetAsync(id, cancellationToken);

            Assert.NotNull(after);
            Assert.Equal("Renamed provider", after.Value);
            Assert.Null(after.Note);
            Assert.Equal(before?.BlockedAttempts, after.BlockedAttempts);
            Assert.Equal(before?.CreatedAt, after.CreatedAt);
            Assert.Equal(before?.Type, after.Type);
        }

        [Fact]
        public async Task AListIsReadOneTypeAtATimeNewestFirst()
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var entries = scope.ServiceProvider.GetRequiredService<IBlacklistEntryRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cancellationToken = TestContext.Current.CancellationToken;

            await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "first", Note = "alpha" },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // The audit clock is the host's `TimeProvider` and the test host freezes it, so two
            // creates a moment apart would share a `created_at` and the order would fall to the
            // identifier tie-break. Advancing it keeps this test about the *order*. One second,
            // not more: the same clock mints the access tokens, and a jump past the validation
            // skew would refuse every sign-in that follows in this run.
            Fixture.Clock.Advance(TimeSpan.FromSeconds(1));

            await entries.AddAsync(
                BlacklistType.Ip,
                new BlacklistEntryWrite { Value = "second" },
                cancellationToken);
            await entries.AddAsync(
                BlacklistType.Isp,
                new BlacklistEntryWrite { Value = "other list" },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var page = await entries.GetPageAsync(
                BlacklistType.Ip,
                1,
                20,
                null,
                BlacklistSortField.CreatedAt,
                BlacklistSortOrder.Descending,
                cancellationToken);

            // Only the IP entries, and the newest of those first.
            Assert.Equal(2, page.Total);
            Assert.Equal(["second", "first"], page.Items.Select(item => item.Value).ToArray());

            // Search covers the note as well as the value.
            var note = await entries.GetPageAsync(
                BlacklistType.Ip,
                1,
                20,
                "ALPHA",
                BlacklistSortField.CreatedAt,
                BlacklistSortOrder.Descending,
                cancellationToken);

            Assert.Equal(1, note.Total);
            Assert.Equal("first", note.Items[0].Value);
        }
    }
}
