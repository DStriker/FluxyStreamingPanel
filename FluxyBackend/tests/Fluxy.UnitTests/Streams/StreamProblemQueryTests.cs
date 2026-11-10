using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    /// <summary>
    /// The query a stream problem read resolves into: its defaults, the stream filter, the two date
    /// bounds, the search bound, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: an absent value
    /// defaults, present text that names nothing is refused, and a blank value inside a filter is
    /// "no filter" rather than a refusal. A test that only checked a valid query would pass over an
    /// implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class StreamProblemQueryTests
    {
        private static readonly string Stream = Guid.NewGuid().ToString();

        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static StreamProblemQuery Parse(
            int? page = null,
            int? pageSize = null,
            string? search = null,
            string? streamId = null,
            string? dateFrom = null,
            string? dateTo = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = StreamProblemQuery.TryParse(
                page,
                pageSize,
                search,
                streamId,
                dateFrom,
                dateTo,
                sortBy,
                sortOrder,
                out var errors);

            Assert.Empty(errors);
            Assert.NotNull(query);

            return query!;
        }

        [Fact]
        public void AnAbsentQueryStartsFromCreatedAtDescending()
        {
            var query = Parse();

            Assert.Equal(1, query.Page);
            Assert.Equal(StreamProblemListLimits.DefaultPageSize, query.PageSize);
            Assert.Null(query.Search);
            Assert.Null(query.StreamId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(StreamProblemSortField.CreatedAt, query.SortBy);
            Assert.Equal(StreamProblemSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                search: " ",
                streamId: "",
                dateFrom: " ",
                dateTo: "",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.Search);
            Assert.Null(query.StreamId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(StreamProblemSortField.CreatedAt, query.SortBy);
            Assert.Equal(StreamProblemSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var query = Parse(
                page: 3,
                pageSize: 50,
                search: " timeout ",
                streamId: $" {Stream} ",
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z",
                sortBy: "createdAt",
                sortOrder: "asc");

            Assert.Equal(3, query.Page);
            Assert.Equal(50, query.PageSize);
            Assert.Equal("timeout", query.Search);
            Assert.Equal(Guid.Parse(Stream), query.StreamId);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
            Assert.Equal(StreamProblemSortField.CreatedAt, query.SortBy);
            Assert.Equal(StreamProblemSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("createdAt")]
        [InlineData("CREATEDAT")]
        [InlineData(" createdAt ")]
        public void TheSortFieldIsReadCaseInsensitively(string value)
        {
            Assert.Equal(StreamProblemSortField.CreatedAt, Parse(sortBy: value).SortBy);
        }

        [Theory]
        [InlineData("asc", StreamProblemSortOrder.Ascending)]
        [InlineData("ASC", StreamProblemSortOrder.Ascending)]
        [InlineData("desc", StreamProblemSortOrder.Descending)]
        [InlineData(" Desc ", StreamProblemSortOrder.Descending)]
        public void TheSortOrderIsReadCaseInsensitively(
            string value,
            StreamProblemSortOrder expected)
        {
            Assert.Equal(expected, Parse(sortOrder: value).SortOrder);
        }

        [Theory]
        [InlineData("message", "sortBy")]
        [InlineData("streamName", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = StreamProblemQuery.TryParse(
                1,
                StreamProblemListLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                parameter == "sortBy" ? value : null,
                parameter == "sortOrder" ? value : null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void PageAndPageSizeAreRefusedRatherThanClamped()
        {
            var badPage = StreamProblemQuery.TryParse(
                0,
                StreamProblemListLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                null,
                null,
                out var pageErrors);

            Assert.Null(badPage);
            Assert.True(pageErrors.ContainsKey("page"));

            var zeroSize = StreamProblemQuery.TryParse(
                1,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                out var zeroErrors);

            Assert.Null(zeroSize);
            Assert.True(zeroErrors.ContainsKey("pageSize"));

            var hugeSize = StreamProblemQuery.TryParse(
                1,
                StreamProblemListLimits.MaxPageSize + 1,
                null,
                null,
                null,
                null,
                null,
                null,
                out var hugeErrors);

            Assert.Null(hugeSize);
            Assert.True(hugeErrors.ContainsKey("pageSize"));

            // At the ceiling it is accepted: the bound is the maximum itself, not one below it.
            Assert.Equal(
                StreamProblemListLimits.MaxPageSize,
                Parse(pageSize: StreamProblemListLimits.MaxPageSize).PageSize);
        }

        [Fact]
        public void ASearchAboveTheCeilingIsRefusedUnderSearch()
        {
            var query = StreamProblemQuery.TryParse(
                1,
                StreamProblemListLimits.DefaultPageSize,
                new string('a', StreamProblemListLimits.MaxSearchLength + 1),
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("search"));

            // The ceiling itself is accepted, and the value is trimmed before its length is judged.
            Assert.Equal(
                StreamProblemListLimits.MaxSearchLength,
                Parse(search: new string('a', StreamProblemListLimits.MaxSearchLength))
                    .Search!
                    .Length);

            Assert.Equal(
                "timeout",
                Parse(search: "  timeout  ").Search);
        }

        [Theory]
        [InlineData("not-a-uuid")]
        [InlineData("42")]
        [InlineData("{not a uuid}")]
        public void AStreamFilterThatNamesNoUuidIsRefusedUnderStreamId(string value)
        {
            var query = StreamProblemQuery.TryParse(
                1,
                StreamProblemListLimits.DefaultPageSize,
                null,
                value,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("streamId"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void AUuidStreamFilterIsAccepted()
        {
            Assert.Equal(Guid.Parse(Stream), Parse(streamId: Stream).StreamId);
        }

        [Theory]
        [InlineData("dateFrom")]
        [InlineData("dateTo")]
        public void AnUnparseableBoundIsRefusedUnderItsOwnParameter(string parameter)
        {
            // A value that parses as no instant is a refusal under its own name - never read as
            // "no bound", because that would answer a list wider than the caller asked for. The
            // locale-dependent spelling is refused as well; only ISO 8601 names one instant.
            var query = StreamProblemQuery.TryParse(
                1,
                StreamProblemListLimits.DefaultPageSize,
                null,
                null,
                parameter == "dateFrom" ? "yesterday" : null,
                parameter == "dateTo" ? "05/01/2026" : null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void TheDateRangeIsTwoIndependentInclusiveBounds()
        {
            Assert.Null(Parse().DateFrom);
            Assert.Null(Parse().DateTo);

            var onlyFrom = Parse(dateFrom: "2026-05-01");
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), onlyFrom.DateFrom);
            Assert.Null(onlyFrom.DateTo);

            var onlyTo = Parse(dateTo: "2026-05-31");
            Assert.Null(onlyTo.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero), onlyTo.DateTo);

            // A date-time with an offset is honoured and normalized to UTC: the caller meant that
            // instant, not the server's locale.
            Assert.Equal(
                new DateTimeOffset(2026, 5, 1, 7, 0, 0, TimeSpan.Zero),
                Parse(dateFrom: "2026-05-01T10:00:00+03:00").DateFrom);
        }

        [Fact]
        public void AnAbsentOffsetMeansUtc()
        {
            Assert.Equal(
                new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
                Parse(dateFrom: "2026-05-01T10:00:00").DateFrom);

            Assert.Equal(
                new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
                Parse(dateTo: "2026-05-01t10:00:00z").DateTo);
        }

        [Fact]
        public void AnInvertedRangeIsAValidQueryDescribingAnEmptyList()
        {
            // A later `from` than `to` is not a broken request: it names an interval with nothing
            // in it, and the answer is 200 with an empty page. Refusing it would turn a valid
            // question into an error, and swapping the bounds would answer a different question.
            var query = Parse(dateFrom: "2026-06-01", dateTo: "2026-05-01");

            Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateTo);
        }

        [Fact]
        public void TheFiltersCompose()
        {
            var query = Parse(
                search: "refused",
                streamId: Stream,
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z");

            Assert.Equal("refused", query.Search);
            Assert.Equal(Guid.Parse(Stream), query.StreamId);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
        }

        [Fact]
        public void EveryBrokenParameterIsReportedAtOnce()
        {
            var query = StreamProblemQuery.TryParse(
                0,
                StreamProblemListLimits.MaxPageSize + 1,
                new string('a', StreamProblemListLimits.MaxSearchLength + 1),
                "not-a-uuid",
                "yesterday",
                "tomorrow",
                "message",
                "sideways",
                out var errors);

            Assert.Null(query);
            Assert.Equal(
                [
                    "dateFrom",
                    "dateTo",
                    "page",
                    "pageSize",
                    "search",
                    "sortBy",
                    "sortOrder",
                    "streamId"
                ],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }
}
