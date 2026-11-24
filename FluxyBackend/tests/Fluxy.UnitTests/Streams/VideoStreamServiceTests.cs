using Fluxy.Application.Streams.Services;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Models;
using Fluxy.Core.Content.ReadModels;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Epg.Commands;
using Fluxy.Core.Epg.Enums;
using Fluxy.Core.Epg.Models;
using Fluxy.Core.Epg.ReadModels;
using Fluxy.Core.Epg.Repositories;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Rows;
using Fluxy.Core.Transcoding.Commands;
using Fluxy.Core.Transcoding.Enums;
using Fluxy.Core.Transcoding.Models;
using Fluxy.Core.Transcoding.ReadModels;
using Fluxy.Core.Transcoding.Repositories;
using Fluxy.Core.Vod.Commands;
using Fluxy.Core.Vod.Enums;
using Fluxy.Core.Vod.Models;
using Fluxy.Core.Vod.ReadModels;
using Fluxy.Core.Vod.Repositories;
using Fluxy.Core.Vod.Rows;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The two server references a stream carries: the existence probe per field, and the merged
    /// rule that a permanent stream's transcoder must not be a timeshift-only server.
    /// </summary>
    /// <remarks>
    /// The repositories are faked rather than mocked, the way every other service test here is.
    /// Only the questions these rules ask are answered; everything else throws, so a rule that
    /// silently started asking something new would fail loudly instead of passing for the wrong
    /// reason. The database itself is exercised by <c>VideoStreamTests</c>.
    /// </remarks>
    public sealed class VideoStreamServiceTests
    {
        private static readonly Guid Category = Guid.Parse("00000000-0000-0000-0000-00000000d001");

        [Fact]
        public async Task ACreateRefusesATranscodingServerThatDoesNotExistUnderItsOwnKey()
        {
            var streams = new FakeStreams();
            var service = NewService(streams);

            var outcome = await service.CreateAsync(
                New(type: VideoStreamType.OnDemand) with { TranscodingServerId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewVideoStream.TranscodingServerId)));
            Assert.Null(streams.Added);
        }

        [Fact]
        public async Task ACreateRefusesAnArchiveServerThatDoesNotExistUnderItsOwnKey()
        {
            var streams = new FakeStreams();
            var service = NewService(streams);

            var outcome = await service.CreateAsync(
                New(type: VideoStreamType.OnDemand) with { ArchiveServerId = Guid.NewGuid() },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewVideoStream.ArchiveServerId)));
            Assert.Null(streams.Added);
        }

        [Fact]
        public async Task APermanentStreamRefusesATimeshiftOnlyTranscoder()
        {
            var streamServer = Guid.NewGuid();
            var streams = new FakeStreams();
            var service = NewService(
                streams,
                servers: new FakeServers { TimeshiftOnlyServer = streamServer });

            var outcome = await service.CreateAsync(
                New(type: VideoStreamType.Permanent) with { TranscodingServerId = streamServer },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(NewVideoStream.TranscodingServerId)));
            Assert.Null(streams.Added);
        }

        [Fact]
        public async Task ANonPermanentStreamMayCarryATimeshiftOnlyTranscoder()
        {
            var server = Guid.NewGuid();
            var streams = new FakeStreams();
            var service = NewService(
                streams,
                servers: new FakeServers { TimeshiftOnlyServer = server });

            var outcome = await service.CreateAsync(
                New(type: VideoStreamType.OnDemand) with { TranscodingServerId = server },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Created, outcome.Action);
            Assert.Equal(server, streams.Added!.TranscodingServerId);
        }

        [Fact]
        public async Task APermanentStreamMayCarryATimeshiftOnlyArchiveServer()
        {
            var server = Guid.NewGuid();
            var streams = new FakeStreams();
            var service = NewService(
                streams,
                servers: new FakeServers { TimeshiftOnlyServer = server });

            var outcome = await service.CreateAsync(
                New(type: VideoStreamType.Permanent) with { ArchiveServerId = server },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Created, outcome.Action);
            Assert.Equal(server, streams.Added!.ArchiveServerId);
        }

        [Fact]
        public async Task UpdateMergesThreeStatesForBothServerFields()
        {
            var id = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var streams = new FakeStreams
            {
                Existing = Stored(id) with { TranscodingServerId = first, ArchiveServerId = first }
            };
            var service = NewService(
                streams,
                servers: new FakeServers { Known = [first, second] });

            // Absent keeps both.
            await service.UpdateAsync(
                id,
                new VideoStreamPatch { Name = "Renamed" },
                TestContext.Current.CancellationToken);
            Assert.Equal(first, streams.Written!.TranscodingServerId);
            Assert.Equal(first, streams.Written.ArchiveServerId);

            // A uuid replaces both.
            await service.UpdateAsync(
                id,
                new VideoStreamPatch
                {
                    TranscodingServerId = second.ToString(),
                    ArchiveServerId = second.ToString()
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(second, streams.Written!.TranscodingServerId);
            Assert.Equal(second, streams.Written.ArchiveServerId);

            // An empty string clears both.
            await service.UpdateAsync(
                id,
                new VideoStreamPatch
                {
                    TranscodingServerId = string.Empty,
                    ArchiveServerId = string.Empty
                },
                TestContext.Current.CancellationToken);
            Assert.Null(streams.Written!.TranscodingServerId);
            Assert.Null(streams.Written.ArchiveServerId);
        }

        [Fact]
        public async Task UpdateRefusesAPermanentRowOverATimeshiftOnlyTranscoder()
        {
            var id = Guid.NewGuid();
            var server = Guid.NewGuid();
            var streams = new FakeStreams
            {
                Existing = Stored(id, VideoStreamType.OnDemand) with { TranscodingServerId = server }
            };
            var service = NewService(
                streams,
                servers: new FakeServers { TimeshiftOnlyServer = server });

            var outcome = await service.UpdateAsync(
                id,
                new VideoStreamPatch { Type = VideoStreamType.Permanent },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Invalid, outcome.Action);
            Assert.True(outcome.Errors!.ContainsKey(nameof(VideoStreamPatch.TranscodingServerId)));
            Assert.Null(streams.Written);
        }

        [Fact]
        public async Task UpdateLeavesTheBindingAloneWhenTheKindMovesAwayFromPermanent()
        {
            var id = Guid.NewGuid();
            var server = Guid.NewGuid();
            var streams = new FakeStreams
            {
                Existing = Stored(id) with { TranscodingServerId = server }
            };
            var service = NewService(
                streams,
                servers: new FakeServers { TimeshiftOnlyServer = server });

            var outcome = await service.UpdateAsync(
                id,
                new VideoStreamPatch { Type = VideoStreamType.OnDemand },
                TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Updated, outcome.Action);
            Assert.Equal(server, streams.Written!.TranscodingServerId);
        }

        private static VideoStreamService NewService(
            FakeStreams streams,
            FakeServers? servers = null)
            => new(
                streams,
                new FakeEpgSources(),
                new FakeVideos(),
                new FakeBouquets(),
                new FakeTranscodingProfiles(),
                servers ?? new FakeServers(),
                new FakeUnitOfWork());

        private static NewVideoStream New(
            VideoStreamType type = VideoStreamType.Permanent,
            string name = "News")
            => new()
            {
                Name = name,
                Type = type,
                Status = VideoStreamStatus.Stopped,
                CategoryId = Category,
                DelayMinutes = 0,
                ArchiveDays = 0,
                Sources = ["https://example.test/source"],
                Files = []
            };

        private static VideoStream Stored(
            Guid id,
            VideoStreamType type = VideoStreamType.Permanent)
            => new()
            {
                Id = id,
                Name = "News",
                Status = VideoStreamStatus.Stopped,
                Type = type,
                CategoryId = Category,
                CategoryName = "Live",
                DelayMinutes = 0,
                ArchiveDays = 0,
                Sources = ["https://example.test/source"],
                Files = []
            };

        /// <summary>The server repository, answering only the reference probe.</summary>
        private sealed class FakeServers : IServerRepository
        {
            /// <summary>Identifiers that exist and are not timeshift-only.</summary>
            public HashSet<Guid> Known { get; init; } = [];

            /// <summary>An identifier that exists and is timeshift-only, or null.</summary>
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

            public Task<IReadOnlyList<ServerOption>> GetOptionsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountReferencesAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

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

        /// <summary>The stream repository, recording the write it was handed.</summary>
        private sealed class FakeStreams : IVideoStreamRepository
        {
            public VideoStream? Existing { get; init; }

            public VideoStreamWrite? Added { get; private set; }

            public VideoStreamWrite? Written { get; private set; }

            public Task<Guid> AddAsync(
                VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
            {
                Added = stream;

                return Task.FromResult(Guid.NewGuid());
            }

            public Task<bool> UpdateAsync(
                Guid id,
                VideoStreamWrite stream,
                CancellationToken cancellationToken = default)
            {
                Written = stream;

                return Task.FromResult(true);
            }

            public Task<bool> CategoryIsUsableAsync(
                Guid categoryId,
                CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid categoryId,
                Guid? exceptStream,
                CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public Task<VideoStream?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Existing);

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

            public Task<IReadOnlyList<VideoStreamImportRow>> GetImportRowsAsync(
                Guid categoryId,
                IReadOnlyCollection<string> names,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task UpdateRangeAsync(
                IReadOnlyList<VideoStreamBatchUpdate> updates,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Guid>> AddRangeAsync(
                IReadOnlyList<VideoStreamWrite> streams,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStreamStatus?> SetStatusAsync(
                Guid id,
                VideoStreamStatus status,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The guide-source repository: nothing in these tests configures a guide.</summary>
        private sealed class FakeEpgSources : IEpgSourceRepository
        {
            public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public Task<EpgSourcePage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                EpgSourceSortField sortBy,
                EpgSourceSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<EpgSource?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<EpgSourceChannelPage> GetChannelsPageAsync(
                Guid sourceId,
                int page,
                int pageSize,
                string? search,
                EpgSourceChannelSortField sortBy,
                EpgSourceChannelSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptSource,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountStreamsUsingAsync(
                Guid sourceId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(EpgSourceWrite source, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                EpgSourceWrite source,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The on-demand repository: no playlist names a video in these tests.</summary>
        private sealed class FakeVideos : IVodRepository
        {
            public Task<IReadOnlySet<Guid>> GetExistingIdsAsync(
                IReadOnlyCollection<Guid> ids,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlySet<Guid>>(ids.ToHashSet());

            public Task<VodPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? categoryId,
                VodStatus? status,
                VodType? type,
                VodSortField sortBy,
                VodSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VodItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VodItem?> FindForUpdateAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> CategoryIsUsableAsync(
                Guid categoryId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid categoryId,
                Guid? exceptVod,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<VodImportRow>> GetImportRowsAsync(
                Guid categoryId,
                IReadOnlyCollection<string> names,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(VodItemWrite item, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<Guid>> AddRangeAsync(
                IReadOnlyList<VodItemWrite> items,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                VodItemWrite item,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VodStatus?> SetStatusAsync(
                Guid id,
                VodStatus status,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>The bouquet repository: the tests name no bouquet.</summary>
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

        /// <summary>The profile repository: the tests name no profile.</summary>
        private sealed class FakeTranscodingProfiles : ITranscodingProfileRepository
        {
            public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(false);

            public Task<TranscodingProfilePage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                TranscodingProfileSortField sortBy,
                TranscodingProfileSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<TranscodingProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<TranscodingProfileOption>> GetOptionsAsync(
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> IsNameTakenAsync(
                string name,
                Guid? exceptProfile,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<int> CountReferencesAsync(
                Guid profileId,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Guid> AddAsync(
                TranscodingProfileWrite profile,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<bool> UpdateAsync(
                Guid id,
                TranscodingProfileWrite profile,
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
