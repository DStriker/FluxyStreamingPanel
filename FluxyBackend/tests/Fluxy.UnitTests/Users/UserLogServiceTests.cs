using Fluxy.Application.Authentication.Utils;
using Fluxy.Application.Users.Services;
using Fluxy.Core.GeoIp.Interfaces;
using Fluxy.Core.GeoIp.ReadModels;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.ReadModels;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The write path of the user log: the GeoIP snapshot it takes, the truncation it applies, the
    /// commit it owns, and the fact that a failure never escapes.
    /// </summary>
    /// <remarks>
    /// The failure cases are the point of this service. It is called from flows that have already
    /// succeeded, so a broken audit table must lose the entry and nothing else - which is only
    /// observable here, in a test that makes the repository and the unit of work throw.
    /// </remarks>
    public sealed class UserLogServiceTests
    {
        private static readonly Guid Account = Guid.NewGuid();

        private static UserLogService Build(
            FakeUserLogs logs,
            FakeUnitOfWork unitOfWork,
            IGeoIpResolver geoIp)
            => new(logs, unitOfWork, new GeoIpMemo(geoIp), NullLogger<UserLogService>.Instance);

        [Fact]
        public async Task ARecordStagesTheRowAndCommitsItOnce()
        {
            var logs = new FakeUserLogs();
            var unitOfWork = new FakeUnitOfWork();

            await Build(logs, unitOfWork, new FakeGeoIp("DE", "Deutsche Telekom"))
                .RecordAsync(Entry(), TestContext.Current.CancellationToken);

            Assert.NotNull(logs.Added);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheGeoIpSnapshotIsStoredUpperCasedWithTheOrganizationAsProvider()
        {
            var logs = new FakeUserLogs();

            await Build(logs, new FakeUnitOfWork(), new FakeGeoIp("de", "Telekom"))
                .RecordAsync(Entry(), TestContext.Current.CancellationToken);

            Assert.Equal("DE", logs.Added!.CountryCode);
            Assert.Equal("Telekom", logs.Added.Isp);
        }

        [Fact]
        public async Task AnUnknownAddressLeavesTheCountryAndProviderEmpty()
        {
            var logs = new FakeUserLogs();

            await Build(logs, new FakeUnitOfWork(), new FakeGeoIp(null, null))
                .RecordAsync(Entry(), TestContext.Current.CancellationToken);

            Assert.Null(logs.Added!.CountryCode);
            Assert.Null(logs.Added.Isp);
            Assert.Equal("203.0.113.7", logs.Added.Ip);
        }

        [Fact]
        public async Task TheOptionalColumnsAreCutToTheirWidths()
        {
            var logs = new FakeUserLogs();

            await Build(
                    logs,
                    new FakeUnitOfWork(),
                    new FakeGeoIp("DEU", new string('o', 400)))
                .RecordAsync(
                    Entry() with
                    {
                        Ip = new string('1', UserLogListLimits.IpMaxLength + 20),
                        UserAgent = new string('u', UserLogListLimits.UserAgentMaxLength + 100)
                    },
                    TestContext.Current.CancellationToken);

            Assert.Equal(UserLogListLimits.IpMaxLength, logs.Added!.Ip!.Length);
            Assert.Equal(UserLogListLimits.IspMaxLength, logs.Added.Isp!.Length);
            Assert.Equal(UserLogListLimits.CountryCodeLength, logs.Added.CountryCode!.Length);
            Assert.Equal(
                UserLogListLimits.UserAgentMaxLength,
                logs.Added.UserAgent!.Length);
        }

        [Fact]
        public async Task ADescriptionLongerThanTheColumnIsCutRatherThanRefused()
        {
            var logs = new FakeUserLogs();

            await Build(logs, new FakeUnitOfWork(), new FakeGeoIp(null, null))
                .RecordAsync(
                    Entry() with
                    {
                        Description = new string('d', UserLogDescriptions.DescriptionMaxLength + 50)
                    },
                    TestContext.Current.CancellationToken);

            Assert.Equal(
                UserLogDescriptions.DescriptionMaxLength,
                logs.Added!.Description.Length);
        }

        [Fact]
        public async Task AFailedCommitIsSwallowedSoTheAuditedFlowIsUnaffected()
        {
            var logs = new FakeUserLogs();
            var unitOfWork = new FakeUnitOfWork(new InvalidOperationException("audit table gone"));

            // The call must complete rather than throw: the flow that records an event has already
            // succeeded, and an audit trail is not a precondition for what it describes.
            await Build(logs, unitOfWork, new FakeGeoIp(null, null))
                .RecordAsync(Entry(), TestContext.Current.CancellationToken);

            Assert.Equal(1, unitOfWork.Saves);
            Assert.Equal(1, logs.Discards);
        }

        [Fact]
        public async Task ARepositoryThatThrowsIsSwallowedToo()
        {
            var logs = new FakeUserLogs { AddFailure = new InvalidOperationException("no table") };

            await Build(logs, new FakeUnitOfWork(), new FakeGeoIp(null, null))
                .RecordAsync(Entry(), TestContext.Current.CancellationToken);

            Assert.Equal(1, logs.Discards);
        }

        [Fact]
        public async Task ACancelledRequestIsNotTreatedAsAnAuditFailure()
        {
            var logs = new FakeUserLogs();
            using var cancellation = new CancellationTokenSource();

            await cancellation.CancelAsync();

            // TaskCanceledException derives from OperationCanceledException, and which of the two
            // surfaces depends on how the cancellation was observed - either one is the
            // "caller went away" case the service must not swallow.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Build(logs, new FakeUnitOfWork(), new FakeGeoIp(null, null))
                    .RecordAsync(Entry(), cancellation.Token));
        }

        private static NewUserLog Entry() => new()
        {
            UserId = Account,
            RecordType = UserLogRecordType.Login,
            IsSuccess = true,
            Description = UserLogDescriptions.Describe(
                UserLogDescriptions.LoginSucceeded,
                "alice"),
            Ip = "203.0.113.7",
            UserAgent = "Mozilla/5.0"
        };

        /// <summary>The resolver, answering the one snapshot the test declares.</summary>
        private sealed class FakeGeoIp : IGeoIpResolver
        {
            private readonly string? _countryCode;
            private readonly string? _organization;

            public FakeGeoIp(string? countryCode, string? organization)
            {
                _countryCode = countryCode;
                _organization = organization;
            }

            public Task<GeoIpInfo> ResolveAsync(
                string? clientAddress,
                CancellationToken cancellationToken = default)
            {
                // A real resolver would abandon the work; the fake does the same so the test can
                // observe that a cancelled request is rethrown rather than swallowed as an audit
                // failure.
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled<GeoIpInfo>(cancellationToken);
                }

                return Task.FromResult(new GeoIpInfo
                {
                    Ip = clientAddress,
                    CountryCode = _countryCode,
                    Organization = _organization
                });
            }
        }

        /// <summary>The repository, recording the row the service staged.</summary>
        private sealed class FakeUserLogs : IUserLogRepository
        {
            /// <summary>Failure the staging step throws, when the test declares one.</summary>
            public Exception? AddFailure { get; init; }

            public NewUserLog? Added { get; private set; }

            public Task<UserLogPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                IReadOnlyList<Guid>? userIds,
                IReadOnlyList<Guid>? groupIds,
                IReadOnlyList<UserLogRecordType>? recordTypes,
                string? ip,
                string? countryCode,
                string? isp,
                DateTimeOffset? dateFrom,
                DateTimeOffset? dateTo,
                UserLogSortField sortBy,
                UserLogSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException("The write path never reads a page.");

            public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException("The write path never clears the table.");

            public Task AddAsync(NewUserLog entry, CancellationToken cancellationToken = default)
            {
                if (AddFailure is not null)
                {
                    return Task.FromException(AddFailure);
                }

                Added = entry;

                return Task.CompletedTask;
            }

            /// <summary>How many times a failed write was asked to drop its staged row.</summary>
            public int Discards { get; private set; }

            public void DiscardStaged() => Discards += 1;
        }

        /// <summary>The unit of work, either committing or failing with what the test declares.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly Exception? _failure;

            public FakeUnitOfWork(Exception? failure = null) => _failure = failure;

            /// <summary>How many times the service asked for a commit.</summary>
            public int Saves { get; private set; }

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                Saves += 1;

                return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
            }
        }
    }
}
