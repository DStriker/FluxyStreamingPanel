using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The query a user log read resolves into: its defaults, the repeated filters, the two date
    /// bounds, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the rules disagree with each other on purpose: an absent value
    /// defaults, present text that names nothing is refused, and a blank element inside a repeated
    /// filter is a refusal rather than a silent widening. A test that only checked a valid query
    /// would pass over an implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class UserLogQueryTests
    {
        /// <summary>Parses a query, asserting that nothing was refused.</summary>
        private static UserLogQuery Parse(
            int? page = null,
            int? pageSize = null,
            string? search = null,
            IReadOnlyList<string>? userIds = null,
            IReadOnlyList<string>? groupIds = null,
            IReadOnlyList<string>? recordTypes = null,
            string? ip = null,
            string? country = null,
            string? isp = null,
            string? dateFrom = null,
            string? dateTo = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = UserLogQuery.TryParse(
                page,
                pageSize,
                search,
                userIds,
                groupIds,
                recordTypes,
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
            Assert.Equal(UserLogListLimits.DefaultPageSize, query.PageSize);
            Assert.Null(query.Search);
            Assert.Null(query.UserIds);
            Assert.Null(query.GroupIds);
            Assert.Null(query.RecordTypes);
            Assert.Null(query.Ip);
            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(UserLogSortField.CreatedAt, query.SortBy);
            Assert.Equal(UserLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(
                search: " ",
                ip: " ",
                country: "",
                isp: "   ",
                dateFrom: "",
                dateTo: " ",
                sortBy: "",
                sortOrder: " ");

            Assert.Null(query.Search);
            Assert.Null(query.Ip);
            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.DateFrom);
            Assert.Null(query.DateTo);
            Assert.Equal(UserLogSortField.CreatedAt, query.SortBy);
            Assert.Equal(UserLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var user = Guid.NewGuid();
            var group = Guid.NewGuid();

            var query = Parse(
                page: 3,
                pageSize: 50,
                search: " alice ",
                userIds: [user.ToString()],
                groupIds: [group.ToString()],
                recordTypes: ["login", "LOGOUT"],
                ip: " 203.0.113.9 ",
                country: "de",
                isp: " Example ISP ",
                dateFrom: "2026-05-01T00:00:00Z",
                dateTo: "2026-05-02T00:00:00+03:00",
                sortBy: "recordType",
                sortOrder: "asc");

            Assert.Equal(3, query.Page);
            Assert.Equal(50, query.PageSize);
            Assert.Equal("alice", query.Search);
            Assert.Equal([user], query.UserIds);
            Assert.Equal([group], query.GroupIds);
            Assert.Equal([UserLogRecordType.Login, UserLogRecordType.Logout], query.RecordTypes);
            Assert.Equal("203.0.113.9", query.Ip);
            Assert.Equal("DE", query.CountryCode);
            Assert.Equal("Example ISP", query.Isp);
            Assert.Equal(
                DateTimeOffset.Parse(
                    "2026-05-01T00:00:00Z",
                    System.Globalization.CultureInfo.InvariantCulture),
                query.DateFrom);
            Assert.Equal(
                DateTimeOffset.Parse(
                    "2026-05-01T21:00:00Z",
                    System.Globalization.CultureInfo.InvariantCulture),
                query.DateTo);
            Assert.Equal(UserLogSortField.RecordType, query.SortBy);
            Assert.Equal(UserLogSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("createdAt", UserLogSortField.CreatedAt)]
        [InlineData("CREATEDAT", UserLogSortField.CreatedAt)]
        [InlineData(" recordType ", UserLogSortField.RecordType)]
        [InlineData("RECORDTYPE", UserLogSortField.RecordType)]
        public void TheSortFieldIsReadCaseInsensitively(string value, UserLogSortField expected)
        {
            Assert.Equal(expected, Parse(sortBy: value).SortBy);
        }

        [Theory]
        [InlineData("asc", UserLogSortOrder.Ascending)]
        [InlineData("DESC", UserLogSortOrder.Descending)]
        [InlineData(" Desc ", UserLogSortOrder.Descending)]
        public void TheSortOrderIsReadCaseInsensitively(string value, UserLogSortOrder expected)
        {
            Assert.Equal(expected, Parse(sortOrder: value).SortOrder);
        }

        [Theory]
        [InlineData("description", "sortBy")]
        [InlineData("isSuccess", "sortBy")]
        [InlineData("upwards", "sortOrder")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string value,
            string parameter)
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
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
            var badPage = UserLogQuery.TryParse(
                0,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
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

            var badSize = UserLogQuery.TryParse(
                1,
                UserLogListLimits.MaxPageSize + 1,
                null,
                null,
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

            var zeroSize = UserLogQuery.TryParse(
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
                null,
                null,
                out var zeroErrors);

            Assert.Null(zeroSize);
            Assert.True(zeroErrors.ContainsKey("pageSize"));
        }

        [Fact]
        public void ASearchAboveTheCeilingIsRefusedUnderSearch()
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                new string('a', UserLogListLimits.MaxSearchLength + 1),
                null,
                null,
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
                UserLogListLimits.MaxSearchLength,
                Parse(search: new string('a', UserLogListLimits.MaxSearchLength)).Search!.Length);
        }

        [Theory]
        [InlineData("userIds")]
        [InlineData("groupIds")]
        [InlineData("recordTypes")]
        public void ABadElementRefusesTheWholeFilterUnderItsOwnName(string parameter)
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                parameter == "userIds" ? [Guid.NewGuid().ToString(), "not-a-uuid"] : null,
                parameter == "groupIds" ? ["not-a-uuid"] : null,
                parameter == "recordTypes" ? ["login", "not a record type"] : null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Theory]
        [InlineData("userIds")]
        [InlineData("groupIds")]
        [InlineData("recordTypes")]
        public void ABlankElementIsARefusalRatherThanNoFilter(string parameter)
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                parameter == "userIds" ? [""] : null,
                parameter == "groupIds" ? [" "] : null,
                parameter == "recordTypes" ? [""] : null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void MoreThanOneHundredValuesIsRefusedUnderTheFiltersOwnName()
        {
            var tooMany = Enumerable
                .Range(0, UserLogListLimits.MaxFilterValues + 1)
                .Select(_ => Guid.NewGuid().ToString())
                .ToArray();

            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                tooMany,
                tooMany,
                Enumerable.Repeat("login", UserLogListLimits.MaxFilterValues + 1).ToArray(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.Equal(
                ["groupIds", "recordTypes", "userIds"],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());

            var atCeiling = Parse(
                userIds: tooMany.Take(UserLogListLimits.MaxFilterValues).ToArray(),
                groupIds: tooMany.Take(UserLogListLimits.MaxFilterValues).ToArray(),
                recordTypes: Enumerable.Repeat("login", UserLogListLimits.MaxFilterValues).ToArray());

            Assert.Equal(UserLogListLimits.MaxFilterValues, atCeiling.UserIds!.Count);
        }

        [Fact]
        public void AnEmptyRepeatedFilterIsTheSameAsAnAbsentOne()
        {
            var query = Parse(userIds: [], groupIds: [], recordTypes: []);

            Assert.Null(query.UserIds);
            Assert.Null(query.GroupIds);
            Assert.Null(query.RecordTypes);
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
        [InlineData("country", "XX")]
        [InlineData("country", "Germany")]
        [InlineData("country", "D")]
        public void ACountryThatNamesNoCodeIsRefusedUnderCountry(string parameter, string value)
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
                null,
                null,
                null,
                parameter == "country" ? value : null,
                null,
                null,
                null,
                null,
                null,
                out var errors);

            Assert.Null(query);
            Assert.True(errors.ContainsKey(parameter), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void ANetworkValueAboveItsColumnsWidthIsRefusedUnderItsOwnParameter()
        {
            var longIp = new string('1', UserLogListLimits.IpMaxLength + 1);
            var longIsp = new string('i', UserLogListLimits.IspMaxLength + 1);

            var refusedIp = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
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

            var refusedIsp = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
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
                UserLogListLimits.IpMaxLength,
                Parse(ip: new string('1', UserLogListLimits.IpMaxLength)).Ip!.Length);
            Assert.Equal(
                UserLogListLimits.IspMaxLength,
                Parse(isp: new string('i', UserLogListLimits.IspMaxLength)).Isp!.Length);
        }

        [Fact]
        public void TheNetworkFiltersComposeWithTheExistingOnes()
        {
            var user = Guid.NewGuid();

            var query = Parse(
                search: "login failed",
                userIds: [user.ToString()],
                recordTypes: ["login"],
                ip: "198.51.100.2",
                country: "de",
                isp: "Example ISP",
                dateFrom: "2026-05-01",
                dateTo: "2026-05-31");

            Assert.Equal("login failed", query.Search);
            Assert.Equal([user], query.UserIds);
            Assert.Equal([UserLogRecordType.Login], query.RecordTypes);
            Assert.Equal("198.51.100.2", query.Ip);
            Assert.Equal("DE", query.CountryCode);
            Assert.Equal("Example ISP", query.Isp);
            Assert.NotNull(query.DateFrom);
            Assert.NotNull(query.DateTo);
        }

        [Theory]
        [InlineData("dateFrom")]
        [InlineData("dateTo")]
        public void AnUnparseableBoundIsRefusedUnderItsOwnParameter(string parameter)
        {
            var query = UserLogQuery.TryParse(
                1,
                UserLogListLimits.DefaultPageSize,
                null,
                null,
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
            var onlyFrom = Parse(dateFrom: "2026-05-01");
            Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), onlyFrom.DateFrom);
            Assert.Null(onlyFrom.DateTo);

            var onlyTo = Parse(dateTo: "2026-05-31");
            Assert.Null(onlyTo.DateFrom);
            Assert.Equal(new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero), onlyTo.DateTo);
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
        public void EveryBrokenParameterIsReportedAtOnce()
        {
            var query = UserLogQuery.TryParse(
                0,
                UserLogListLimits.MaxPageSize + 1,
                new string('a', UserLogListLimits.MaxSearchLength + 1),
                ["not-a-uuid"],
                ["not-a-uuid"],
                ["not a record type"],
                new string('1', UserLogListLimits.IpMaxLength + 1),
                "Germany",
                new string('i', UserLogListLimits.IspMaxLength + 1),
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
                    "groupIds",
                    "ip",
                    "isp",
                    "page",
                    "pageSize",
                    "recordTypes",
                    "search",
                    "sortBy",
                    "sortOrder",
                    "userIds"
                ],
                errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }
}
