using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class BaseUserGroupsTests
    {
        [Fact]
        public void IdentifiersAreFixedAndDistinct()
        {
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), BaseUserGroups.Clients);
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), BaseUserGroups.Resellers);
            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000003"), BaseUserGroups.Administrators);
            Assert.Equal(3, BaseUserGroups.All.Distinct().Count());
        }

        [Theory]
        [InlineData(UserRole.Client)]
        [InlineData(UserRole.Reseller)]
        [InlineData(UserRole.Admin)]
        public void ForRoleReturnsTheMatchingBaseGroup(UserRole role)
        {
            var id = BaseUserGroups.ForRole(role);

            Assert.True(BaseUserGroups.IsBase(id));
            Assert.Equal(id, BaseUserGroups.ForRole(role));
        }

        [Theory]
        [InlineData(UserRole.Client, "Clients")]
        [InlineData(UserRole.Reseller, "Resellers")]
        [InlineData(UserRole.Admin, "Administrators")]
        public void NameOfReturnsTheStartingName(UserRole role, string expected)
        {
            Assert.Equal(expected, BaseUserGroups.NameOf(role));
        }

        [Fact]
        public void IsBaseRejectsOrdinaryAndEmptyIdentifiers()
        {
            Assert.False(BaseUserGroups.IsBase(Guid.NewGuid()));
            Assert.False(BaseUserGroups.IsBase(Guid.Empty));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(-1)]
        public void ForRoleAndNameOfRejectValuesOutsideTheEnum(int value)
        {
            var role = (UserRole)value;

            Assert.Throws<ArgumentOutOfRangeException>(() => BaseUserGroups.ForRole(role));
            Assert.Throws<ArgumentOutOfRangeException>(() => BaseUserGroups.NameOf(role));
        }
    }
}
