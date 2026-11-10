using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.Utils;

namespace Fluxy.UnitTests.Requests
{
    /// <summary>
    /// The query an API request failure read resolves into: its defaults, the repeated reason
    /// filter, the three network filters, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: an absent value
    /// defaults, present text that names nothing is refused, and a blank element inside the
    /// repeated filter is a refusal rather than a silent widening. A test that only checked a
    /// valid query would pass over an implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class ApiRequestFailureQueryTests
    {
        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static ApiRequestFailureQuery Parse(
            int? page = null,
            int? pageSize = null,
            string? search = null,
            IReadOnlyList<string>? reasons = null,
            string? ip = null,
            string? country = null,
            string? isp = null,
            string? dateFrom = null,
            string? dateTo = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = ApiRequestFailureQuery.TryParse(
                page,
                pageSize,
                search,
                reasons,
                ip,
                country,
                isp,
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
            Assert.Equal(ApiRequestFailureListLimits.DefaultPageSize, query.PageSize);
            Assert.Null(query.Search);
            Assert.Null(query.Reasons);
            Assert.Null(query.Ip);
            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(ApiRequestFailureSortField.CreatedAt, query.SortBy);
            Assert.Equal(ApiRequestFailureSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                search: " ",
                ip: " ",
                country: "",
                isp: "   ",
                dateFrom: " ",
                dateTo: "",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.Search);
            Assert.Null(query.Ip);
            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(ApiRequestFailureSortField.CreatedAt, query.SortBy);
            Assert.Equal(ApiRequestFailureSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var query = Parse(
                page: 3,
                pageSize: 50,
                search: " token ",
                reasons: ["auth_failed", "USER_BLOCKED"],
                ip: " 203.0.113.9 ",
                country: "de",
                isp: " Example ISP ",
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z",
                sortBy: "reason",
                sortOrder: "asc");

            Assert.Equal(3, query.Page);
            Assert.Equal(50, query.PageSize);
            Assert.Equal("token", query.Search);
            Assert.Equal(
                [ApiRequestFailureReason.AuthFailed, ApiRequestFailureReason.UserBlocked],
                query.Reasons);
            Assert.Equal("203.0.113.9", query.Ip);
            Assert.Equal("DE", query.CountryCode);
            Assert.Equal("Example ISP", query.Isp);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
            Assert.Equal(ApiRequestFailureSortField.Reason, query.SortBy);
            Assert.Equal(ApiRequestFailureSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("createdAt", ApiRequestFailureSortField.CreatedAt)]
        [InlineData("CREATEDAT", ApiRequestFailureSortField.CreatedAt)]
        [InlineData(" reason ", ApiRequestFailureSortField.Reason)]
        [InlineData("REASON", ApiRequestFailureSortField.Reason)]
        public void TheSortFieldIsReadCaseInsensitively(
            string value,
            ApiRequestFailureSortField expected)
        {
            Assert.Equal(expected, Parse(sortBy: value).SortBy);
        }

        [Theory]
        [InlineData("asc", ApiRequestFailureSortOrder.Ascending)]
        [InlineData("DESC", ApiRequestFailureSortOrder.Descending)]
        [InlineData(" Desc ", ApiRequestFailureSortOrder.Descending)]
        public void TheSortOrderIsReadCaseInsensitively(
            string value,
            ApiRequestFailureSortOrder expected)
        {
            Assert.Equal(expected, Parse(sortOrder: value).SortOrder);
        }

        [Theory]
        [InlineData("description", "sortBy")]
        [InlineData("ip", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
                null,
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
            var badPage = ApiRequestFailureQuery.TryParse(
                0,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
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

            var badSize = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.MaxPageSize + 1,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var sizeErrors);

            Assert.Null(badSize);
            Assert.True(sizeErrors.ContainsKey("pageSize"));

            var zeroSize = ApiRequestFailureQuery.TryParse(
                1,
                0,
                null,
                null,
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
        }

        [Fact]
        public void ASearchAboveTheCeilingIsRefusedUnderSearch()
        {
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                new string('a', ApiRequestFailureListLimits.MaxSearchLength + 1),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("search"));

            Assert.Equal(
                ApiRequestFailureListLimits.MaxSearchLength,
                Parse(search: new string('a', ApiRequestFailureListLimits.MaxSearchLength))
                    .Search!
                    .Length);
        }

        [Fact]
        public void AnUnknownReasonRefusesTheWholeFilterUnderReasons()
        {
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                ["unknown_id", "nope"],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("reasons"), string.Join(", ", errors.Keys));
            Assert.Equal([ApiRequestFailureReasons.UnknownReasonHint], errors["reasons"]);
        }

        [Fact]
        public void ABlankReasonElementIsARefusalRatherThanNoFilter()
        {
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                [""],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("reasons"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void TheReasonsAreDeduplicatedInTheOrderSent()
        {
            var query = Parse(
                reasons: ["user_blocked", "auth_failed", "USER_BLOCKED", " unknown_id "]);

            Assert.Equal(
                [
                    ApiRequestFailureReason.UserBlocked,
                    ApiRequestFailureReason.AuthFailed,
                    ApiRequestFailureReason.UnknownId
                ],
                query.Reasons);
        }

        [Fact]
        public void MoreThanOneHundredReasonsIsRefusedUnderTheFiltersOwnName()
        {
            var tooMany = Enumerable
                .Range(0, ApiRequestFailureListLimits.MaxFilterValues + 1)
                .Select(_ => "auth_failed")
                .ToArray();

            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                tooMany,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("reasons"));

            var atCeiling = Parse(
                reasons: Enumerable
                    .Repeat("auth_failed", ApiRequestFailureListLimits.MaxFilterValues)
                    .ToArray());

            Assert.Single(atCeiling.Reasons!);
            Assert.Equal(ApiRequestFailureReason.AuthFailed, atCeiling.Reasons![0]);
        }

        [Fact]
        public void AnEmptyRepeatedFilterIsTheSameAsAnAbsentOne()
        {
            Assert.Null(Parse(reasons: []).Reasons);
        }

        [Fact]
        public void TheNetworkFiltersAreTheConnectionLogsAndTrimAtTheirEdges()
        {
            var query = Parse(
                ip: " 203.0.113.9 ",
                country: " fr ",
                isp: " Example Telecom ");

            Assert.Equal("203.0.113.9", query.Ip);
            Assert.Equal("FR", query.CountryCode);
            Assert.Equal("Example Telecom", query.Isp);
        }

        [Fact]
        public void TheAddressAndProviderAreNotFoldedToAnotherCase()
        {
            // Exact semantics, case included - the same rule the connection log follows: the row
            // is a snapshot of what the request sent, so folding the case would answer a value the
            // log never recorded.
            var upper = Parse(ip: "2001:DB8::1", isp: "EXAMPLE ISP");
            var lower = Parse(ip: "2001:db8::1", isp: "example isp");

            Assert.Equal("2001:DB8::1", upper.Ip);
            Assert.Equal("2001:db8::1", lower.Ip);
            Assert.NotEqual(upper.Ip, lower.Ip);
            Assert.NotEqual(upper.Isp, lower.Isp);
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("Germany")]
        [InlineData("D")]
        public void ACountryThatNamesNoCodeIsRefusedUnderCountry(string value)
        {
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
                null,
                value,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey("country"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void ANetworkValueAboveItsColumnsWidthIsRefusedUnderItsOwnParameter()
        {
            var longIp = new string('1', ApiRequestFailureListLimits.IpMaxLength + 1);
            var longIsp = new string('i', ApiRequestFailureListLimits.IspMaxLength + 1);

            var refusedIp = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
                longIp,
                null,
                null,
                null,
                null,
                null,
                null,
                out var ipErrors);

            Assert.Null(refusedIp);
            Assert.True(ipErrors.ContainsKey("ip"));

            var refusedIsp = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                longIsp,
                null,
                null,
                null,
                null,
                out var ispErrors);

            Assert.Null(refusedIsp);
            Assert.True(ispErrors.ContainsKey("isp"));

            // At the column's own width the value is accepted: the bound is the column, not one
            // character less than it.
            Assert.Equal(
                ApiRequestFailureListLimits.IpMaxLength,
                Parse(ip: new string('1', ApiRequestFailureListLimits.IpMaxLength)).Ip!.Length);
            Assert.Equal(
                ApiRequestFailureListLimits.IspMaxLength,
                Parse(isp: new string('i', ApiRequestFailureListLimits.IspMaxLength)).Isp!.Length);
        }

        [Fact]
        public void TheFiltersCompose()
        {
            var query = Parse(
                search: "token",
                reasons: ["auth_failed", "user_blocked"],
                ip: "198.51.100.2",
                country: "de",
                isp: "SomeNet",
                dateFrom: "2026-05-01",
                dateTo: "2026-05-03T23:59:59.999Z");

            Assert.Equal("token", query.Search);
            Assert.Equal(
                [ApiRequestFailureReason.AuthFailed, ApiRequestFailureReason.UserBlocked],
                query.Reasons);
            Assert.Equal("198.51.100.2", query.Ip);
            Assert.Equal("DE", query.CountryCode);
            Assert.Equal("SomeNet", query.Isp);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, 999, TimeSpan.Zero), query.DateTo);
        }

        [Fact]
        public void EveryBrokenParameterIsReportedAtOnce()
        {
            var query = ApiRequestFailureQuery.TryParse(
                0,
                ApiRequestFailureListLimits.MaxPageSize + 1,
                new string('a', ApiRequestFailureListLimits.MaxSearchLength + 1),
                ["not a reason"],
                new string('1', ApiRequestFailureListLimits.IpMaxLength + 1),
                "Germany",
                new string('i', ApiRequestFailureListLimits.IspMaxLength + 1),
                "yesterday",
                "tomorrow",
                "description",
                "sideways",
                out var errors);

            Assert.Null(query);
            Assert.Equal(
                [
                    "country",
                    "dateFrom",
                    "dateTo",
                    "ip",
                    "isp",
                    "page",
                    "pageSize",
                    "reasons",
                    "search",
                    "sortBy",
                    "sortOrder"
                ],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }

        [Theory]
        [InlineData("dateFrom")]
        [InlineData("dateTo")]
        public void AnUnparseableBoundIsRefusedUnderItsOwnParameter(string parameter)
        {
            // A value that parses as no instant is a refusal under its own name - never read as
            // "no bound", because that would answer a list wider than the caller asked for. The
            // locale-dependent spelling is refused as well; only ISO 8601 names one instant.
            var query = ApiRequestFailureQuery.TryParse(
                1,
                ApiRequestFailureListLimits.DefaultPageSize,
                null,
                null,
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
        public void AnInvertedRangeIsAValidQueryDescribingAnEmptyList()
        {
            // A later `from` than `to` is not a broken request: it names an interval with nothing
            // in it, and the answer is 200 with an empty page. Refusing it would turn a valid
            // question into an error, and swapping the bounds would answer a different question.
            var query = Parse(dateFrom: "2026-06-01", dateTo: "2026-05-01");

            Assert.Equal(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), query.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), query.DateTo);
        }
    }
}
