using Fluxy.Core.Series.Utils;

namespace Fluxy.UnitTests.Series
{
    public sealed class NaturalStringComparerTests
    {
        [Fact]
        public void NumbersAreComparedAsNumbers()
        {
            var names = new[] { "Episode 10.mp4", "Episode 2.mp4", "Episode 1.mp4" };

            var sorted = names
                .OrderBy(name => name, NaturalStringComparer.Instance)
                .ToArray();

            Assert.Equal(
                new[] { "Episode 1.mp4", "Episode 2.mp4", "Episode 10.mp4" },
                sorted);
        }

        [Fact]
        public void ComparisonIsCaseInsensitiveWithAStableOrdinalTieBreak()
        {
            var names = new[] { "b.mp4", "A.mp4", "a.mp4" };

            var sorted = names
                .OrderBy(name => name, NaturalStringComparer.Instance)
                .ToArray();

            Assert.Equal(new[] { "A.mp4", "a.mp4", "b.mp4" }, sorted);
        }

        [Fact]
        public void LeadingZeroesDoNotChangeTheNumericValue()
        {
            var names = new[] { "S01E010.mp4", "S01E002.mp4", "S01E1.mp4" };

            var sorted = names
                .OrderBy(name => name, NaturalStringComparer.Instance)
                .ToArray();

            Assert.Equal(
                new[] { "S01E1.mp4", "S01E002.mp4", "S01E010.mp4" },
                sorted);
        }
    }
}
