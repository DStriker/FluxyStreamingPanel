using Fluxy.Application.Streams.Services;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The rules a playlist import applies to the whole batch rather than to one line: the two
    /// server references it may set, and the permanence rule those references turn on.
    /// </summary>
    /// <remarks>
    /// The service runs against fakes, so what is proven here is the rule and the projection -
    /// that a refused identifier answers under its own field and leaves the repository untouched,
    /// and that an accepted one lands on every row the batch writes. The rest of the import
    /// (playlist parsing, name collisions, the single save) is covered by the integration tests,
    /// where the database is the point.
    /// </remarks>
    public sealed class VideoStreamImportServiceTests
    {
        private static readonly Guid Category = Guid.NewGuid();

        [Fact]
        public async Task AnUnknownTranscoderIsRefusedUnderItsOwnFieldAndNothingIsWritten()
        {
            var streams = new FakeStreams();
            var service = NewService(streams, new FakeServers());

            var outcome = await service.ImportAsync(
                Import(transcodingServerId: Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Invalid, outcome.Status);
            Assert.True(
                outcome.Errors!.ContainsKey(nameof(NewVideoStreamImport.TranscodingServerId)));
            Assert.Empty(streams.Added);
        }

        [Fact]
        public async Task AnUnknownArchiveServerIsRefusedUnderItsOwnFieldAndNothingIsWritten()
        {
            var streams = new FakeStreams();
            var service = NewService(streams, new FakeServers());

            var outcome = await service.ImportAsync(
                Import(archiveServerId: Guid.NewGuid()),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Invalid, outcome.Status);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewVideoStreamImport.ArchiveServerId)));
            Assert.Empty(streams.Added);
        }

        [Fact]
        public async Task APermanentImportRefusesATimeshiftOnlyTranscoder()
        {
            var streams = new FakeStreams();
            var timeshift = Guid.NewGuid();
            var service = NewService(
                streams,
                new FakeServers { TimeshiftOnlyServer = timeshift });

            var outcome = await service.ImportAsync(
                Import(transcodingServerId: timeshift),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Invalid, outcome.Status);
            Assert.True(
                outcome.Errors!.ContainsKey(nameof(NewVideoStreamImport.TranscodingServerId)));
            Assert.Empty(streams.Added);
        }

        [Fact]
        public async Task ANonPermanentImportMayTranscodeWithATimeshiftOnlyServer()
        {
            var streams = new FakeStreams();
            var timeshift = Guid.NewGuid();
            var service = NewService(
                streams,
                new FakeServers { TimeshiftOnlyServer = timeshift });

            var outcome = await service.ImportAsync(
                Import(VideoStreamType.OnDemand, transcodingServerId: timeshift),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Completed, outcome.Status);
            Assert.All(streams.Added, write => Assert.Equal(timeshift, write.TranscodingServerId));
        }

        [Fact]
        public async Task APermanentImportMayKeepATimeshiftOnlyArchiveServer()
        {
            var streams = new FakeStreams();
            var timeshift = Guid.NewGuid();
            var service = NewService(
                streams,
                new FakeServers { TimeshiftOnlyServer = timeshift });

            var outcome = await service.ImportAsync(
                Import(archiveServerId: timeshift, archiveDays: 7),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Completed, outcome.Status);
            Assert.All(streams.Added, write => Assert.Equal(timeshift, write.ArchiveServerId));
        }

        [Fact]
        public async Task BothAcceptedReferencesLandOnEveryCreatedRow()
        {
            var streams = new FakeStreams();
            var transcoder = Guid.NewGuid();
            var archiver = Guid.NewGuid();
            var service = NewService(
                streams,
                new FakeServers { Known = [transcoder, archiver] });

            var outcome = await service.ImportAsync(
                Import(transcodingServerId: transcoder, archiveServerId: archiver),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Completed, outcome.Status);
            Assert.Equal(2, streams.Added.Count);
            Assert.All(streams.Added, write =>
            {
                Assert.Equal(transcoder, write.TranscodingServerId);
                Assert.Equal(archiver, write.ArchiveServerId);
            });
        }

        [Fact]
        public async Task AnOverwriteOfARowBoundToATimeshiftOnlyTranscoderRefusesTheWholeImport()
        {
            var streams = new FakeStreams();
            var timeshift = Guid.NewGuid();
            streams.Existing.Add(new VideoStreamImportRow
            {
                Id = Guid.NewGuid(),
                Name = "First",
                Type = VideoStreamType.OnDemand,
                TranscodingServerId = timeshift
            });
            var service = NewService(
                streams,
                new FakeServers { TimeshiftOnlyServer = timeshift });

            var outcome = await service.ImportAsync(
                Import(overwriteExistingSettings: true),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Invalid, outcome.Status);
            Assert.True(
                outcome.Errors!.ContainsKey(nameof(NewVideoStreamImport.TranscodingServerId)));
            Assert.Empty(streams.Added);
            Assert.Empty(streams.Updated);
        }

        [Fact]
        public async Task AnOverwriteOfAnUnboundRowCompletes()
        {
            var streams = new FakeStreams();
            streams.Existing.Add(new VideoStreamImportRow
            {
                Id = Guid.NewGuid(),
                Name = "First",
                Type = VideoStreamType.Permanent
            });
            var service = NewService(streams, new FakeServers());

            var outcome = await service.ImportAsync(
                Import(overwriteExistingSettings: true),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Completed, outcome.Status);
            Assert.Single(streams.Updated);
            Assert.Single(streams.Added);
        }

        [Fact]
        public async Task AnOverwriteOfARowBoundToAnOrdinaryTranscoderCompletes()
        {
            var streams = new FakeStreams();
            var transcoder = Guid.NewGuid();
            streams.Existing.Add(new VideoStreamImportRow
            {
                Id = Guid.NewGuid(),
                Name = "First",
                Type = VideoStreamType.OnDemand,
                TranscodingServerId = transcoder
            });
            var service = NewService(
                streams,
                new FakeServers { Known = [transcoder] });

            var outcome = await service.ImportAsync(
                Import(overwriteExistingSettings: true),
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamImportStatus.Completed, outcome.Status);
            Assert.Single(streams.Updated);
            Assert.Single(streams.Added);
        }

        private static VideoStreamImportService NewService(
            FakeStreams streams,
            FakeServers servers) => new(
                streams,
                new FakeBouquets(),
                servers,
                new FakeUnitOfWork());

        private static NewVideoStreamImport Import(
            VideoStreamType type = VideoStreamType.Permanent,
            Guid? transcodingServerId = null,
            Guid? archiveServerId = null,
            int archiveDays = 0,
            bool overwriteExistingSettings = false) => new()
            {
                Type = type,
                Status = VideoStreamStatus.Stopped,
                CategoryId = Category,
                Playlist =
                    "#EXTM3U\n"
                    + "#EXTINF:-1,First\n"
                    + "https://example.test/first\n"
                    + "#EXTINF:-1,Second\n"
                    + "https://example.test/second\n",
                ArchiveDays = archiveDays,
                TranscodingServerId = transcodingServerId,
                ArchiveServerId = archiveServerId,
                OverwriteExistingSettings = overwriteExistingSettings
            };

        /// <summary>The stream repository: one usable category and the rows the test planted.</summary>
        private sealed class FakeStreams : IVideoStreamRepository
        {
            public List<VideoStreamImportRow> Existing { get; } = [];

            public List<VideoStreamWrite> Added { get; } = [];

            public List<VideoStreamBatchUpdate> Updated { get; } = [];

            public Task<bool> CategoryIsUsableAsync(
                Guid categoryId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task<IReadOnlyList<VideoStreamImportRow>> GetImportRowsAsync(
                Guid categoryId,
                IReadOnlyCollection<string> names,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<VideoStreamImportRow>>(
                    Existing.Where(row => names.Contains(row.Name)).ToList());

            public Task<IReadOnlyList<Guid>> AddRangeAsync(
                IReadOnlyList<VideoStreamWrite> streams,
                CancellationToken cancellationToken = default)
            {
                Added.AddRange(streams);

                return Task.FromResult<IReadOnlyList<Guid>>(
                    streams.Select(_ => Guid.NewGuid()).ToList());
            }

            public Task UpdateRangeAsync(
                IReadOnlyList<VideoStreamBatchUpdate> updates,
                CancellationToken cancellationToken = default)
            {
                Updated.AddRange(updates);

                return Task.CompletedTask;
            }

            public Task<VideoStreamPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? categoryId,
                VideoStreamStatus? status,
                VideoStreamType? type,
                VideoStreamSortField sortBy,
                VideoStreamSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStream?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStream?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid categoryId,
                Guid? exceptStream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStreamStatus?> SetStatusAsync(
                Guid id,
                VideoStreamStatus status,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<VideoStreamStatusCountRow>> ReadStatusCountsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The bouquet repository: these imports name no bouquet.</summary>
        private sealed class FakeBouquets : IBouquetRepository
        {
            public Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
                IReadOnlyCollection<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(bouquetIds.ToHashSet());

            public Task SetItemBouquetIdsAsync(
                ContentType type,
                Guid contentId,
                IReadOnlyList<Guid> bouquetIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<BouquetListItem>> GetAllAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Bouquet>> GetTrackedAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Bouquet?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptBouquet,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> GetLastSortOrderAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                NewBouquet bouquet,
                int sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetNameAsync(
                Guid id,
                string name,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetSortOrderAsync(
                Guid id,
                int sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountItemsAsync(Guid bouquetId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<BouquetContent> GetContentAsync(
                Guid bouquetId,
                ContentType type,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task SetContentAsync(
                Guid bouquetId,
                ContentType type,
                IReadOnlyList<Guid> itemIds,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Guid>> GetItemBouquetIdsAsync(
                ContentType type,
                Guid contentId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The server repository, answering only the reference probe.</summary>
        private sealed class FakeServers : IServerRepository
        {
            public HashSet<Guid> Known { get; init; } = [];

            public Guid? TimeshiftOnlyServer { get; init; }

            public Task<ServerReference?> GetReferenceAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                if (TimeshiftOnlyServer == id)
                {
                    return Task.FromResult<ServerReference?>(new ServerReference
                    {
                        Id = id,
                        Name = "Timeshift",
                        TimeshiftOnly = true
                    });
                }

                return Task.FromResult(Known.Contains(id)
                    ? new ServerReference { Id = id, Name = "Edge", TimeshiftOnly = false }
                    : null);
            }

            public Task<ServerPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                ServerSortField sortBy,
                ServerSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Server?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<ServerOption>> GetOptionsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Server>> GetAllAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountReferencesAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> ExistsMainAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptServer,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(ServerWrite server, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                ServerWrite server,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The unit of work, committing without a failure.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
