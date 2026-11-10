using Fluxy.Application.Streams.Services;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.ReadModels;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The write path of the stream problem journal: the trim, the blank refusal, the truncation
    /// and the commit the service owns.
    /// </summary>
    /// <remarks>
    /// Unlike the user audit log, a failure to write here must not be swallowed: the row is the
    /// whole operation. The test that makes the unit of work throw is what pins that difference.
    /// </remarks>
    public sealed class StreamProblemServiceTests
    {
        private static readonly Guid Stream = Guid.NewGuid();

        private static StreamProblemService Build(
            FakeStreamProblems problems,
            FakeUnitOfWork unitOfWork)
            => new(problems, unitOfWork);

        [Fact]
        public async Task ARecordStagesTheRowAndCommitsItOnce()
        {
            var problems = new FakeStreamProblems();
            var unitOfWork = new FakeUnitOfWork();

            await Build(problems, unitOfWork).RecordAsync(
                new NewStreamProblem { StreamId = Stream, ErrorMessage = "The source timed out." },
                TestContext.Current.CancellationToken);

            Assert.NotNull(problems.Added);
            Assert.Equal(Stream, problems.Added!.StreamId);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task TheMessageIsTrimmedAtItsEdges()
        {
            var problems = new FakeStreamProblems();

            await Build(problems, new FakeUnitOfWork()).RecordAsync(
                new NewStreamProblem { StreamId = Stream, ErrorMessage = "   reference refused   " },
                TestContext.Current.CancellationToken);

            Assert.Equal("reference refused", problems.Added!.ErrorMessage);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public async Task ABlankMessageIsRefusedRatherThanStored(string message)
        {
            var problems = new FakeStreamProblems();
            var unitOfWork = new FakeUnitOfWork();

            await Assert.ThrowsAsync<ArgumentException>(
                () => Build(problems, unitOfWork).RecordAsync(
                    new NewStreamProblem { StreamId = Stream, ErrorMessage = message },
                    TestContext.Current.CancellationToken));

            // Nothing was staged and nothing was committed: a refused record leaves no trace.
            Assert.Null(problems.Added);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task AMessageLongerThanTheColumnIsCutRatherThanRefused()
        {
            var problems = new FakeStreamProblems();

            await Build(problems, new FakeUnitOfWork()).RecordAsync(
                new NewStreamProblem
                {
                    StreamId = Stream,
                    ErrorMessage = new string(
                        'm',
                        StreamProblemListLimits.ErrorMessageMaxLength + 50)
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(
                StreamProblemListLimits.ErrorMessageMaxLength,
                problems.Added!.ErrorMessage.Length);
        }

        [Fact]
        public async Task AMessageAtTheColumnsWidthIsStoredUnchanged()
        {
            var problems = new FakeStreamProblems();
            var message = new string('m', StreamProblemListLimits.ErrorMessageMaxLength);

            await Build(problems, new FakeUnitOfWork()).RecordAsync(
                new NewStreamProblem { StreamId = Stream, ErrorMessage = message },
                TestContext.Current.CancellationToken);

            Assert.Equal(message, problems.Added!.ErrorMessage);
        }

        [Fact]
        public async Task AFailedCommitIsNotSwallowed()
        {
            var problems = new FakeStreamProblems();
            var unitOfWork = new FakeUnitOfWork(new InvalidOperationException("journal gone"));

            // The row is the whole operation here, so the caller has to learn that it did not
            // happen; retry belongs to the producer, not to this service.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Build(problems, unitOfWork).RecordAsync(
                    new NewStreamProblem { StreamId = Stream, ErrorMessage = "Timeout." },
                    TestContext.Current.CancellationToken));

            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task ClearAllReportsTheDeletedCount()
        {
            var problems = new FakeStreamProblems { Cleared = 7 };

            var removed = await Build(problems, new FakeUnitOfWork())
                .ClearAllAsync(TestContext.Current.CancellationToken);

            Assert.Equal(7, removed);
        }

        /// <summary>The repository, recording the row the service staged.</summary>
        private sealed class FakeStreamProblems : IStreamProblemRepository
        {
            public NewStreamProblem? Added { get; private set; }

            /// <summary>How many rows <see cref="ClearAllAsync"/> reports as deleted.</summary>
            public long Cleared { get; init; }

            public Task<StreamProblemPage> GetPageAsync(
                int page,
                int pageSize,
                string? search,
                Guid? streamId,
                DateTimeOffset? dateFrom,
                DateTimeOffset? dateTo,
                StreamProblemSortField sortBy,
                StreamProblemSortOrder sortOrder,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException("The write path never reads a page.");

            public Task AddAsync(
                NewStreamProblem problem,
                CancellationToken cancellationToken = default)
            {
                Added = problem;

                return Task.CompletedTask;
            }

            public Task<long> ClearAllAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(Cleared);

            public Task<int> CountAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException("The write path never counts the table.");
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
