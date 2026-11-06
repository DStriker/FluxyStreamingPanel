using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The query a credit log read resolves into: the defaults it starts from, the values it
    /// reads, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the value of the rules is that they disagree with each other:
    /// <c>sortBy</c> defaults while an unknown <c>sortBy</c> is refused, the two date bounds are
    /// judged separately so a request that broke both is told about both, and a blank bound means
    /// "no bound" while a malformed one is a refusal. A test that only checked a valid query would
    /// pass over an implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class CreditLogQueryTests
    {
        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static CreditLogQuery Parse(
            string? resellerId = null,
            string? dateFrom = null,
            string? dateTo = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = CreditLogQuery.TryParse(
                resellerId,
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

            Assert.Null(query.ResellerId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(CreditLogSortField.CreatedAt, query.SortBy);
            Assert.Equal(CreditLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                resellerId: "",
                dateFrom: " ",
                dateTo: "",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.ResellerId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(CreditLogSortField.CreatedAt, query.SortBy);
            Assert.Equal(CreditLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var reseller = Guid.NewGuid();

            var query = Parse(
                resellerId: reseller.ToString(),
                dateFrom: "2026-05-01T00:00:00Z",
                dateTo: "2026-05-02T00:00:00+03:00",
                sortBy: "amount",
                sortOrder: "asc");

            Assert.Equal(reseller, query.ResellerId);
            Assert.Equal(
                DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                query.DateFrom);
            Assert.Equal(
                DateTimeOffset.Parse("2026-05-01T21:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                query.DateTo);
            Assert.Equal(CreditLogSortField.Amount, query.SortBy);
            Assert.Equal(CreditLogSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("createdAt", CreditLogSortField.CreatedAt)]
        [InlineData("CREATEDAT", CreditLogSortField.CreatedAt)]
        [InlineData(" amount ", CreditLogSortField.Amount)]
        [InlineData("AMOUNT", CreditLogSortField.Amount)]
        public void TheSortFieldIsReadCaseInsensitively(string value, CreditLogSortField expected)
        {
            Assert.Equal(expected, Parse(sortBy: value).SortBy);
        }

        [Theory]
        [InlineData("asc", CreditLogSortOrder.Ascending)]
        [InlineData("DESC", CreditLogSortOrder.Descending)]
        [InlineData(" Desc ", CreditLogSortOrder.Descending)]
        public void TheSortOrderIsReadCaseInsensitively(string value, CreditLogSortOrder expected)
        {
            Assert.Equal(expected, Parse(sortOrder: value).SortOrder);
        }

        [Theory]
        [InlineData("description", "sortBy")]
        [InlineData("resellerUsername", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = CreditLogQuery.TryParse(
                resellerId: null,
                dateFrom: null,
                dateTo: null,
                sortBy: parameter == "sortBy" ? value : null,
                sortOrder: parameter == "sortOrder" ? value : null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void AnIdentifierThatIsNotAUuidIsRefusedUnderResellerId()
        {
            var query = CreditLogQuery.TryParse(
                "not-a-uuid",
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("resellerId"));
        }

        [Theory]
        [InlineData("dateFrom")]
        [InlineData("dateTo")]
        public void AnUnparseableBoundIsRefusedUnderItsOwnParameter(string parameter)
        {
            var query = CreditLogQuery.TryParse(
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
        public void TwoBrokenBoundsAreBothReportedAtOnce()
        {
            var query = CreditLogQuery.TryParse(
                "not-a-uuid",
                "yesterday",
                "tomorrow",
                "description",
                "sideways",
                out var errors);

            Assert.Null(query);
            Assert.Equal(
                ["dateFrom", "dateTo", "resellerId", "sortBy", "sortOrder"],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void TheDateRangeIsTwoIndependentInclusiveBounds()
        {
            var onlyFrom = Parse(dateFrom: "2026-05-01");
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), onlyFrom.DateFrom);
            Assert.Null(onlyFrom.DateTo);

            var onlyTo = Parse(dateTo: "2026-05-31");
            Assert.Null(onlyTo.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero), onlyTo.DateTo);
        }
    }
}
