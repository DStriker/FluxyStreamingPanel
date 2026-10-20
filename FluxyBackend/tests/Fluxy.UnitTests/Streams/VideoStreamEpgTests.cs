using Fluxy.Core.Content.Enums;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Utils;

namespace Fluxy.UnitTests.Streams
{
    public sealed class VideoStreamEpgTests
    {
        [Fact]
        public void ConfiguredRequiresBothHalves()
        {
            var source = Guid.NewGuid();

            Assert.True(VideoStreamEpg.Configured(source, "channel-1"));
            Assert.False(VideoStreamEpg.Configured(source, null));
            Assert.False(VideoStreamEpg.Configured(source, ""));
            Assert.False(VideoStreamEpg.Configured(source, "   "));
            Assert.False(VideoStreamEpg.Configured(null, "channel-1"));
            Assert.False(VideoStreamEpg.Configured(null, null));
        }

        [Fact]
        public void FixedEnumsKeepTheirStoredValues()
        {
            Assert.Equal(0, (int)VideoStreamStatus.Stopped);
            Assert.Equal(1, (int)VideoStreamStatus.Running);
            Assert.Equal(1, (int)VideoStreamType.Permanent);
            Assert.Equal(2, (int)VideoStreamType.OnDemand);
            Assert.Equal(3, (int)VideoStreamType.Redirect);
            Assert.Equal(4, (int)VideoStreamType.OnlineSeries);
            Assert.Equal(5, (int)VideoStreamType.VodPlaylist);
            Assert.Equal(6, (int)VideoStreamType.FilePlaylist);
            Assert.Equal(1, (int)ContentType.VideoStream);
            Assert.Equal(2, (int)ContentType.AudioStream);
            Assert.Equal(3, (int)ContentType.VideoOnDemand);
            Assert.Equal(4, (int)ContentType.Series);
        }
    }
}
