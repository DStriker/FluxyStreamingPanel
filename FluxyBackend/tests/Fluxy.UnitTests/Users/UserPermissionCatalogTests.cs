using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class UserPermissionCatalogTests
    {
        [Fact]
        public void EveryCatalogMemberHasExactlyOneOwnerRole()
        {
            foreach (var permission in UserPermissionCatalog.All)
            {
                var owners = Enum.GetValues<UserRole>()
                    .Where(role => UserPermissionCatalog.IsValidForRole(permission, role))
                    .ToArray();

                Assert.Single(owners);
            }
        }

        [Fact]
        public void AdministratorBaseGroupOwnsEveryPermission()
        {
            Assert.Equal(UserPermissionCatalog.All, UserPermissionCatalog.ForRole(UserRole.Admin));
        }

        [Fact]
        public void TheTariffPlanPairBelongsToTheAdministratorBaseGroup()
        {
            // The seeder reconciles the base groups against this catalog on every start, and the
            // migration `GrantTariffPlanPermissions` backfills the same pair for an installation
            // that has been migrated but not yet restarted. Both decisions rest on this one: the
            // pair is owned by Admin, so it is granted to the Administrators group and to nobody
            // else.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewTariffPlans, admin);
            Assert.Contains(UserPermission.EditTariffPlans, admin);

            Assert.DoesNotContain(
                UserPermission.ViewTariffPlans,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.EditTariffPlans,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheBlacklistPairBelongsToTheAdministratorBaseGroup()
        {
            // One pair covers all three lists because they are one resource behind one API and
            // one screen family. The seeder reconciles the base groups against this catalog on
            // every start, and `AddBlacklistEntries` backfills the same pair for an installation
            // that has been migrated but not yet restarted. Both decisions rest on this one: the
            // pair is owned by Admin, so it is granted to the Administrators group and to nobody
            // else.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewBlacklists, admin);
            Assert.Contains(UserPermission.EditBlacklists, admin);

            Assert.DoesNotContain(
                UserPermission.ViewBlacklists,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.EditBlacklists,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheUserLogReadGrantBelongsToTheAdministratorBaseGroup()
        {
            // The log is written by the flows it describes, never by an operator, so the catalog
            // names one read grant and no write one. The seeder reconciles the base groups
            // against this catalog on every start - which is why no backfill migration
            // accompanies the member - so the grant reaches the Administrators group without a
            // migration and without a frontend permission mirror changing.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewUserLogs, admin);

            Assert.DoesNotContain(
                UserPermission.ViewUserLogs,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.ViewUserLogs,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheApiRequestFailureReadGrantBelongsToTheAdministratorBaseGroup()
        {
            // One grant covers both the read and the whole-table clear, for the argument the
            // user-log grant records: the two destroy the same evidence, and there is no write
            // permission because a refusal row is written by the public API's future capture path.
            // The seeder reconciles the base groups against this catalog on every start, so no
            // backfill migration accompanies the member.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewApiRequestFailures, admin);

            Assert.DoesNotContain(
                UserPermission.ViewApiRequestFailures,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.ViewApiRequestFailures,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheServerPairBelongsToTheAdministratorBaseGroup()
        {
            // A server is neither content nor an account, so it carries a pair of its own. The
            // seeder reconciles the base groups against this catalog on every start, so the pair
            // reaches the Administrators group without a backfill migration - the reason no
            // migration accompanies it, as with every addition since the transcoding pair.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewServers, admin);
            Assert.Contains(UserPermission.EditServers, admin);

            Assert.DoesNotContain(
                UserPermission.ViewServers,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.EditServers,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheServerPairIsKnownAndParsesFromItsWireNames()
        {
            Assert.True(UserPermissionCatalog.IsKnown(UserPermission.ViewServers));
            Assert.True(UserPermissionCatalog.IsKnown(UserPermission.EditServers));
            Assert.True(UserPermissionCatalog.IsKnown(
                UserPermission.ViewServers | UserPermission.EditServers));

            Assert.True(UserPermissionCatalog.TryParse("viewServers", out var view));
            Assert.Equal(UserPermission.ViewServers, view);

            Assert.True(UserPermissionCatalog.TryParse("editServers", out var edit));
            Assert.Equal(UserPermission.EditServers, edit);
        }

        [Fact]
        public void TheSettingsPairBelongsToTheAdministratorBaseGroup()
        {
            // The seeder reconciles the base groups against this catalog on every start, and the
            // migration `GrantSettingsPermissions` backfills the same pair for an installation
            // that has been migrated but not yet restarted. Both decisions rest on this one: the
            // pair is owned by Admin, so it is granted to the Administrators group and to nobody
            // else.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewSettings, admin);
            Assert.Contains(UserPermission.EditSettings, admin);

            Assert.DoesNotContain(
                UserPermission.ViewSettings,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.EditSettings,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheSettingsPairIsKnownAndParsesFromItsWireNames()
        {
            Assert.True(UserPermissionCatalog.IsKnown(UserPermission.ViewSettings));
            Assert.True(UserPermissionCatalog.IsKnown(UserPermission.EditSettings));
            Assert.True(UserPermissionCatalog.IsKnown(
                UserPermission.ViewSettings | UserPermission.EditSettings));

            Assert.True(UserPermissionCatalog.TryParse("viewSettings", out var view));
            Assert.Equal(UserPermission.ViewSettings, view);

            Assert.True(UserPermissionCatalog.TryParse("editSettings", out var edit));
            Assert.Equal(UserPermission.EditSettings, edit);
        }

        [Fact]
        public void ClientAndResellerBaseGroupsOwnNoPermissions()
        {
            Assert.Empty(UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.Empty(UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void TheSubscriptionPairBelongsToTheAdministratorBaseGroup()
        {
            // The seeder reconciles the base groups against this catalog on every start, and the
            // migration `GrantSubscriptionPermissions` backfills the same pair for an installation
            // that has been migrated but not yet restarted. Both decisions rest on this one: the
            // pair is owned by Admin, so it is granted to the Administrators group and to nobody
            // else.
            var admin = UserPermissionCatalog.ForRole(UserRole.Admin);

            Assert.Contains(UserPermission.ViewSubscriptions, admin);
            Assert.Contains(UserPermission.EditSubscriptions, admin);

            Assert.DoesNotContain(
                UserPermission.ViewSubscriptions,
                UserPermissionCatalog.ForRole(UserRole.Client));
            Assert.DoesNotContain(
                UserPermission.EditSubscriptions,
                UserPermissionCatalog.ForRole(UserRole.Reseller));
        }

        [Fact]
        public void NumericValuesKeepTheSchemaAndRetiredSlotsFree()
        {
            Assert.Equal(0, (long)UserPermission.None);
            Assert.Equal(4, (long)UserPermission.ViewUserGroups);
            Assert.Equal(8, (long)UserPermission.EditUserGroups);
            Assert.Equal(16, (long)UserPermission.ViewClients);
            Assert.Equal(32, (long)UserPermission.ViewResellers);
            Assert.Equal(64, (long)UserPermission.ViewAdmins);
            Assert.Equal(128, (long)UserPermission.EditClients);
            Assert.Equal(256, (long)UserPermission.EditResellers);
            Assert.Equal(512, (long)UserPermission.EditAdmins);
            Assert.Equal(1024, (long)UserPermission.ViewStreams);
            Assert.Equal(2048, (long)UserPermission.EditStreams);
            Assert.Equal(4096, (long)UserPermission.ViewAudioStreams);
            Assert.Equal(8192, (long)UserPermission.EditAudioStreams);
            Assert.Equal(16384, (long)UserPermission.ViewVideoOnDemand);
            Assert.Equal(32768, (long)UserPermission.EditVideoOnDemand);
            Assert.Equal(65536, (long)UserPermission.ViewSeries);
            Assert.Equal(131072, (long)UserPermission.EditSeries);
            Assert.Equal(262144, (long)UserPermission.ViewEpgSources);
            Assert.Equal(524288, (long)UserPermission.EditEpgSources);
            Assert.Equal(1048576, (long)UserPermission.ViewBouquets);
            Assert.Equal(2097152, (long)UserPermission.EditBouquets);
            Assert.Equal(4194304, (long)UserPermission.ViewTariffPlans);
            Assert.Equal(8388608, (long)UserPermission.EditTariffPlans);
            Assert.Equal(16777216, (long)UserPermission.ViewSubscriptions);
            Assert.Equal(33554432, (long)UserPermission.EditSubscriptions);
            Assert.Equal(67108864, (long)UserPermission.ViewBlacklists);
            Assert.Equal(134217728, (long)UserPermission.EditBlacklists);
            Assert.Equal(1L << 30, (long)UserPermission.ViewTranscodingProfiles);
            Assert.Equal(1L << 31, (long)UserPermission.EditTranscodingProfiles);
            Assert.Equal(268435456, (long)UserPermission.ViewUserLogs);
            Assert.Equal(536870912, (long)UserPermission.ViewApiRequestFailures);
            Assert.Equal(1L << 32, (long)UserPermission.ViewServers);
            Assert.Equal(1L << 33, (long)UserPermission.EditServers);
            Assert.Equal(1L << 34, (long)UserPermission.ViewSettings);
            Assert.Equal(1L << 35, (long)UserPermission.EditSettings);
            Assert.Equal(17179869184, (long)UserPermission.ViewSettings);
            Assert.Equal(34359738368, (long)UserPermission.EditSettings);
            Assert.False(Enum.IsDefined((UserPermission)1));
            Assert.False(Enum.IsDefined((UserPermission)2));
        }

        [Fact]
        public void EveryCatalogMemberIsAPowerOfTwo()
        {
            foreach (var permission in UserPermissionCatalog.All)
            {
                var value = (long)permission;

                Assert.True(value > 0);
                Assert.Equal(0, value & (value - 1));
            }
        }

        [Theory]
        [InlineData(UserRole.Client)]
        [InlineData(UserRole.Reseller)]
        [InlineData(UserRole.Admin)]
        public void ViewAndEditForMapEachRoleToItsAccountPermission(UserRole role)
        {
            Assert.True(UserPermissionCatalog.IsValidForRole(
                UserPermissionCatalog.ViewFor(role),
                UserRole.Admin));
            Assert.True(UserPermissionCatalog.IsValidForRole(
                UserPermissionCatalog.EditFor(role),
                UserRole.Admin));
        }

        [Fact]
        public void CanViewAndCanEditReadOnlyTheMatchingRoleKey()
        {
            UserPermission[] granted = [UserPermission.ViewClients, UserPermission.EditResellers];

            Assert.True(UserPermissionCatalog.CanView(granted, UserRole.Client));
            Assert.False(UserPermissionCatalog.CanView(granted, UserRole.Reseller));
            Assert.True(UserPermissionCatalog.CanEdit(granted, UserRole.Reseller));
            Assert.False(UserPermissionCatalog.CanEdit(granted, UserRole.Client));
        }

        [Fact]
        public void VisibleRolesContainsOnlyRolesWithAViewPermission()
        {
            UserPermission[] granted =
            [
                UserPermission.ViewClients,
                UserPermission.ViewAdmins,
                UserPermission.EditResellers
            ];

            var visible = UserPermissionCatalog.VisibleRoles(granted);

            Assert.Equal([UserRole.Client, UserRole.Admin], visible.Order());
        }

        [Fact]
        public void IsKnownAcceptsNoneAndRejectsUnknownFlags()
        {
            Assert.True(UserPermissionCatalog.IsKnown(UserPermission.None));
            Assert.True(UserPermissionCatalog.IsKnown(
                UserPermission.ViewClients | UserPermission.EditStreams));
            Assert.False(UserPermissionCatalog.IsKnown((UserPermission)1));

            // A bit above the highest member the catalog names. It moved up when the bouquet
            // pair arrived (1 << 20), again when the tariff plan pair took the next two
            // slots, again when the subscription pair took the two after those, again when the
            // blacklist pair took 2^26 and 2^27, again when the user-log read grant took
            // 2^28, again when the API request failure read grant took 2^29, again when the
            // transcoding pair took 2^30 and 2^31 - the widening to long is what made the
            // second of those two fit - and again when the server pair took 2^32 and 2^33 - and
            // again when the settings pair took 2^34 and 2^35. It moves each time a permission
            // takes one, which is exactly what this line is for.
            Assert.False(UserPermissionCatalog.IsKnown(
                UserPermission.ViewClients | (UserPermission)(1L << 36)));
        }

        [Fact]
        public void NormaliseDropsGrantsTheRoleCannotHold()
        {
            var normalised = UserPermissionCatalog.Normalise(
                [UserPermission.ViewClients, UserPermission.EditStreams],
                UserRole.Client);

            Assert.Equal(UserPermission.None, normalised);
        }

        [Theory]
        [InlineData(UserPermission.ViewClients, "viewClients")]
        [InlineData(UserPermission.EditAdmins, "editAdmins")]
        [InlineData(UserPermission.ViewStreams, "viewStreams")]
        [InlineData(UserPermission.None, "none")]
        public void NameOfLowerCasesTheFirstLetter(UserPermission permission, string expected)
        {
            Assert.Equal(expected, UserPermissionCatalog.NameOf(permission));
        }

        [Theory]
        [InlineData("viewClients", UserPermission.ViewClients)]
        [InlineData("ViewClients", UserPermission.ViewClients)]
        [InlineData(" viewstreams ", UserPermission.ViewStreams)]
        public void TryParseAcceptsCatalogNamesCaseInsensitively(string value, UserPermission expected)
        {
            Assert.True(UserPermissionCatalog.TryParse(value, out var permission));
            Assert.Equal(expected, permission);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("viewEverything")]
        [InlineData("1")]
        public void TryParseRejectsNamesOutsideTheCatalog(string? value)
        {
            Assert.False(UserPermissionCatalog.TryParse(value, out var permission));
            Assert.Equal(UserPermission.None, permission);
        }

        [Fact]
        public void UnknownPermissionHintNamesEveryCatalogMember()
        {
            foreach (var permission in UserPermissionCatalog.All)
            {
                Assert.Contains(
                    UserPermissionCatalog.NameOf(permission),
                    UserPermissionCatalog.UnknownPermissionHint,
                    StringComparison.Ordinal);
            }
        }
    }
}
