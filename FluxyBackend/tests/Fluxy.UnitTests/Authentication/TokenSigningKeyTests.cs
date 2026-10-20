using System.Text;
using Fluxy.Core.Authentication.Utils;

namespace Fluxy.UnitTests.Authentication
{
    public sealed class TokenSigningKeyTests
    {
        [Fact]
        public void MaterializeDecodesAHexadecimalSecret()
        {
            var secret = new string('a', 64);

            var material = TokenSigningKey.Materialize(secret);

            Assert.Equal(32, material.Length);
            Assert.All(material, value => Assert.Equal(0xAA, value));
        }

        [Fact]
        public void MaterializeUsesUtf8ForANonHexadecimalSecret()
        {
            var secret = new string('k', 32);

            var material = TokenSigningKey.Materialize(secret);

            Assert.Equal(Encoding.UTF8.GetBytes(secret), material);
        }

        [Fact]
        public void MaterializeAcceptsExactlyTheMinimumLength()
        {
            Assert.Equal(32, TokenSigningKey.Materialize(new string('x', 32)).Length);
            Assert.Equal(32, TokenSigningKey.Materialize(new string('0', 64)).Length);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MaterializeRefusesAMissingSecret(string? secret)
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TokenSigningKey.Materialize(secret));

            Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MaterializeRefusesASecretShorterThanTheMinimum()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TokenSigningKey.Materialize(new string('x', 31)));

            Assert.Contains("at least 32", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MaterializeReadsShortHexAsHexRatherThanAsText()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TokenSigningKey.Materialize(new string('a', 32)));

            Assert.Contains("16 bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
