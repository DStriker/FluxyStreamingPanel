using Fluxy.Application.Streams.Services;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Interfaces;
using Fluxy.Core.Streams.Models;
using Fluxy.Core.Streams.Outcomes;
using Fluxy.Core.Streams.ReadModels;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The rules that belong to a bulk operation rather than to one row: which kinds a value may
    /// be written to, and what a type change must clear before it can land.
    /// </summary>
    /// <remarks>
    /// The service is exercised against a fake <see cref="IVideoStreamService"/> rather than a
    /// database. Every rule in <see cref="VideoStreamBulkService"/> is a decision about what to
    /// ask the single-row service for - the delegation itself is covered by the integration
    /// tests - so the fake records those requests instead of carrying them out.
    /// </remarks>
    public sealed class VideoStreamBulkServiceTests
    {
        [Fact]
        public async Task ATypeChangeClearsEverySettingTheNewKindCannotCarry()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[id] = Row(id, delay: 12, archive: 7, userAgent: "agent");
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeType,
                id,
                type: VideoStreamType.Redirect), TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.Equal(VideoStreamAction.Updated, item.Action);
            Assert.NotNull(item.Warnings);
            Assert.Equal(
                new[]
                {
                    VideoStreamBulkWarning.DelayCleared,
                    VideoStreamBulkWarning.ArchiveCleared,
                    VideoStreamBulkWarning.RestreamCleared
                },
                item.Warnings);

            var patch = Assert.Single(streams.Updates).Patch;
            Assert.Equal(VideoStreamType.Redirect, patch.Type);
            Assert.Equal(0, patch.DelayMinutes);
            Assert.Equal(0, patch.ArchiveDays);
            Assert.Equal(string.Empty, patch.RestreamUserAgent);
            Assert.Equal(string.Empty, patch.RestreamProxy);
            Assert.Equal(string.Empty, patch.RestreamCookies);
            Assert.Equal(string.Empty, patch.RestreamHeaders);
        }

        [Fact]
        public async Task ATypeChangeWarnsOnlyAboutTheSettingsThatWereActuallySet()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[id] = Row(id, delay: 5);
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeType,
                id,
                type: VideoStreamType.Redirect), TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.NotNull(item.Warnings);
            Assert.Equal(new[] { VideoStreamBulkWarning.DelayCleared }, item.Warnings);

            var patch = Assert.Single(streams.Updates).Patch;
            Assert.Equal(0, patch.DelayMinutes);
            Assert.Null(patch.ArchiveDays);
            Assert.Null(patch.RestreamUserAgent);
        }

        [Fact]
        public async Task ATypeChangeToOnDemandKeepsTheRestreamSettings()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[id] = Row(id, userAgent: "agent");
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeType,
                id,
                type: VideoStreamType.OnDemand), TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.Equal(VideoStreamAction.Updated, item.Action);
            Assert.Null(item.Warnings);

            var patch = Assert.Single(streams.Updates).Patch;
            Assert.Equal(VideoStreamType.OnDemand, patch.Type);
            Assert.Null(patch.DelayMinutes);
            Assert.Null(patch.ArchiveDays);
            Assert.Null(patch.RestreamUserAgent);
        }

        [Fact]
        public async Task ARedirectedRowReportsNoWarningsBecauseNothingWasWritten()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams { RefuseUpdates = true };
            streams.Rows[id] = Row(id, delay: 5, archive: 9);
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeType,
                id,
                type: VideoStreamType.Redirect), TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.Equal(VideoStreamAction.Invalid, item.Action);
            Assert.Null(item.Warnings);
        }

        [Fact]
        public async Task DelayAndArchiveAreRefusedOnKindsOtherThanPermanent()
        {
            var permanent = Guid.NewGuid();
            var redirect = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[permanent] = Row(permanent);
            streams.Rows[redirect] = Row(redirect, VideoStreamType.Redirect);
            var service = new VideoStreamBulkService(streams);

            var delay = await service.BulkAsync(new BulkVideoStreamOperation
            {
                Action = BulkVideoStreamAction.ChangeDelay,
                StreamIds = [permanent, redirect],
                DelayMinutes = 15
            }, TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Updated, delay.Items[0].Action);
            Assert.Equal(VideoStreamAction.Invalid, delay.Items[1].Action);
            Assert.True(delay.Items[1].Errors!.ContainsKey("DelayMinutes"));
            Assert.Single(streams.Updates);

            var archive = await service.BulkAsync(new BulkVideoStreamOperation
            {
                Action = BulkVideoStreamAction.ChangeArchive,
                StreamIds = [permanent, redirect],
                ArchiveDays = 30
            }, TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Updated, archive.Items[0].Action);
            Assert.Equal(VideoStreamAction.Invalid, archive.Items[1].Action);
            Assert.True(archive.Items[1].Errors!.ContainsKey("ArchiveDays"));
            Assert.Equal(2, streams.Updates.Count);
        }

        [Fact]
        public async Task RestreamIsRefusedForRedirectAndClearsWithBlankValues()
        {
            var permanent = Guid.NewGuid();
            var redirect = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[permanent] = Row(permanent);
            streams.Rows[redirect] = Row(redirect, VideoStreamType.Redirect);
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(new BulkVideoStreamOperation
            {
                Action = BulkVideoStreamAction.ChangeRestream,
                StreamIds = [permanent, redirect]
            }, TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Updated, outcome.Items[0].Action);
            Assert.Equal(VideoStreamAction.Invalid, outcome.Items[1].Action);
            Assert.True(outcome.Items[1].Errors!.ContainsKey("RestreamUserAgent"));

            var patch = Assert.Single(streams.Updates).Patch;
            Assert.Equal(string.Empty, patch.RestreamUserAgent);
            Assert.Equal(string.Empty, patch.RestreamProxy);
            Assert.Equal(string.Empty, patch.RestreamCookies);
            Assert.Equal(string.Empty, patch.RestreamHeaders);
        }

        [Fact]
        public async Task AMissingPayloadRefusesEveryRowWithoutReadingOne()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var streams = new FakeStreams();
            var service = new VideoStreamBulkService(streams);

            var noDelay = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeDelay,
                first,
                second), TestContext.Current.CancellationToken);

            Assert.All(noDelay.Items, item =>
            {
                Assert.Equal(VideoStreamAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey("DelayMinutes"));
            });

            var badType = await service.BulkAsync(Operation(
                BulkVideoStreamAction.ChangeType,
                first,
                type: VideoStreamType.OnlineSeries), TestContext.Current.CancellationToken);

            Assert.All(badType.Items, item =>
            {
                Assert.Equal(VideoStreamAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey("Type"));
            });

            var noCategory = await service.BulkAsync(Operation(
                BulkVideoStreamAction.MoveCategory,
                first), TestContext.Current.CancellationToken);

            Assert.All(noCategory.Items, item =>
            {
                Assert.Equal(VideoStreamAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey("CategoryId"));
            });

            Assert.Empty(streams.Reads);
            Assert.Empty(streams.Updates);
        }

        [Fact]
        public async Task ADelegatedActionReportsItsOwnOutcomePerRow()
        {
            var known = Guid.NewGuid();
            var missing = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[known] = Row(known);
            var service = new VideoStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.Restart,
                known,
                missing), TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Restarted, outcome.Items[0].Action);
            Assert.Equal(VideoStreamAction.NotFound, outcome.Items[1].Action);
            Assert.Equal(new[] { known }, streams.Restarted);
        }

        [Fact]
        public async Task ACategoryCollisionRefusesOnlyItsOwnRow()
        {
            var movable = Guid.NewGuid();
            var colliding = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[movable] = Row(movable);
            streams.Rows[colliding] = Row(colliding);
            streams.NameCollisions.Add(colliding);
            var service = new VideoStreamBulkService(streams);

            var category = Guid.NewGuid();
            var outcome = await service.BulkAsync(Operation(
                BulkVideoStreamAction.MoveCategory,
                movable,
                colliding,
                categoryId: category), TestContext.Current.CancellationToken);

            Assert.Equal(VideoStreamAction.Updated, outcome.Items[0].Action);
            Assert.Equal(VideoStreamAction.AlreadyExists, outcome.Items[1].Action);
            Assert.Equal(2, streams.Updates.Count);
            Assert.All(streams.Updates, update => Assert.Equal(category, update.Patch.CategoryId));
        }

        private static BulkVideoStreamOperation Operation(
            BulkVideoStreamAction action,
            Guid first,
            Guid? second = null,
            VideoStreamType? type = null,
            Guid? categoryId = null)
            => new()
            {
                Action = action,
                StreamIds = second is { } other ? [first, other] : [first],
                Type = type,
                CategoryId = categoryId
            };

        private static VideoStream Row(
            Guid id,
            VideoStreamType type = VideoStreamType.Permanent,
            int delay = 0,
            int archive = 0,
            string? userAgent = null)
            => new()
            {
                Id = id,
                Name = $"Stream {id:N}",
                Status = VideoStreamStatus.Stopped,
                Type = type,
                CategoryId = Guid.NewGuid(),
                CategoryName = "Live",
                DelayMinutes = delay,
                ArchiveDays = archive,
                SortOrder = 1,
                RestreamUserAgent = userAgent,
                Sources = ["https://example.test/source"],
                Files = []
            };

        /// <summary>
        /// The single-row service, recording what it was asked rather than carrying it out.
        /// </summary>
        private sealed class FakeStreams : IVideoStreamService
        {
            public Dictionary<Guid, VideoStream> Rows { get; } = [];

            public HashSet<Guid> NameCollisions { get; } = [];

            public List<Guid> Reads { get; } = [];

            public List<(Guid Id, VideoStreamPatch Patch)> Updates { get; } = [];

            public List<Guid> Restarted { get; } = [];

            public bool RefuseUpdates { get; init; }

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

            public Task<VideoStream?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                Reads.Add(id);

                return Task.FromResult(Rows.TryGetValue(id, out var row) ? row : null);
            }

            public Task<VideoStreamOutcome> CreateAsync(
                NewVideoStream input,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<VideoStreamOutcome> UpdateAsync(
                Guid id,
                VideoStreamPatch patch,
                CancellationToken cancellationToken = default)
            {
                Updates.Add((id, patch));

                if (RefuseUpdates)
                {
                    return Task.FromResult(new VideoStreamOutcome
                    {
                        Action = VideoStreamAction.Invalid,
                        StreamId = id,
                        Errors = new Dictionary<string, string[]> { ["Type"] = ["refused"] }
                    });
                }

                return Task.FromResult(new VideoStreamOutcome
                {
                    Action = NameCollisions.Contains(id)
                        ? VideoStreamAction.AlreadyExists
                        : VideoStreamAction.Updated,
                    StreamId = id
                });
            }

            public Task<VideoStreamOutcome> DeleteAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, VideoStreamAction.Removed));

            public Task<VideoStreamOutcome> StartAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, VideoStreamAction.Started));

            public Task<VideoStreamOutcome> StopAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, VideoStreamAction.Stopped));

            public Task<VideoStreamOutcome> RestartAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                if (Rows.ContainsKey(id))
                {
                    Restarted.Add(id);
                }

                return Task.FromResult(Transition(id, VideoStreamAction.Restarted));
            }

            private VideoStreamOutcome Transition(Guid id, VideoStreamAction action)
                => new()
                {
                    Action = Rows.ContainsKey(id) ? action : VideoStreamAction.NotFound,
                    StreamId = id
                };
        }
    }
}
