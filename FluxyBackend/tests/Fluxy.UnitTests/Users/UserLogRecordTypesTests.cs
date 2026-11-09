using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    /// <summary>
    /// The wire vocabulary of the user log: each record type has exactly one snake_case name,
    /// and that name is the only spelling a filter accepts.
    /// </summary>
    /// <remarks>
    /// The wire name is what a query string and a response carry, while the enum member is what
    /// the database stores. A name added on one side and forgotten on the other would make a
    /// filter silently refuse a value the screen offers, which is why the round trip is pinned
    /// for all ten members rather than for a representative two.
    /// </remarks>
    public sealed class UserLogRecordTypesTests
    {
        [Theory]
        [InlineData(UserLogRecordType.RegistrationSubmit, "registration_submit")]
        [InlineData(UserLogRecordType.RegistrationConfirm, "registration_confirm")]
        [InlineData(UserLogRecordType.Login, "login")]
        [InlineData(UserLogRecordType.PasswordResetRequest, "password_reset_request")]
        [InlineData(UserLogRecordType.PasswordResetConfirm, "password_reset_confirm")]
        [InlineData(UserLogRecordType.UsernameChange, "username_change")]
        [InlineData(UserLogRecordType.EmailChange, "email_change")]
        [InlineData(UserLogRecordType.PasswordChange, "password_change")]
        [InlineData(UserLogRecordType.LoginGuardChange, "login_guard_change")]
        [InlineData(UserLogRecordType.Logout, "logout")]
        public void EveryMemberHasOneWireNameAndParsesBackToItself(
            UserLogRecordType recordType,
            string wire)
        {
            Assert.Equal(wire, UserLogRecordTypes.WireName(recordType));

            Assert.True(UserLogRecordTypes.TryParseWire(wire, out var parsed));
            Assert.Equal(recordType, parsed);
        }

        [Theory]
        [InlineData("REGISTRATION_SUBMIT", UserLogRecordType.RegistrationSubmit)]
        [InlineData(" Login ", UserLogRecordType.Login)]
        [InlineData("logout", UserLogRecordType.Logout)]
        [InlineData("Password_Reset_Request", UserLogRecordType.PasswordResetRequest)]
        public void AWireNameIsReadCaseInsensitivelyAndTrimmed(
            string value,
            UserLogRecordType expected)
        {
            Assert.True(UserLogRecordTypes.TryParseWire(value, out var parsed));
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("registration")]
        [InlineData("registration-submit")]
        [InlineData("RegistrationSubmit")]
        [InlineData("login failed")]
        public void AnythingElseNamesNoRecordType(string? value)
        {
            Assert.False(UserLogRecordTypes.TryParseWire(value, out var parsed));
            Assert.Equal(default, parsed);
        }

        [Fact]
        public void TheCatalogHoldsExactlyTheTenMembersInSchemaOrder()
        {
            Assert.Equal(
                [
                    UserLogRecordType.RegistrationSubmit,
                    UserLogRecordType.RegistrationConfirm,
                    UserLogRecordType.Login,
                    UserLogRecordType.PasswordResetRequest,
                    UserLogRecordType.PasswordResetConfirm,
                    UserLogRecordType.UsernameChange,
                    UserLogRecordType.EmailChange,
                    UserLogRecordType.PasswordChange,
                    UserLogRecordType.LoginGuardChange,
                    UserLogRecordType.Logout
                ],
                UserLogRecordTypes.All);

            Assert.Equal(UserLogRecordTypes.All, Enum.GetValues<UserLogRecordType>());
        }

        [Fact]
        public void AValueThatNamesNoMemberHasNoWireName()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => UserLogRecordTypes.WireName((UserLogRecordType)99));
        }

        [Fact]
        public void TheHintNamesEveryWireValue()
        {
            foreach (var recordType in UserLogRecordTypes.All)
            {
                Assert.Contains(
                    UserLogRecordTypes.WireName(recordType),
                    UserLogRecordTypes.UnknownRecordTypeHint,
                    StringComparison.Ordinal);
            }
        }
    }
}
