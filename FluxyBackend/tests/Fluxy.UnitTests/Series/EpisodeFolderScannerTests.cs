using Fluxy.Application.Series.Utils;

namespace Fluxy.UnitTests.Series
{
    public sealed class EpisodeFolderScannerTests
    {
        [Theory]
        [InlineData("episode.mp4", true)]
        [InlineData("episode.MKV", true)]
        [InlineData("episode.m2ts", true)]
        [InlineData("cover.jpg", false)]
        [InlineData("notes.txt", false)]
        [InlineData("episode", false)]
        public void IsEpisodeFileReadsTheExtensionAllowList(string fileName, bool expected)
        {
            Assert.Equal(expected, EpisodeFolderScanner.IsEpisodeFile(fileName));
        }

        [Fact]
        public void ScanReadsTheTopLevelInNaturalOrderAndIgnoresOtherFiles()
        {
            var folder = Path.Combine(Path.GetTempPath(), "fluxy-series-" + Guid.NewGuid());

            try
            {
                Directory.CreateDirectory(folder);
                Directory.CreateDirectory(Path.Combine(folder, "extras"));

                File.WriteAllText(Path.Combine(folder, "Episode 10.mp4"), string.Empty);
                File.WriteAllText(Path.Combine(folder, "Episode 2.mkv"), string.Empty);
                File.WriteAllText(Path.Combine(folder, "Episode 1.avi"), string.Empty);
                File.WriteAllText(Path.Combine(folder, "cover.jpg"), string.Empty);
                File.WriteAllText(Path.Combine(folder, "notes.txt"), string.Empty);
                File.WriteAllText(Path.Combine(folder, "extras", "Episode 3.mp4"), string.Empty);

                var files = EpisodeFolderScanner.Scan(folder);

                Assert.Equal(
                    new[] { "Episode 1.avi", "Episode 2.mkv", "Episode 10.mp4" },
                    files.Select(Path.GetFileName).ToArray());
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }
    }
}
