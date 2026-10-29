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
            Assert.Equal(0, (int)UserPermission.None);
            Assert.Equal(4, (int)UserPermission.ViewUserGroups);
            Assert.Equal(8, (int)UserPermission.EditUserGroups);
            Assert.Equal(16, (int)UserPermission.ViewClients);
            Assert.Equal(32, (int)UserPermission.ViewResellers);
            Assert.Equal(64, (int)UserPermission.ViewAdmins);
            Assert.Equal(128, (int)UserPermission.EditClients);
            Assert.Equal(256, (int)UserPermission.EditResellers);
            Assert.Equal(512, (int)UserPermission.EditAdmins);
            Assert.Equal(1024, (int)UserPermission.ViewStreams);
            Assert.Equal(2048, (int)UserPermission.EditStreams);
            Assert.Equal(4096, (int)UserPermission.ViewAudioStreams);
            Assert.Equal(8192, (int)UserPermission.EditAudioStreams);
            Assert.Equal(16384, (int)UserPermission.ViewVideoOnDemand);
            Assert.Equal(32768, (int)UserPermission.EditVideoOnDemand);
            Assert.Equal(65536, (int)UserPermission.ViewSeries);
            Assert.Equal(131072, (int)UserPermission.EditSeries);
            Assert.Equal(262144, (int)UserPermission.ViewEpgSources);
            Assert.Equal(524288, (int)UserPermission.EditEpgSources);
            Assert.Equal(1048576, (int)UserPermission.ViewBouquets);
            Assert.Equal(2097152, (int)UserPermission.EditBouquets);
            Assert.Equal(4194304, (int)UserPermission.ViewTariffPlans);
            Assert.Equal(8388608, (int)UserPermission.EditTariffPlans);
            Assert.Equal(16777216, (int)UserPermission.ViewSubscriptions);
            Assert.Equal(33554432, (int)UserPermission.EditSubscriptions);
            Assert.False(Enum.IsDefined((UserPermission)1));
            Assert.False(Enum.IsDefined((UserPermission)2));
        }

        [Fact]
        public void EveryCatalogMemberIsAPowerOfTwo()
        {
            foreach (var permission in UserPermissionCatalog.All)
            {
                var value = (int)permission;

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
            // pair arrived (1 &lt;&lt; 20), again when the tariff plan pair took the next two
            // slots, again when the subscription pair took the two after those, and it moves
            // each time a permission takes one - which is exactly what this line is for.
            Assert.False(UserPermissionCatalog.IsKnown(
                UserPermission.ViewClients | (UserPermission)(1 << 26)));
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
