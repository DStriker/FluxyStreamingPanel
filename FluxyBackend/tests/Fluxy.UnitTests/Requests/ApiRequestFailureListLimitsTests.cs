using Fluxy.Core.Requests.Enums;
using Fluxy.Core.Requests.Utils;

namespace Fluxy.UnitTests.Requests
{
    /// <summary>
    /// The column widths and list bounds of the API request failure log, and the numeric
    /// vocabulary of its reason and its sort choices.
    /// </summary>
    /// <remarks>
    /// The widths are shared by the table configuration and the query parser, so a test here is
    /// what keeps them from drifting apart silently. Only <see cref="ApiRequestFailureReason"/> is
    /// schema: its values are already written into rows, so renumbering one would reinterpret every
    /// stored refusal as a different reason without a single query being made.
    /// <see cref="ApiRequestFailureSortField"/> and <see cref="ApiRequestFailureSortOrder"/> are
    /// query-contract only and are never persisted; their numbers are pinned so the sort choices a
    /// client sends and the members this API resolves stay paired.
    /// </remarks>
    public sealed class ApiRequestFailureListLimitsTests
    {
        [Fact]
        public void TheStoredWidthsAreTheOnesTheColumnsDeclare()
        {
            Assert.Equal(512, ApiRequestFailureListLimits.SubscriptionDataMaxLength);
            Assert.Equal(255, ApiRequestFailureListLimits.ContentNameMaxLength);
            Assert.Equal(1000, ApiRequestFailureListLimits.DescriptionMaxLength);
            Assert.Equal(45, ApiRequestFailureListLimits.IpMaxLength);
            Assert.Equal(255, ApiRequestFailureListLimits.IspMaxLength);
            Assert.Equal(2, ApiRequestFailureListLimits.CountryCodeLength);
        }

        [Fact]
        public void TheAddressColumnHoldsTheLongestTextualIpv6Form()
        {
            // The full form with an embedded IPv4 suffix is 45 characters.
            const string longest = "0000:0000:0000:0000:0000:0000:255.255.255.255";

            Assert.Equal(ApiRequestFailureListLimits.IpMaxLength, longest.Length);
        }

        [Fact]
        public void ThePageDefaultsAndCeilingAreTheSharedOnes()
        {
            Assert.Equal(20, ApiRequestFailureListLimits.DefaultPageSize);
            Assert.Equal(1000, ApiRequestFailureListLimits.MaxPageSize);
            Assert.Equal(200, ApiRequestFailureListLimits.MaxSearchLength);
            Assert.Equal(100, ApiRequestFailureListLimits.MaxFilterValues);
        }

        [Theory]
        [InlineData(ApiRequestFailureSortField.CreatedAt, 0)]
        [InlineData(ApiRequestFailureSortField.Reason, 1)]
        public void TheSortFieldNumbersAreTheContract(
            ApiRequestFailureSortField field,
            int stored)
        {
            Assert.Equal(stored, (int)field);
        }

        [Theory]
        [InlineData(ApiRequestFailureSortOrder.Ascending, 0)]
        [InlineData(ApiRequestFailureSortOrder.Descending, 1)]
        public void TheSortOrderNumbersAreTheContract(
            ApiRequestFailureSortOrder order,
            int stored)
        {
            Assert.Equal(stored, (int)order);
        }

        [Theory]
        [InlineData(ApiRequestFailureReason.UnknownId, 0)]
        [InlineData(ApiRequestFailureReason.GeoIpViolation, 1)]
        [InlineData(ApiRequestFailureReason.AuthFailed, 2)]
        [InlineData(ApiRequestFailureReason.TooManyConnections, 3)]
        [InlineData(ApiRequestFailureReason.SubscriptionExpired, 4)]
        [InlineData(ApiRequestFailureReason.UserBlocked, 5)]
        public void TheReasonNumbersAreTheSchema(ApiRequestFailureReason reason, int stored)
        {
            Assert.Equal(stored, (int)reason);
        }
    }
}
