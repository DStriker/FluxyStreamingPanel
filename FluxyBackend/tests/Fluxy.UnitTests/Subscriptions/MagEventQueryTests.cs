using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The query a MAG event read resolves into: its defaults, the event and subscription filters,
    /// the two inclusive date bounds, the search bound, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: an absent value
    /// defaults, present text that names nothing is refused, and a blank value inside a filter is
    /// "no filter" rather than a refusal. A test that only checked a valid query would pass over an
    /// implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class MagEventQueryTests
    {
        private static readonly string Subscription = Guid.NewGuid().ToString();

        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static MagEventQuery Parse(
            int? page = null,
            int? pageSize = null,
            string? search = null,
            string? magEvent = null,
            string? subscriptionId = null,
            string? dateFrom = null,
            string? dateTo = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = MagEventQuery.TryParse(
                page,
                pageSize,
                search,
                magEvent,
                subscriptionId,
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
            Assert.Equal(MagEventListLimits.DefaultPageSize, query.PageSize);
            Assert.Null(query.Search);
            Assert.Null(query.EventType);
            Assert.Null(query.SubscriptionId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(MagEventSortField.CreatedAt, query.SortBy);
            Assert.Equal(MagEventSortOrder.Desc, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                search: " ",
                magEvent: "",
                subscriptionId: "   ",
                dateFrom: " ",
                dateTo: "",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.Search);
            Assert.Null(query.EventType);
            Assert.Null(query.SubscriptionId);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(MagEventSortField.CreatedAt, query.SortBy);
            Assert.Equal(MagEventSortOrder.Desc, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var query = Parse(
                page: 3,
                pageSize: 50,
                search: " hello ",
                magEvent: "PLAYCHANNEL",
                subscriptionId: $" {Subscription} ",
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z",
                sortBy: "createdAt",
                sortOrder: "asc");

            Assert.Equal(3, query.Page);
            Assert.Equal(50, query.PageSize);
            Assert.Equal("hello", query.Search);
            Assert.Equal(MagEventType.PlayChannel, query.EventType);
            Assert.Equal(Guid.Parse(Subscription), query.SubscriptionId);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
            Assert.Equal(MagEventSortField.CreatedAt, query.SortBy);
            Assert.Equal(MagEventSortOrder.Asc, query.SortOrder);
        }

        [Theory]
        [InlineData("playChannel")]
        [InlineData("PLAYCHANNEL")]
        [InlineData(" closePortal ")]
        public void TheEventFilterIsReadCaseInsensitively(string value)
        {
            var query = Parse(magEvent: value);

            Assert.NotNull(query.EventType);
        }

        [Theory]
        [InlineData("play", "event")]
        [InlineData("0", "event")]
        [InlineData("resetStbLockk", "event")]
        [InlineData("eventType", "sortBy")]
        [InlineData("message", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = MagEventQuery.TryParse(
                1,
                MagEventListLimits.DefaultPageSize,
                null,
                parameter == "event" ? value : null,
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
            var badPage = MagEventQuery.TryParse(
                0,
                MagEventListLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var pageErrors);

            Assert.Null(badPage);
            Assert.True(pageErrors.ContainsKey("page"));

            var zeroSize = MagEventQuery.TryParse(
                1,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var zeroErrors);

            Assert.Null(zeroSize);
            Assert.True(zeroErrors.ContainsKey("pageSize"));

            var hugeSize = MagEventQuery.TryParse(
                1,
                MagEventListLimits.MaxPageSize + 1,
                null,
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
                MagEventListLimits.MaxPageSize,
                Parse(pageSize: MagEventListLimits.MaxPageSize).PageSize);
        }

        [Fact]
        public void ASearchAboveTheCeilingIsRefusedUnderSearch()
        {
            var query = MagEventQuery.TryParse(
                1,
                MagEventListLimits.DefaultPageSize,
                new string('a', MagEventListLimits.MaxSearchLength + 1),
                null,
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
                MagEventListLimits.MaxSearchLength,
                Parse(search: new string('a', MagEventListLimits.MaxSearchLength))
                    .Search!
                    .Length);

            Assert.Equal("hello", Parse(search: "  hello  ").Search);
        }

        [Theory]
        [InlineData("not-a-uuid")]
        [InlineData("42")]
        [InlineData("{not a uuid}")]
        public void ASubscriptionFilterThatNamesNoUuidIsRefusedUnderSubscriptionId(string value)
        {
            var query = MagEventQuery.TryParse(
                1,
                MagEventListLimits.DefaultPageSize,
                null,
                null,
                value,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("subscriptionId"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void AUuidSubscriptionFilterIsAccepted()
        {
            Assert.Equal(
                Guid.Parse(Subscription),
                Parse(subscriptionId: Subscription).SubscriptionId);
        }

        [Theory]
        [InlineData("dateFrom")]
        [InlineData("dateTo")]
        public void AnUnparseableBoundIsRefusedUnderItsOwnParameter(string parameter)
        {
            // A value that parses as no instant is a refusal under its own name - never read as
            // "no bound", because that would answer a list wider than the caller asked for. The
            // locale-dependent spelling is refused as well; only ISO 8601 names one instant.
            var query = MagEventQuery.TryParse(
                1,
                MagEventListLimits.DefaultPageSize,
                null,
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
                magEvent: "sendMessage",
                subscriptionId: Subscription,
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z");

            Assert.Equal("refused", query.Search);
            Assert.Equal(MagEventType.SendMessage, query.EventType);
            Assert.Equal(Guid.Parse(Subscription), query.SubscriptionId);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
        }

        [Fact]
        public void EveryBrokenParameterIsReportedAtOnce()
        {
            var query = MagEventQuery.TryParse(
                0,
                MagEventListLimits.MaxPageSize + 1,
                new string('a', MagEventListLimits.MaxSearchLength + 1),
                "play",
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
                    "event",
                    "page",
                    "pageSize",
                    "search",
                    "sortBy",
                    "sortOrder",
                    "subscriptionId"
                ],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }
}
