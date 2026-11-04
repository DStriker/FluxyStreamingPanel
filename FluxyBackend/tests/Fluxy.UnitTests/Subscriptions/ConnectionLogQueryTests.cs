using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The query a connection log read resolves into: the defaults it starts from, the values it
    /// reads, and the parameter each refusal names.
    /// </summary>
    /// <remarks>
    /// One test per rule, because the value of the rules is that they disagree with each other:
    /// <c>kind</c> defaults while an unknown <c>kind</c> is refused, <c>state</c> is a two-member
    /// choice rather than a boolean a typo could silently mean, and the three identifiers are
    /// judged separately so a request that broke two is told about both. A test that only checked
    /// a valid query would pass over an implementation that read every parameter as "no filter".
    /// </remarks>
    public sealed class ConnectionLogQueryTests
    {
        /// <summary>An absent query - the untouched screen.</summary>
        private static ConnectionLogQuery Parse(
            string? kind = null,
            string? state = null,
            string? subscriptionId = null,
            string? ownerId = null,
            string? streamId = null,
            string? ip = null,
            string? country = null,
            string? isp = null,
            string? userAgent = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var query = ConnectionLogQuery.TryParse(
                kind,
                state,
                subscriptionId,
                ownerId,
                streamId,
                ip,
                country,
                isp,
                userAgent,
                sortBy,
                sortOrder,
                out var errors);

            Assert.Empty(errors);
            Assert.NotNull(query);

            return query!;
        }

        [Fact]
        public void AnAbsentQueryStartsFromVideoAllAndTheDefaultOrder()
        {
            var query = Parse();

            Assert.Equal(ConnectionStreamKind.VideoStream, query.StreamKind);
            Assert.False(query.LiveOnly);
            Assert.Null(query.SubscriptionId);
            Assert.Null(query.OwnerId);
            Assert.Null(query.StreamId);
            Assert.Null(query.Ip);
            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.UserAgent);
            Assert.Equal(ConnectionLogSortField.StartedAt, query.SortBy);
            Assert.Equal(ConnectionLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void AnEmptyValueMeansTheSameAsAnAbsentOne()
        {
            var query = Parse(kind: "", state: "   ", sortBy: "", sortOrder: " ");

            Assert.Equal(ConnectionStreamKind.VideoStream, query.StreamKind);
            Assert.False(query.LiveOnly);
            Assert.Equal(ConnectionLogSortField.StartedAt, query.SortBy);
            Assert.Equal(ConnectionLogSortOrder.Descending, query.SortOrder);
        }

        [Fact]
        public void EveryNamedValueIsRead()
        {
            var subscription = Guid.NewGuid();
            var owner = Guid.NewGuid();
            var stream = Guid.NewGuid();

            var query = Parse(
                kind: "audio",
                state: "live",
                subscriptionId: subscription.ToString(),
                ownerId: owner.ToString(),
                streamId: stream.ToString(),
                ip: " 203.0.113.9 ",
                country: "de",
                isp: " Example ISP ",
                userAgent: " Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140 ",
                sortBy: "endedAt",
                sortOrder: "asc");

            Assert.Equal(ConnectionStreamKind.AudioStream, query.StreamKind);
            Assert.True(query.LiveOnly);
            Assert.Equal(subscription, query.SubscriptionId);
            Assert.Equal(owner, query.OwnerId);
            Assert.Equal(stream, query.StreamId);
            Assert.Equal("203.0.113.9", query.Ip);
            Assert.Equal("DE", query.CountryCode);
            Assert.Equal("Example ISP", query.Isp);
            Assert.Equal(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140",
                query.UserAgent);
            Assert.Equal(ConnectionLogSortField.EndedAt, query.SortBy);
            Assert.Equal(ConnectionLogSortOrder.Ascending, query.SortOrder);
        }

        [Theory]
        [InlineData("kind", "videostream")]
        [InlineData("kind", "1")]
        [InlineData("state", "ended")]
        [InlineData("state", "0")]
        [InlineData("sortBy", "streamTitle")]
        [InlineData("sortBy", "username")]
        [InlineData("sortOrder", "downwards")]
        [InlineData("subscriptionId", "not-a-uuid")]
        [InlineData("ownerId", "1234")]
        [InlineData("streamId", "not-a-uuid")]
        [InlineData("country", "XX")]
        [InlineData("country", "Germany")]
        public void AValueThatNamesNothingIsRefusedUnderItsOwnParameter(
            string parameter,
            string value)
        {
            var query = ConnectionLogQuery.TryParse(
                parameter == "kind" ? value : null,
                parameter == "state" ? value : null,
                parameter == "subscriptionId" ? value : null,
                parameter == "ownerId" ? value : null,
                parameter == "streamId" ? value : null,
                null,
                parameter == "country" ? value : null,
                null,
                null,
                parameter == "sortBy" ? value : null,
                parameter == "sortOrder" ? value : null,
                out var errors);

            Assert.Null(query);
            Assert.True(
                errors.ContainsKey(parameter),
                $"errors did not name '{parameter}': {string.Join(", ", errors.Keys)}");
        }

        [Fact]
        public void TwoBrokenParametersAreReportedTogether()
        {
            // The screen sends its whole state on one request, so a caller wrong about two things
            // is told about both rather than being walked through them one at a time.
            var query = ConnectionLogQuery.TryParse(
                "streamkind",
                "finished",
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
            Assert.True(errors.ContainsKey("kind"));
            Assert.True(errors.ContainsKey("state"));
        }

        [Fact]
        public void ABlankIdentifierIsNoFilterRatherThanARefusal()
        {
            var query = Parse(subscriptionId: "", ownerId: "  ", streamId: "");

            Assert.Null(query.SubscriptionId);
            Assert.Null(query.OwnerId);
            Assert.Null(query.StreamId);
        }

        [Fact]
        public void ABlankCountryAndIspAreNoFilterRatherThanARefusal()
        {
            var query = Parse(country: "", isp: "   ", userAgent: "  ");

            Assert.Null(query.CountryCode);
            Assert.Null(query.Isp);
            Assert.Null(query.UserAgent);
        }

        [Fact]
        public void AUserAgentIsKeptExactlyAsTypedApartFromItsEdges()
        {
            // Exact-match semantics, and case included: the value is a client's own string, so
            // folding its case would answer rows for a client that never sent it. Only the
            // surrounding whitespace a query string can carry is trimmed - the same rule `ip`
            // and `isp` follow beside it.
            var query = Parse(userAgent: "  Mozilla/5.0 (X11; Linux x86_64) Firefox/141.0  ");

            Assert.Equal("Mozilla/5.0 (X11; Linux x86_64) Firefox/141.0", query.UserAgent);
        }

        [Fact]
        public void AUserAgentIsNotFoldedToAnotherCase()
        {
            var upper = Parse(userAgent: "CURL/8.5.0").UserAgent;
            var lower = Parse(userAgent: "curl/8.5.0").UserAgent;

            // Two different clients as far as the filter is concerned. The database compares
            // them the same way the parser keeps them, so the assertion is that nothing here
            // collapsed one into the other.
            Assert.Equal("CURL/8.5.0", upper);
            Assert.Equal("curl/8.5.0", lower);
            Assert.NotEqual(upper, lower);
        }
    }
}
