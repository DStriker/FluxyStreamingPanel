using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Rows;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The canonical forms a subscription's access fields are stored in: a MAC, an IP whitelist
    /// entry, a user agent, and the two derivations a plan owns.
    /// </summary>
    /// <remarks>
    /// These are the halves of the rule a database cannot state: a MAC typed in another case is
    /// the same device, an address written as a host is the same network as one written as a CIDR
    /// of full width, and "1 month" from January is not thirty days. Each is exercised directly
    /// because a save that stored the raw input would pass every other check.
    /// </remarks>
    public sealed class SubscriptionNormalizerTests
    {
        [Theory]
        [InlineData("aa-bb-cc-dd-ee-ff", "AA:BB:CC:DD:EE:FF")]
        [InlineData("AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
        [InlineData("aabb.ccdd.eeff", null)]
        [InlineData("aa:bb:cc:dd:ee", null)]
        [InlineData("aa:bb:cc:dd:ee:ff:00", null)]
        [InlineData("zz:bb:cc:dd:ee:ff", null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void AMacIsStoredInOneCanonicalForm(string? input, string? expected)
        {
            Assert.Equal(expected, SubscriptionNormalizer.NormalizeMac(input));
        }

        [Theory]
        [InlineData("192.168.1.1", "192.168.1.1")]
        [InlineData("192.168.1.0/24", "192.168.1.0/24")]
        [InlineData("2001:db8::/32", "2001:db8::/32")]
        [InlineData("::1", "::1")]
        [InlineData("192.168.1.0/33", null)]
        [InlineData("192.168.1.0/-1", null)]
        [InlineData("not-an-address", null)]
        [InlineData("/24", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void AnIpWhitelistEntryIsAnAddressOrACidrRange(string? input, string? expected)
        {
            Assert.Equal(expected, SubscriptionNormalizer.NormalizeIpEntry(input));
        }

        [Fact]
        public void AnIpv6AddressIsStoredInItsNormalizedSpelling()
        {
            // The two spellings of one loopback address must not become two allow-list entries.
            Assert.Equal(
                SubscriptionNormalizer.NormalizeIpEntry("0:0:0:0:0:0:0:1"),
                SubscriptionNormalizer.NormalizeIpEntry("::1"));
        }

        [Theory]
        [InlineData("  Mozilla/5.0  ", "Mozilla/5.0")]
        [InlineData("   ", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void AUserAgentEntryIsTrimmedText(string? input, string? expected)
        {
            Assert.Equal(expected, SubscriptionNormalizer.NormalizeUserAgent(input));
        }

        [Theory]
        [InlineData("0123456789abcdef0123456789abcdef", true)]
        [InlineData("0123456789ABCDEF0123456789ABCDEF", false)]
        [InlineData("0123456789abcdef0123456789abcde", false)]
        [InlineData("0123456789abcdef0123456789abcdef0", false)]
        [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void APlaylistIdIsThirtyTwoLowerCaseHexCharacters(string? input, bool valid)
        {
            Assert.Equal(valid, SubscriptionNormalizer.IsPlaylistId(input));
        }
    }

    /// <summary>
    /// The rules a subscription is judged by: whitelist refusals, the service minimum, and the
    /// expiry a plan computes.
    /// </summary>
    public sealed class SubscriptionRulesTests
    {
        private static readonly DateTimeOffset Created = new(2026, 1, 31, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void AMonthlyPlanComputesTheLastDayOfFebruaryForAJanThirtyFirstSubscription()
        {
            // The scenario the specification names: January 31st plus one month is February 28th
            // in a common year - calendar arithmetic rather than thirty days.
            Assert.Equal(
                new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero),
                SubscriptionRules.ExpiryOf(Created, Plan(1, TariffPlanDurationUnit.Months)));
        }

        [Fact]
        public void AMonthlyPlanInALeapYearComputesFebruaryTwentyNinth()
        {
            var created = new DateTimeOffset(2028, 1, 31, 10, 0, 0, TimeSpan.Zero);

            Assert.Equal(
                new DateTimeOffset(2028, 2, 29, 10, 0, 0, TimeSpan.Zero),
                SubscriptionRules.ExpiryOf(created, Plan(1, TariffPlanDurationUnit.Months)));
        }

        [Fact]
        public void AYearlyPlanComputesTheSameDayNextYear()
        {
            Assert.Equal(
                new DateTimeOffset(2027, 1, 31, 10, 0, 0, TimeSpan.Zero),
                SubscriptionRules.ExpiryOf(Created, Plan(1, TariffPlanDurationUnit.Years)));
        }

        [Fact]
        public void AnHourlyPlanComputesHoursAndADailyPlanComputesDays()
        {
            Assert.Equal(
                Created.AddHours(12),
                SubscriptionRules.ExpiryOf(Created, Plan(12, TariffPlanDurationUnit.Hours)));

            Assert.Equal(
                Created.AddDays(30),
                SubscriptionRules.ExpiryOf(Created, Plan(30, TariffPlanDurationUnit.Days)));
        }

        [Fact]
        public void AnIpWhitelistEntryThatIsNeitherAnAddressNorACidrIsRefused()
        {
            var (_, errors) = SubscriptionRules.NormalizeIpWhitelist(["192.168.1.1", "nope"]);

            Assert.Contains("IpWhitelist", errors.Keys);
        }

        [Fact]
        public void AnIpWhitelistAboveTheEntryBoundIsRefused()
        {
            var entries = Enumerable.Range(0, SubscriptionLimits.WhitelistMaxEntries + 1)
                .Select(index => $"10.0.0.{index}")
                .ToList();

            var (_, errors) = SubscriptionRules.NormalizeIpWhitelist(entries);

            Assert.Contains("IpWhitelist", errors.Keys);
        }

        [Fact]
        public void AnIpWhitelistAtTheEntryBoundIsAccepted()
        {
            var entries = Enumerable.Range(0, SubscriptionLimits.WhitelistMaxEntries)
                .Select(index => $"10.0.0.{index}")
                .ToList();

            var (value, errors) = SubscriptionRules.NormalizeIpWhitelist(entries);

            Assert.Empty(errors);
            Assert.Equal(SubscriptionLimits.WhitelistMaxEntries, value.Count);
        }

        [Fact]
        public void AnEmptyUserAgentEntryIsRefused()
        {
            var (_, errors) = SubscriptionRules.NormalizeUserAgentWhitelist(["Mozilla/5.0", "  "]);

            Assert.Contains("UserAgentWhitelist", errors.Keys);
        }

        [Fact]
        public void AUserAgentAboveTheEntryBoundIsRefused()
        {
            var entries = Enumerable.Range(0, SubscriptionLimits.WhitelistMaxEntries + 1)
                .Select(index => $"agent-{index}")
                .ToList();

            var (_, errors) = SubscriptionRules.NormalizeUserAgentWhitelist(entries);

            Assert.Contains("UserAgentWhitelist", errors.Keys);
        }

        [Fact]
        public void AnEmptyWhitelistIsAcceptedAndClearsTheRestriction()
        {
            var (ips, ipErrors) = SubscriptionRules.NormalizeIpWhitelist([]);
            var (agents, agentErrors) = SubscriptionRules.NormalizeUserAgentWhitelist(null);

            Assert.Empty(ips);
            Assert.Empty(agents);
            Assert.Empty(ipErrors);
            Assert.Empty(agentErrors);
        }

        [Fact]
        public void AConnectionCeilingStaysInsideItsRange()
        {
            Assert.True(SubscriptionRules.IsUsableConnections(1));
            Assert.True(SubscriptionRules.IsUsableConnections(SubscriptionLimits.ConnectionsMax));
            Assert.False(SubscriptionRules.IsUsableConnections(0));
            Assert.False(SubscriptionRules.IsUsableConnections(
                SubscriptionLimits.ConnectionsMax + 1));
        }

        [Fact]
        public void AnOutputFormatSetWithNoFormatIsRefused()
        {
            Assert.False(SubscriptionRules.IsUsableOutputFormats(SubscriptionOutputFormat.None));
            Assert.True(SubscriptionRules.IsUsableOutputFormats(SubscriptionOutputFormat.MpegTs));
            Assert.True(SubscriptionRules.IsUsableOutputFormats(SubscriptionOutputFormat.Hls));
            Assert.True(SubscriptionRules.IsUsableOutputFormats(
                SubscriptionOutputFormat.MpegTs | SubscriptionOutputFormat.Hls));
        }

        [Fact]
        public void AnOutputFormatSetWithAnUnknownBitIsRefused()
        {
            Assert.False(SubscriptionRules.IsUsableOutputFormats(
                SubscriptionOutputFormat.MpegTs | (SubscriptionOutputFormat)4));
        }

        [Theory]
        // A playlist can only be kept when a playlist is supported.
        [InlineData("id", null, null, true, false, false, true)]
        [InlineData("id", null, null, false, false, false, false)]
        // A MAG can only be kept when a MAG is supported.
        [InlineData(null, "AA:BB:CC:DD:EE:FF", null, false, true, false, true)]
        [InlineData(null, "AA:BB:CC:DD:EE:FF", null, false, false, false, false)]
        // An Enigma2 device, the same way.
        [InlineData(null, null, "AA:BB:CC:DD:EE:FF", false, false, true, true)]
        [InlineData(null, null, "AA:BB:CC:DD:EE:FF", false, false, false, false)]
        // Nothing at all is always no service.
        [InlineData(null, null, null, true, true, true, false)]
        public void ClearingUnsupportedServicesLeavesAServiceOnlyWhenOneSurvives(
            string? playlistId,
            string? magMac,
            string? enigma2Mac,
            bool supportsPlaylist,
            bool supportsMag,
            bool supportsEnigma2,
            bool serviceRemains)
        {
            Assert.Equal(
                !serviceRemains,
                SubscriptionRules.LeavesNoService(
                    playlistId,
                    magMac,
                    enigma2Mac,
                    supportsPlaylist,
                    supportsMag,
                    supportsEnigma2));
        }

        [Fact]
        public void APlanIsOnlyAvailableToAnOwnerWhoseGroupItNames()
        {
            var ownerGroup = Guid.NewGuid();
            var otherGroup = Guid.NewGuid();

            Assert.True(SubscriptionRules.IsAvailableToOwner(
                Plan(1, TariffPlanDurationUnit.Months, [ownerGroup]),
                ownerGroup));

            Assert.False(SubscriptionRules.IsAvailableToOwner(
                Plan(1, TariffPlanDurationUnit.Months, [otherGroup]),
                ownerGroup));
        }

        [Fact]
        public void APlanDerivesEveryDependentFieldFromItsOwnFlags()
        {
            var plan = Plan(
                1,
                TariffPlanDurationUnit.Months,
                [],
                supportsM3u: true,
                supportsMag: false,
                supportsEnigma2: false,
                outputMpegTs: false,
                outputHls: true,
                strictStb: false,
                allowRestream: true,
                maxConnections: 4,
                allowMultipleCountries: false,
                isTrial: true);

            var derived = SubscriptionRules.Derive(plan, Created);

            Assert.Equal(4, derived.MaxConnections);
            Assert.True(derived.IsRestreamer);
            Assert.Equal(SubscriptionOutputFormat.Hls, derived.OutputFormats);
            Assert.False(derived.IsStbLock);
            Assert.True(derived.IsCountryLock);
            Assert.True(derived.IsTrial);
            Assert.True(derived.SupportsPlaylist);
            Assert.False(derived.SupportsMag);
            Assert.False(derived.SupportsEnigma2);
            Assert.Equal(Created.AddMonths(1), derived.ExpireAt);
        }

        /// <summary>
        /// A plan every derivation test starts from, with the values a test overrides.
        /// </summary>
        private static SubscriptionPlanContext Plan(
            int durationValue,
            TariffPlanDurationUnit durationUnit,
            IReadOnlyList<Guid>? userGroupIds = null,
            bool supportsM3u = true,
            bool supportsMag = true,
            bool supportsEnigma2 = true,
            bool outputMpegTs = true,
            bool outputHls = true,
            bool strictStb = false,
            bool allowRestream = false,
            int maxConnections = 2,
            bool allowMultipleCountries = true,
            bool isTrial = false)
            => new()
            {
                Id = Guid.NewGuid(),
                Name = "Plan",
                IsTrial = isTrial,
                DurationValue = durationValue,
                DurationUnit = durationUnit,
                SupportsM3u = supportsM3u,
                SupportsMag = supportsMag,
                SupportsEnigma2 = supportsEnigma2,
                StrictStbDeviceId = strictStb,
                AllowRestream = allowRestream,
                MaxConnections = maxConnections,
                AllowMultipleCountries = allowMultipleCountries,
                OutputMpegTs = outputMpegTs,
                OutputHls = outputHls,
                UserGroupIds = userGroupIds ?? [],
                BouquetIds = []
            };
    }
}
