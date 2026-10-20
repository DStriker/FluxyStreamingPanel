using Fluxy.Application.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class EmailNormalizerTests
    {
        [Theory]
        [InlineData("User@Example.COM", "user@example.com")]
        [InlineData("  user@example.com  ", "user@example.com")]
        [InlineData("USER@EXAMPLE.COM", "user@example.com")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void NormalizeTrimsAndFoldsToLowerCase(string? input, string expected)
        {
            Assert.Equal(expected, EmailNormalizer.Normalize(input));
        }
    }
}
