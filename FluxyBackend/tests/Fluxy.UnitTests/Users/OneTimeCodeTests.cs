using Fluxy.Application.Users.Utils;

namespace Fluxy.UnitTests.Users
{
    public sealed class OneTimeCodeTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        [InlineData(12)]
        public void GenerateReturnsTheRequestedNumberOfDigits(int length)
        {
            var code = OneTimeCode.Generate(length);

            Assert.Equal(length, code.Length);
            Assert.All(code, character => Assert.True(char.IsAsciiDigit(character)));
        }

        [Fact]
        public void GenerateReturnsAnEmptyStringForZeroLength()
        {
            Assert.Equal(string.Empty, OneTimeCode.Generate(0));
        }

        [Fact]
        public void GenerateDoesNotRepeatAcrossCalls()
        {
            var codes = Enumerable.Range(0, 100)
                .Select(_ => OneTimeCode.Generate(6))
                .ToHashSet(StringComparer.Ordinal);

            Assert.True(codes.Count > 90);
        }
    }
}
