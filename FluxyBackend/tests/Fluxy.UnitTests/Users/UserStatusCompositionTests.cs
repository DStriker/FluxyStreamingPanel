using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class UserStatusCompositionTests
    {
        [Theory]
        [InlineData(UserStatus.Registered, UserStatus.Registered, UserStatus.Registered)]
        [InlineData(UserStatus.Unregistered, UserStatus.Registered, UserStatus.Unregistered)]
        [InlineData(UserStatus.Registered, UserStatus.Unregistered, UserStatus.Unregistered)]
        [InlineData(UserStatus.Blocked, UserStatus.Registered, UserStatus.Blocked)]
        [InlineData(UserStatus.Registered, UserStatus.Blocked, UserStatus.Blocked)]
        [InlineData(UserStatus.Unregistered, UserStatus.Blocked, UserStatus.Blocked)]
        [InlineData(UserStatus.Blocked, UserStatus.Unregistered, UserStatus.Blocked)]
        public void CombineReturnsTheStricterState(
            UserStatus userStatus,
            UserStatus groupStatus,
            UserStatus expected)
        {
            Assert.Equal(expected, UserStatusComposition.Combine(userStatus, groupStatus));
        }

        [Fact]
        public void CombineIsSymmetric()
        {
            foreach (var userStatus in UserStatusComposition.All)
            {
                foreach (var groupStatus in UserStatusComposition.All)
                {
                    Assert.Equal(
                        UserStatusComposition.Combine(userStatus, groupStatus),
                        UserStatusComposition.Combine(groupStatus, userStatus));
                }
            }
        }

        [Fact]
        public void AllNamesEveryMemberExactlyOnce()
        {
            var expected = Enum.GetValues<UserStatus>();

            Assert.Equal(expected.Length, UserStatusComposition.All.Count);
            Assert.Equal(expected.Order(), UserStatusComposition.All.Order());
        }
    }
}
