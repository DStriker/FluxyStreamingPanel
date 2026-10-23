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
    /// The rules that belong to a bulk operation over audio streams rather than to one row:
    /// which kinds a bulk type change may write, what a restream change writes on a kind that
    /// accepts it, and what a missing payload does to the whole run.
    /// </summary>
    /// <remarks>
    /// The service is exercised against a fake <see cref="IAudioStreamService"/> rather than a
    /// database. Every rule in <see cref="AudioStreamBulkService"/> is a decision about what to
    /// ask the single-row service for - the delegation itself is covered by the integration
    /// tests - so the fake records those requests instead of carrying them out.
    /// </remarks>
    public sealed class AudioStreamBulkServiceTests
    {
        [Fact]
        public async Task ATypeChangeWritesOnlyTheTypeAndClearsNothing()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[id] = Row(id, userAgent: "agent");
            var service = new AudioStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkAudioStreamAction.ChangeType,
                id,
                type: AudioStreamType.Redirect), TestContext.Current.CancellationToken);

            var item = Assert.Single(outcome.Items);
            Assert.Equal(AudioStreamAction.Updated, item.Action);

            var patch = Assert.Single(streams.Updates).Patch;
            Assert.Equal(AudioStreamType.Redirect, patch.Type);
            Assert.Null(patch.RestreamUserAgent);
            Assert.Null(patch.RestreamProxy);
            Assert.Null(patch.RestreamCookies);
            Assert.Null(patch.RestreamHeaders);
        }

        [Fact]
        public async Task ATypeChangeIsAcceptedForBothAudioKinds()
        {
            var id = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[id] = Row(id);
            var service = new AudioStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkAudioStreamAction.ChangeType,
                id,
                type: AudioStreamType.OnDemand), TestContext.Current.CancellationToken);

            Assert.Equal(AudioStreamAction.Updated, Assert.Single(outcome.Items).Action);
            Assert.Equal(AudioStreamType.OnDemand, Assert.Single(streams.Updates).Patch.Type);
        }

        [Fact]
        public async Task RestreamIsAcceptedForEveryKindAndWritesAllFourSettings()
        {
            var onDemand = Guid.NewGuid();
            var redirect = Guid.NewGuid();
            var streams = new FakeStreams();
            streams.Rows[onDemand] = Row(onDemand);
            streams.Rows[redirect] = Row(redirect, AudioStreamType.Redirect);
            var service = new AudioStreamBulkService(streams);

            var outcome = await service.BulkAsync(new BulkAudioStreamOperation
            {
                Action = BulkAudioStreamAction.ChangeRestream,
                StreamIds = [onDemand, redirect]
            }, TestContext.Current.CancellationToken);

            Assert.All(outcome.Items, item => Assert.Equal(AudioStreamAction.Updated, item.Action));
            Assert.Equal(2, streams.Updates.Count);

            var patch = streams.Updates[0].Patch;
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
            var service = new AudioStreamBulkService(streams);

            var noCategory = await service.BulkAsync(Operation(
                BulkAudioStreamAction.MoveCategory,
                first,
                second), TestContext.Current.CancellationToken);

            Assert.All(noCategory.Items, item =>
            {
                Assert.Equal(AudioStreamAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey("CategoryId"));
            });

            var badType = await service.BulkAsync(Operation(
                BulkAudioStreamAction.ChangeType,
                first,
                second,
                type: (AudioStreamType)99), TestContext.Current.CancellationToken);

            Assert.All(badType.Items, item =>
            {
                Assert.Equal(AudioStreamAction.Invalid, item.Action);
                Assert.True(item.Errors!.ContainsKey("Type"));
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
            var service = new AudioStreamBulkService(streams);

            var outcome = await service.BulkAsync(Operation(
                BulkAudioStreamAction.Restart,
                known,
                missing), TestContext.Current.CancellationToken);

            Assert.Equal(AudioStreamAction.Restarted, outcome.Items[0].Action);
            Assert.Equal(AudioStreamAction.NotFound, outcome.Items[1].Action);
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
            var service = new AudioStreamBulkService(streams);

            var category = Guid.NewGuid();
            var outcome = await service.BulkAsync(Operation(
                BulkAudioStreamAction.MoveCategory,
                movable,
                colliding,
                categoryId: category), TestContext.Current.CancellationToken);

            Assert.Equal(AudioStreamAction.Updated, outcome.Items[0].Action);
            Assert.Equal(AudioStreamAction.AlreadyExists, outcome.Items[1].Action);
            Assert.Equal(2, streams.Updates.Count);
            Assert.All(streams.Updates, update => Assert.Equal(category, update.Patch.CategoryId));
        }

        private static BulkAudioStreamOperation Operation(
            BulkAudioStreamAction action,
            Guid first,
            Guid? second = null,
            AudioStreamType? type = null,
            Guid? categoryId = null)
            => new()
            {
                Action = action,
                StreamIds = second is { } other ? [first, other] : [first],
                Type = type,
                CategoryId = categoryId
            };

        private static AudioStream Row(
            Guid id,
            AudioStreamType type = AudioStreamType.OnDemand,
            string? userAgent = null)
            => new()
            {
                Id = id,
                Name = $"Stream {id:N}",
                Status = AudioStreamStatus.Stopped,
                Type = type,
                CategoryId = Guid.NewGuid(),
                CategoryName = "Radio",
                RestreamUserAgent = userAgent,
                Sources = ["https://example.test/source"]
            };

        /// <summary>
        /// The single-row service, recording what it was asked rather than carrying it out.
        /// </summary>
        private sealed class FakeStreams : IAudioStreamService
        {
            public Dictionary<Guid, AudioStream> Rows { get; } = [];

            public HashSet<Guid> NameCollisions { get; } = [];

            public List<Guid> Reads { get; } = [];

            public List<(Guid Id, AudioStreamPatch Patch)> Updates { get; } = [];

            public List<Guid> Restarted { get; } = [];

            public Task<AudioStreamPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? categoryId,
                AudioStreamStatus? status,
                AudioStreamType? type,
                AudioStreamSortField sortBy,
                AudioStreamSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AudioStream?> GetAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                Reads.Add(id);

                return Task.FromResult(Rows.TryGetValue(id, out var row) ? row : null);
            }

            public Task<AudioStreamOutcome> CreateAsync(
                NewAudioStream input,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<AudioStreamOutcome> UpdateAsync(
                Guid id,
                AudioStreamPatch patch,
                CancellationToken cancellationToken = default)
            {
                Updates.Add((id, patch));

                return Task.FromResult(new AudioStreamOutcome
                {
                    Action = NameCollisions.Contains(id)
                        ? AudioStreamAction.AlreadyExists
                        : AudioStreamAction.Updated,
                    StreamId = id
                });
            }

            public Task<AudioStreamOutcome> DeleteAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, AudioStreamAction.Removed));

            public Task<AudioStreamOutcome> StartAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, AudioStreamAction.Started));

            public Task<AudioStreamOutcome> StopAsync(
                Guid id,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Transition(id, AudioStreamAction.Stopped));

            public Task<AudioStreamOutcome> RestartAsync(
                Guid id,
                CancellationToken cancellationToken = default)
            {
                if (Rows.ContainsKey(id))
                {
                    Restarted.Add(id);
                }

                return Task.FromResult(Transition(id, AudioStreamAction.Restarted));
            }

            private AudioStreamOutcome Transition(Guid id, AudioStreamAction action)
                => new()
                {
                    Action = Rows.ContainsKey(id) ? action : AudioStreamAction.NotFound,
                    StreamId = id
                };
        }
    }
}
