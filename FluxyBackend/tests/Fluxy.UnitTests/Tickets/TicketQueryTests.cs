using Fluxy.Core.Tickets.Enums;
using Fluxy.Core.Tickets.Utils;

namespace Fluxy.UnitTests.Tickets
{
    /// <summary>
    /// The query a support ticket read resolves into: its defaults, the viewer-relative status
    /// filter, the search bound, the sort choices, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: an absent value
    /// defaults, present text that names nothing is refused, and a blank value is "no filter"
    /// rather than a refusal. A test that only checked a valid query would pass over an
    /// implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class TicketQueryTests
    {
        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static TicketQuery Parse(
            int? page = null,
            int? pageSize = null,
            string? search = null,
            string? status = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = TicketQuery.TryParse(
                page,
                pageSize,
                search,
                status,
                sortBy,
                sortOrder,
                out var errors);

            Assert.Empty(errors);
            Assert.NotNull(query);

            return query!;
        }

        [Fact]
        public void AnAbsentQueryStartsFromLastReplyAtDescending()
        {
            var query = Parse();

            Assert.Equal(1, query.Page);
            Assert.Equal(TicketLimits.DefaultPageSize, query.PageSize);
            Assert.Null(query.Search);
            Assert.Null(query.Status);
            Assert.Equal(TicketSortField.LastReplyAt, query.SortBy);
            Assert.Equal(TicketSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                search: " ",
                status: "",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.Search);
            Assert.Null(query.Status);
            Assert.Equal(TicketSortField.LastReplyAt, query.SortBy);
            Assert.Equal(TicketSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var query = Parse(
                page: 3,
                pageSize: 50,
                search: " router ",
                status: "UNREAD",
                sortBy: "subject",
                sortOrder: "asc");

            Assert.Equal(3, query.Page);
            Assert.Equal(50, query.PageSize);
            Assert.Equal("router", query.Search);
            Assert.Equal(TicketStatuses.Unread, query.Status);
            Assert.Equal(TicketSortField.Subject, query.SortBy);
            Assert.Equal(TicketSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("subject", TicketSortField.Subject)]
        [InlineData("SUBJECT", TicketSortField.Subject)]
        [InlineData(" username ", TicketSortField.Username)]
        [InlineData("createdAt", TicketSortField.CreatedAt)]
        [InlineData("CREATEDAT", TicketSortField.CreatedAt)]
        [InlineData("lastReplyAt", TicketSortField.LastReplyAt)]
        [InlineData("LASTREPLYAT", TicketSortField.LastReplyAt)]
        public void TheSortFieldIsReadCaseInsensitively(string value, TicketSortField expected)
        {
            Assert.Equal(expected, Parse(sortBy: value).SortBy);
        }

        [Theory]
        [InlineData("asc", TicketSortOrder.Ascending)]
        [InlineData("DESC", TicketSortOrder.Descending)]
        [InlineData(" Desc ", TicketSortOrder.Descending)]
        public void TheSortOrderIsReadCaseInsensitively(string value, TicketSortOrder expected)
        {
            Assert.Equal(expected, Parse(sortOrder: value).SortOrder);
        }

        [Theory]
        [InlineData("unread", TicketStatuses.Unread)]
        [InlineData("UNREAD", TicketStatuses.Unread)]
        [InlineData(" read ", TicketStatuses.Read)]
        [InlineData("Closed", TicketStatuses.Closed)]
        [InlineData("CLOSED", TicketStatuses.Closed)]
        public void TheStatusIsReadCaseInsensitivelyIntoItsCanonicalWord(
            string value,
            string expected)
        {
            Assert.Equal(expected, Parse(status: value).Status);
        }

        [Theory]
        [InlineData("open", "status")]
        [InlineData("archived", "status")]
        [InlineData("description", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = TicketQuery.TryParse(
                1,
                TicketLimits.DefaultPageSize,
                null,
                parameter == "status" ? value : null,
                parameter == "sortBy" ? value : null,
                parameter == "sortOrder" ? value : null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void TheStatusIsNotASortField()
        {
            // The lifecycle and the unread flags are two different facts, and neither is a column
            // the screen orders by - naming one where a sort field belongs is a refusal, not a
            // silent fallback to the default.
            var query = TicketQuery.TryParse(
                1,
                TicketLimits.DefaultPageSize,
                null,
                null,
                "status",
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("sortBy"));
        }

        [Fact]
        public void PageAndPageSizeAreRefusedRatherThanClamped()
        {
            var badPage = TicketQuery.TryParse(
                0,
                TicketLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                out var pageErrors);

            Assert.Null(badPage);
            Assert.True(pageErrors.ContainsKey("page"));

            var badSize = TicketQuery.TryParse(
                1,
                TicketLimits.MaxPageSize + 1,
                null,
                null,
                null,
                null,
                out var sizeErrors);

            Assert.Null(badSize);
            Assert.True(sizeErrors.ContainsKey("pageSize"));

            var zeroSize = TicketQuery.TryParse(
                1,
                0,
                null,
                null,
                null,
                null,
                out var zeroErrors);

            Assert.Null(zeroSize);
            Assert.True(zeroErrors.ContainsKey("pageSize"));
        }

        [Fact]
        public void ASearchAboveTheCeilingIsRefusedUnderSearch()
        {
            var query = TicketQuery.TryParse(
                1,
                TicketLimits.DefaultPageSize,
                new string('a', TicketLimits.MaxSearchLength + 1),
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("search"));

            Assert.Equal(
                TicketLimits.MaxSearchLength,
                Parse(search: new string('a', TicketLimits.MaxSearchLength)).Search!.Length);
        }

        [Fact]
        public void TheCeilingsAreAcceptedAtTheirOwnValue()
        {
            var query = Parse(pageSize: TicketLimits.MaxPageSize);

            Assert.Equal(TicketLimits.MaxPageSize, query.PageSize);
        }

        [Fact]
        public void EveryBrokenParameterIsReportedAtOnce()
        {
            var query = TicketQuery.TryParse(
                0,
                TicketLimits.MaxPageSize + 1,
                new string('a', TicketLimits.MaxSearchLength + 1),
                "archived",
                "description",
                "sideways",
                out var errors);

            Assert.Null(query);
            Assert.Equal(
                ["page", "pageSize", "search", "sortBy", "sortOrder", "status"],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }
}
