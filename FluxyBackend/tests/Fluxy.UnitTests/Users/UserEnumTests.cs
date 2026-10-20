using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class UserEnumTests
    {
        [Fact]
        public void RoleValuesKeepTheirStoredOrder()
        {
            Assert.Equal(1, (int)UserRole.Client);
            Assert.Equal(2, (int)UserRole.Reseller);
            Assert.Equal(3, (int)UserRole.Admin);
            Assert.True(UserRole.Client < UserRole.Reseller);
            Assert.True(UserRole.Reseller < UserRole.Admin);
        }

        [Fact]
        public void StatusValuesAreStatesRatherThanLevels()
        {
            Assert.Equal(0, (int)UserStatus.Unregistered);
            Assert.Equal(1, (int)UserStatus.Registered);
            Assert.Equal(2, (int)UserStatus.Blocked);
            Assert.Equal(
                UserStatus.Blocked,
                UserStatusComposition.Combine(UserStatus.Registered, UserStatus.Blocked));
        }
    }
}
