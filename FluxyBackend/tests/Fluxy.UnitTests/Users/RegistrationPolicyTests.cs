using Fluxy.Application.Users.Utils;
using Fluxy.Core.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class RegistrationPolicyTests
    {
        [Theory]
        [InlineData("user@example.com")]
        [InlineData("first.last+tag@sub.example.co.uk")]
        [InlineData("a@b.cd")]
        public void IsEmailShapeValidAcceptsOrdinaryAddresses(string email)
        {
            Assert.True(RegistrationPolicy.IsEmailShapeValid(email));
        }

        [Theory]
        [InlineData("")]
        [InlineData("user")]
        [InlineData("@example.com")]
        [InlineData("user@example")]
        [InlineData("user@.com")]
        [InlineData("user@example..com")]
        [InlineData("user name@example.com")]
        [InlineData("user@@example.com")]
        public void IsEmailShapeValidRejectsMalformedAddresses(string email)
        {
            Assert.False(RegistrationPolicy.IsEmailShapeValid(email));
        }

        [Fact]
        public void IsEmailShapeValidRefusesAnAddressLongerThanTheColumn()
        {
            var localPart = new string('a', RegistrationLimits.EmailMaxLength);
            var email = $"{localPart}@example.com";

            Assert.False(RegistrationPolicy.IsEmailShapeValid(email));
        }

        [Theory]
        [InlineData("Password1")]
        [InlineData("aA1bbbbb")]
        [InlineData("UPPERlower123")]
        public void IsPasswordAcceptableAcceptsTheRequiredComplexity(string password)
        {
            Assert.True(RegistrationPolicy.IsPasswordAcceptable(password));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Aa1")]
        [InlineData("password1")]
        [InlineData("PASSWORD1")]
        [InlineData("Password")]
        [InlineData("Password ")]
        public void IsPasswordAcceptableRejectsMissingComplexity(string password)
        {
            Assert.False(RegistrationPolicy.IsPasswordAcceptable(password));
        }

        [Fact]
        public void IsPasswordAcceptableEnforcesTheLengthBounds()
        {
            Assert.False(RegistrationPolicy.IsPasswordAcceptable(
                "Aa1" + new string('x', RegistrationLimits.PasswordMinLength - 4)));
            Assert.True(RegistrationPolicy.IsPasswordAcceptable(
                "Aa1" + new string('x', RegistrationLimits.PasswordMinLength - 3)));
            Assert.True(RegistrationPolicy.IsPasswordAcceptable(
                "Aa1" + new string('x', RegistrationLimits.PasswordMaxLength - 3)));
            Assert.False(RegistrationPolicy.IsPasswordAcceptable(
                "Aa1" + new string('x', RegistrationLimits.PasswordMaxLength - 2)));
        }
    }
}
