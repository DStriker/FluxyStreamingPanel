using System.Net;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Platform
{
    /// <summary>
    /// The ceiling every server-paged table shares.
    /// </summary>
    /// <remarks>
    /// The tables offer 500 and 1000 as page sizes, and the selection a bulk operation runs over
    /// is the rows of one page. The number is therefore accepted by every list rather than only
    /// by the streams list - a table whose pager offers a size its endpoint refuses is a button
    /// that answers with a validation error.
    /// </remarks>
    public sealed class PageSizeLimitTests : IntegrationTestBase
    {
        public PageSizeLimitTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Theory]
        [InlineData("/admin/users")]
        [InlineData("/admin/user-groups")]
        [InlineData("/admin/video-streams")]
        [InlineData("/admin/sessions")]
        [InlineData("/auth/sessions")]
        public async Task APageOfAThousandIsAcceptedAndOneMoreIsRefused(string path)
        {
            await CreateAdminAndSignInAsync();

            using var allowed = await Client.GetAsync(
                $"{path}?pageSize=1000",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

            using var refused = await Client.GetAsync(
                $"{path}?pageSize=1001",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

            using var body = await ReadJsonAsync(refused);
            Assert.Equal("validation_failed", CodeOf(body));
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));
        }
    }
}
