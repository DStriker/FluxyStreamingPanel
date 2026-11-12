using Fluxy.Application.Content.Services;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Outcomes;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Content.Rows;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Content
{
    /// <summary>
    /// The rules of a mass source replacement that a database cannot state: the ordinal
    /// replacement, the per-entity refusal precedence, and the single commit.
    /// </summary>
    /// <remarks>
    /// The integration suite covers what the tables hold afterwards. What it cannot see is the
    /// save count - one run is one commit - so that claim is pinned here, against a fake
    /// repository that records the intents it was handed.
    /// </remarks>
    public sealed class SourceReplacementServiceTests
    {
        [Fact]
        public async Task TheSearchIsOrdinalAndCaseSensitiveAndReplacesEveryOccurrence()
        {
            var changed = Guid.NewGuid();
            var skipped = Guid.NewGuid();

            var repository = new FakeSources
            {
                Streams =
                [
                    Row(changed, "News", 0, "http://Host.test/a/Host.test"),
                    // A candidate the database's collation may admit but the ordinal rule
                    // leaves alone: nothing on this line is capitalised "Host".
                    Row(skipped, "Kept", 0, "http://host.test/host")
                ]
            };

            var outcome = await Build(repository).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "Host", Replace = "Other" },
                TestContext.Current.CancellationToken);

            Assert.True(outcome.Accepted);
            Assert.Equal(1, outcome.Updated);
            Assert.Equal(1, outcome.Skipped);

            var staged = Assert.Single(repository.StreamUpdates);
            Assert.Equal(changed, staged.StreamId);
            Assert.Equal("http://Other.test/a/Other.test", staged.Urls.Values.Single());
        }

        [Fact]
        public async Task ABlankSearchStagesNothingAndReportsTheField()
        {
            var repository = new FakeSources
            {
                Streams = [Row(Guid.NewGuid(), "News", 0, "http://old.test/live")]
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(repository, unitOfWork).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "   ", Replace = "new" },
                TestContext.Current.CancellationToken);

            Assert.False(outcome.Accepted);
            Assert.True(outcome.Errors!.ContainsKey(nameof(ReplaceSourcesOperation.Find)));
            Assert.False(outcome.Errors.ContainsKey(nameof(ReplaceSourcesOperation.Replace)));
            Assert.Equal(0, unitOfWork.Saves);
            Assert.Empty(repository.StreamUpdates);
        }

        [Fact]
        public async Task ASearchEqualToItsReplacementReportsBothFieldsWhenBothAreBlank()
        {
            var repository = new FakeSources();
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(repository, unitOfWork).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "", Replace = "" },
                TestContext.Current.CancellationToken);

            Assert.False(outcome.Accepted);
            Assert.True(outcome.Errors!.ContainsKey(nameof(ReplaceSourcesOperation.Find)));
            Assert.True(outcome.Errors.ContainsKey(nameof(ReplaceSourcesOperation.Replace)));
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task AReplacementEqualToTheSearchIsRefusedOnTheReplacementField()
        {
            var repository = new FakeSources();
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(repository, unitOfWork).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "old.test", Replace = "old.test" },
                TestContext.Current.CancellationToken);

            Assert.False(outcome.Accepted);
            Assert.False(outcome.Errors!.ContainsKey(nameof(ReplaceSourcesOperation.Find)));
            Assert.True(outcome.Errors.ContainsKey(nameof(ReplaceSourcesOperation.Replace)));
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task ABlankResultRefusesTheWholeEntityAndNeverStagesAnyOfItsPositions()
        {
            var refused = Guid.NewGuid();
            var kept = Guid.NewGuid();

            var repository = new FakeSources
            {
                Streams =
                [
                    Row(refused, "Refused", 0, "placeholder"),
                    Row(refused, "Refused", 1, "http://a.test/placeholder"),
                    Row(kept, "Kept", 0, "http://b.test/placeholder")
                ]
            };

            var outcome = await Build(repository).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "placeholder", Replace = "" },
                TestContext.Current.CancellationToken);

            var refusal = Assert.Single(outcome.Refusals);
            Assert.Equal(SourceDomain.VideoStreams, refusal.Domain);
            Assert.Equal(refused, refusal.Id);
            Assert.Equal(SourceRefusalReason.Blank, refusal.Reason);

            // All or nothing: the refused stream's second position would have been usable, and
            // it is not written either. The other stream still is.
            var staged = Assert.Single(repository.StreamUpdates);
            Assert.Equal(kept, staged.StreamId);
            Assert.Equal("http://b.test/", staged.Urls[0]);
        }

        [Fact]
        public async Task AnOverLongResultRefusesTheEntityAndAnotherEntityStillChanges()
        {
            var refused = Guid.NewGuid();
            var kept = Guid.NewGuid();
            var longUrl = "http://a.test/" + new string('z', 1983) + "bad";

            var repository = new FakeSources
            {
                Streams =
                [
                    Row(refused, "Long", 0, longUrl),
                    Row(kept, "Short", 0, "http://b.test/bad")
                ]
            };

            var outcome = await Build(repository).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "bad", Replace = "badbadxx" },
                TestContext.Current.CancellationToken);

            var refusal = Assert.Single(outcome.Refusals);
            Assert.Equal(SourceRefusalReason.TooLong, refusal.Reason);

            var staged = Assert.Single(repository.StreamUpdates);
            Assert.Equal(kept, staged.StreamId);
        }

        [Fact]
        public async Task ABlankResultWinsOverAnOverLongOneWhenOneEntityProducesBoth()
        {
            var refused = Guid.NewGuid();
            var kept = Guid.NewGuid();

            // A replacement of three spaces is neither blank itself nor equal to the search, so
            // the request is valid. On this one stream it does both forbidden things at once:
            // the first address becomes whitespace only, and the second - exactly as wide as the
            // column - grows two characters past VideoStreamLimits.SourceUrlMaxLength. The two
            // rules therefore meet on one entity, which is the only place their order shows.
            var prefix = "http://a.test/";
            var stretched =
                prefix +
                new string('z', VideoStreamLimits.SourceUrlMaxLength - prefix.Length - 1) +
                "X";

            Assert.Equal(VideoStreamLimits.SourceUrlMaxLength, stretched.Length);

            var repository = new FakeSources
            {
                Streams =
                [
                    Row(refused, "Both", 0, "X"),
                    Row(refused, "Both", 1, stretched),
                    Row(kept, "Kept", 0, "http://b.test/X")
                ]
            };

            var outcome = await Build(repository).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "X", Replace = "   " },
                TestContext.Current.CancellationToken);

            // Blank is the more fundamental answer and is the one reported, even though the
            // other position of the same entity is also too long.
            var refusal = Assert.Single(outcome.Refusals);
            Assert.Equal(SourceDomain.VideoStreams, refusal.Domain);
            Assert.Equal(refused, refusal.Id);
            Assert.Equal(SourceRefusalReason.Blank, refusal.Reason);

            // The refused entity staged nothing; the other stream is unaffected by its refusal.
            var staged = Assert.Single(repository.StreamUpdates);
            Assert.Equal(kept, staged.StreamId);
            Assert.Equal("http://b.test/   ", staged.Urls[0]);
        }

        [Fact]
        public async Task OneRunCommitsExactlyOnce()
        {
            var repository = new FakeSources
            {
                Streams =
                [
                    Row(Guid.NewGuid(), "News", 0, "http://old.test/live"),
                    Row(Guid.NewGuid(), "Radio", 0, "http://old.test/audio")
                ],
                Vod = [new StoredVodSourceRow
                {
                    Id = Guid.NewGuid(),
                    Name = "Film",
                    Source = "http://old.test/film.mp4"
                }],
                Episodes = [new StoredEpisodeSourceRow
                {
                    Id = Guid.NewGuid(),
                    Name = "First",
                    Source = "http://old.test/show.mp4"
                }]
            };
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(repository, unitOfWork).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "old.test", Replace = "new.test" },
                TestContext.Current.CancellationToken);

            Assert.True(outcome.Accepted);
            Assert.Equal(4, outcome.Updated);
            Assert.Equal(1, unitOfWork.Saves);
            Assert.Equal(2, repository.StreamUpdates.Count);
            Assert.Single(repository.VodUpdates);
            Assert.Single(repository.EpisodeUpdates);
        }

        [Fact]
        public async Task AValidRunThatMatchesNothingIsAcceptedWithFourZeroCounts()
        {
            var repository = new FakeSources();
            var unitOfWork = new FakeUnitOfWork();

            var outcome = await Build(repository, unitOfWork).ReplaceAsync(
                new ReplaceSourcesOperation { Find = "nothing", Replace = "x" },
                TestContext.Current.CancellationToken);

            Assert.True(outcome.Accepted);
            Assert.Empty(outcome.Refusals);
            Assert.Equal(4, outcome.Domains.Count);
            Assert.Equal(
                new[]
                {
                    SourceDomain.VideoStreams,
                    SourceDomain.AudioStreams,
                    SourceDomain.VideoOnDemand,
                    SourceDomain.SeriesEpisodes
                },
                outcome.Domains.Select(count => count.Domain).ToArray());
            Assert.All(outcome.Domains, count => Assert.Equal(0, count.Updated));
            Assert.Equal(0, outcome.Skipped);
            Assert.Equal(1, unitOfWork.Saves);
        }

        private static SourceReplacementService Build(
            FakeSources sources,
            FakeUnitOfWork? unitOfWork = null)
            => new(sources, unitOfWork ?? new FakeUnitOfWork());

        private static StoredStreamSourceRow Row(
            Guid streamId,
            string name,
            int position,
            string url)
            => new()
            {
                StreamId = streamId,
                StreamName = name,
                Position = position,
                Url = url
            };

        /// <summary>The repository, recording every set intent and serving the planted candidates.</summary>
        private sealed class FakeSources : ISourceReplacementRepository
        {
            public IReadOnlyList<StoredStreamSourceRow> Streams { get; init; } = [];

            public IReadOnlyList<StoredAudioSourceRow> Audio { get; init; } = [];

            public IReadOnlyList<StoredVodSourceRow> Vod { get; init; } = [];

            public IReadOnlyList<StoredEpisodeSourceRow> Episodes { get; init; } = [];

            public List<StagedStream> StreamUpdates { get; } = [];

            public List<StagedStream> AudioUpdates { get; } = [];

            public List<string> VodUpdates { get; } = [];

            public List<string> EpisodeUpdates { get; } = [];

            public Task<IReadOnlyList<StoredStreamSourceRow>> GetStreamSourceCandidatesAsync(
                string find,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Streams);

            public Task<IReadOnlyList<StoredAudioSourceRow>> GetAudioSourceCandidatesAsync(
                string find,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Audio);

            public Task<IReadOnlyList<StoredVodSourceRow>> GetVodSourceCandidatesAsync(
                string find,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Vod);

            public Task<IReadOnlyList<StoredEpisodeSourceRow>> GetEpisodeSourceCandidatesAsync(
                string find,
                CancellationToken cancellationToken = default)
                => Task.FromResult(Episodes);

            public Task SetStreamSourcesAsync(
                Guid streamId,
                IReadOnlyDictionary<int, string> urlsByPosition,
                CancellationToken cancellationToken = default)
            {
                StreamUpdates.Add(new StagedStream(streamId, urlsByPosition));
                return Task.CompletedTask;
            }

            public Task SetAudioSourcesAsync(
                Guid streamId,
                IReadOnlyDictionary<int, string> urlsByPosition,
                CancellationToken cancellationToken = default)
            {
                AudioUpdates.Add(new StagedStream(streamId, urlsByPosition));
                return Task.CompletedTask;
            }

            public Task SetVodSourceAsync(
                Guid id,
                string source,
                CancellationToken cancellationToken = default)
            {
                VodUpdates.Add(source);
                return Task.CompletedTask;
            }

            public Task SetEpisodeSourceAsync(
                Guid id,
                string source,
                CancellationToken cancellationToken = default)
            {
                EpisodeUpdates.Add(source);
                return Task.CompletedTask;
            }
        }

        /// <summary>One staged stream's positions, as the fake recorded them.</summary>
        private sealed record StagedStream(
            Guid StreamId,
            IReadOnlyDictionary<int, string> Urls);

        /// <summary>The unit of work, counting the commits the service asks for.</summary>
        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Saves { get; private set; }

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                Saves += 1;

                return Task.CompletedTask;
            }
        }
    }
}
