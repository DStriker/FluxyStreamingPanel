using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Content
{
    /// <summary>
    /// The mass source replacement over HTTP: one run across four playback domains, the two
    /// input refusals, the per-entity refusals, the grant fence and the row identity the run
    /// must preserve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The endpoint is the administrative shortcut an operator uses to move every playback
    /// address from one host to another, so the tests are written against the consequences
    /// rather than the implementation: what each of the four tables holds afterwards, which
    /// rows were deliberately left alone, and what the answer counts.
    /// </para>
    /// <para>
    /// Rows are planted through the Core repository interfaces - never an entity or a context -
    /// which is the rule the test project follows. The two read-only SQL fixtures that inspect a
    /// source row's identifier, position and audit columns are the deliberate exception: those
    /// facts are not reachable through the API, and the in-place claim is the whole point of the
    /// feature.
    /// </para>
    /// </remarks>
    public sealed class SourceReplacementTests : IntegrationTestBase
    {
        public SourceReplacementTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task OneRunReplacesEveryDomainInTheStableOrder()
        {
            await CreateAdminAndSignInAsync();

            var streamId = await SeedVideoStreamAsync(
                "News",
                ["http://old.test/live/news"]);

            var audioId = await SeedAudioStreamAsync(
                "Radio",
                ["http://old.test/audio/radio"]);

            var vodId = await SeedVodAsync("Film", "http://old.test/film.mp4");

            var (_, episodeId) = await SeedEpisodeAsync(
                "Show",
                "First",
                "http://old.test/show/s1e1.mp4");

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);

            Assert.Equal(4, root.GetProperty("updated").GetInt32());
            Assert.Equal(4, root.GetProperty("domains").GetArrayLength());

            Assert.Equal(
                new[]
                {
                    "videoStreams",
                    "audioStreams",
                    "videoOnDemand",
                    "seriesEpisodes"
                },
                root.GetProperty("domains").EnumerateArray()
                    .Select(domain => domain.GetProperty("domain").GetString())
                    .ToArray());

            Assert.All(
                root.GetProperty("domains").EnumerateArray(),
                domain => Assert.Equal(1, domain.GetProperty("updated").GetInt32()));

            Assert.Equal(
                root.GetProperty("updated").GetInt32(),
                root.GetProperty("domains").EnumerateArray()
                    .Sum(domain => domain.GetProperty("updated").GetInt32()));

            Assert.Equal(0, root.GetProperty("refusals").GetArrayLength());

            await using var connection = await Fixture.OpenConnectionAsync();
            var token = TestContext.Current.CancellationToken;

            var videoRows = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, streamId, token);
            var audioRows = await TestDataSeeder.ReadAudioSourceRowsAsync(connection, audioId, token);
            var vod = await TestDataSeeder.ReadVodSourceAsync(connection, vodId, token);
            var episode = await TestDataSeeder.ReadEpisodeSourceAsync(connection, episodeId, token);

            Assert.Equal("http://new.test/live/news", Assert.Single(videoRows).Url);
            Assert.Equal("http://new.test/audio/radio", Assert.Single(audioRows).Url);
            Assert.Equal("http://new.test/film.mp4", vod.Source);
            Assert.Equal("http://new.test/show/s1e1.mp4", episode.Source);
        }

        [Fact]
        public async Task AMatchlessRunIsStillASuccessWithFourEmptyCounts()
        {
            await CreateAdminAndSignInAsync();
            await SeedVideoStreamAsync("News", ["http://old.test/live"]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "nothing-holds-this", replace = "x" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);

            Assert.Equal(0, root.GetProperty("updated").GetInt32());
            Assert.Equal(0, root.GetProperty("skipped").GetInt32());
            Assert.Equal(4, root.GetProperty("domains").GetArrayLength());
            Assert.Equal(0, root.GetProperty("refusals").GetArrayLength());
        }

        [Fact]
        public async Task TheSearchIsOrdinalAndCaseSensitiveAndEveryOccurrenceIsReplaced()
        {
            await CreateAdminAndSignInAsync();

            var streamId = await SeedVideoStreamAsync(
                "News",
                [
                    "http://Host.test/a/Host.test",
                    "http://host.test/Host"
                ]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "Host", replace = "Other" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            await using var connection = await Fixture.OpenConnectionAsync();
            var rows = await TestDataSeeder.ReadVideoSourceRowsAsync(
                connection,
                streamId,
                TestContext.Current.CancellationToken);

            // Every occurrence on a line, and nothing in the lower-case host: an ordinal,
            // case-sensitive replacement is the contract, not a case-insensitive convenience.
            Assert.Equal("http://Other.test/a/Other.test", rows[0].Url);
            Assert.Equal("http://host.test/Other", rows[1].Url);
        }

        [Fact]
        public async Task ABlankSearchIsAValidationRefusalAndTouchesNothing()
        {
            await CreateAdminAndSignInAsync();
            var streamId = await SeedVideoStreamAsync("News", ["http://old.test/live"]);
            var before = await ReadVideoRowsAsync(streamId);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "   ", replace = "new" },
                csrf);

            await AssertValidationAsync(response, "find");

            var after = await ReadVideoRowsAsync(streamId);

            Assert.Equal(before.Select(row => row.Url), after.Select(row => row.Url));
            Assert.Equal(before.Select(row => row.UpdatedAt), after.Select(row => row.UpdatedAt));
        }

        [Fact]
        public async Task AReplacementEqualToTheSearchIsAValidationRefusal()
        {
            await CreateAdminAndSignInAsync();
            var streamId = await SeedVideoStreamAsync("News", ["http://old.test/live"]);
            var before = await ReadVideoRowsAsync(streamId);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "old.test" },
                csrf);

            await AssertValidationAsync(response, "replace");

            var after = await ReadVideoRowsAsync(streamId);

            Assert.Equal(before.Select(row => row.Url), after.Select(row => row.Url));
        }

        [Fact]
        public async Task ABodyThatBreaksBothRulesNamesBothFields()
        {
            await CreateAdminAndSignInAsync();
            var streamId = await SeedVideoStreamAsync("News", ["http://old.test/live"]);
            var before = await ReadVideoRowsAsync(streamId);

            var csrf = await GetCsrfTokenAsync();

            // An absent replacement reads as the empty string, so a blank search and a
            // replacement equal to it are both true of this body.
            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "", replace = "" },
                csrf);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var root = await ReadRootAsync(response);
            Assert.Equal("validation_failed", root.GetProperty("code").GetString());

            var errors = root.GetProperty("errors");
            Assert.True(errors.TryGetProperty("find", out _), errors.ToString());
            Assert.True(errors.TryGetProperty("replace", out _), errors.ToString());

            var after = await ReadVideoRowsAsync(streamId);
            Assert.Equal(before.Select(row => row.Url), after.Select(row => row.Url));
        }

        [Fact]
        public async Task ARequestWithoutTheAntiforgeryPairIsRefusedAndTouchesNothing()
        {
            await CreateAdminAndSignInAsync();
            var streamId = await SeedVideoStreamAsync("News", ["http://old.test/live"]);
            var before = await ReadVideoRowsAsync(streamId);

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");

            var after = await ReadVideoRowsAsync(streamId);
            Assert.Equal(before.Select(row => row.Url), after.Select(row => row.Url));
        }

        [Fact]
        public async Task AMissingAntiforgeryPairIsAnsweredBeforeTheBodyRulesAreJudged()
        {
            await CreateAdminAndSignInAsync();
            var streamId = await SeedVideoStreamAsync("News", ["http://old.test/live"]);
            var before = await ReadVideoRowsAsync(streamId);

            // The body is well-formed JSON with a blank search, so model binding answers
            // neither path: the only two things that could speak are the antiforgery check and
            // the service's own rules. A refusal naming csrf_invalid is what proves the pair is
            // demanded before those rules rather than after them.
            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "   ", replace = "new" });

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");

            var after = await ReadVideoRowsAsync(streamId);
            Assert.Equal(before.Select(row => row.Url), after.Select(row => row.Url));
        }

        [Theory]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, false, true)]
        [InlineData(true, true, true, false)]
        public async Task AnyOneOfTheFourGrantsMissingRefusesTheRun(
            bool editStreams,
            bool editAudioStreams,
            bool editVideoOnDemand,
            bool editSeries)
        {
            var groupId = await CreateGroupAsync(
                "source-replacement-partial",
                UserRole.Admin,
                UserStatus.Registered,
                Grants(editStreams, editAudioStreams, editVideoOnDemand, editSeries));

            await CreateUserAsync(UserRole.Admin, "partial", groupId: groupId);

            using var client = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "partial", "Password1", client);

            var csrf = await GetCsrfTokenAsync(client);

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" },
                csrf,
                client);

            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task AllFourGrantsTogetherAdmitTheRun()
        {
            var groupId = await CreateGroupAsync(
                "source-replacement-full",
                UserRole.Admin,
                UserStatus.Registered,
                Grants(true, true, true, true));

            await CreateUserAsync(UserRole.Admin, "fullgrant", groupId: groupId);

            using var client = Fixture.CreateClient();
            await SignInAsync(UserRole.Admin, "fullgrant", "Password1", client);

            var csrf = await GetCsrfTokenAsync(client);

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" },
                csrf,
                client);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");
        }

        [Fact]
        public async Task ABlankResultRefusesTheWholeEntityAndTheRestOfTheRunProceeds()
        {
            await CreateAdminAndSignInAsync();

            // Two candidates on the refused stream: one would become empty, the other would
            // become a usable address. All-or-nothing means neither is written.
            var refusedId = await SeedVideoStreamAsync(
                "Refused",
                ["placeholder", "http://a.test/placeholder"]);

            var updatedId = await SeedVideoStreamAsync(
                "Updated",
                ["http://b.test/placeholder"]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "placeholder", replace = "" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);
            var refusals = root.GetProperty("refusals");

            Assert.Equal(1, refusals.GetArrayLength());
            Assert.Equal("videoStreams", refusals[0].GetProperty("domain").GetString());
            Assert.Equal("Refused", refusals[0].GetProperty("name").GetString());
            Assert.Equal(
                "source_replacement_blank",
                refusals[0].GetProperty("code").GetString());

            await using var connection = await Fixture.OpenConnectionAsync();
            var token = TestContext.Current.CancellationToken;

            var refused = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, refusedId, token);
            Assert.Equal("placeholder", refused[0].Url);
            Assert.Equal("http://a.test/placeholder", refused[1].Url);

            var updated = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, updatedId, token);
            Assert.Equal("http://b.test/", Assert.Single(updated).Url);
        }

        [Fact]
        public async Task AnOverLongResultRefusesTheWholeEntityAndTheRestOfTheRunProceeds()
        {
            await CreateAdminAndSignInAsync();

            // 2000 characters is the column's width, so this row is storable but its
            // replacement is not: "bad" (3) becomes "badbadxx" (8), five characters longer.
            var longSource = "http://a.test/" + new string('z', 1983) + "bad";
            Assert.Equal(2000, longSource.Length);

            var refusedId = await SeedVideoStreamAsync("Long", [longSource]);
            var updatedId = await SeedVideoStreamAsync("Short", ["http://b.test/bad"]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "bad", replace = "badbadxx" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);
            var refusals = root.GetProperty("refusals");

            Assert.Equal(1, refusals.GetArrayLength());
            Assert.Equal("Long", refusals[0].GetProperty("name").GetString());
            Assert.Equal(
                "source_replacement_too_long",
                refusals[0].GetProperty("code").GetString());

            await using var connection = await Fixture.OpenConnectionAsync();
            var token = TestContext.Current.CancellationToken;

            var refused = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, refusedId, token);
            Assert.Equal(longSource, Assert.Single(refused).Url);

            var updated = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, updatedId, token);
            Assert.Equal("http://b.test/badbadxx", Assert.Single(updated).Url);
        }

        [Fact]
        public async Task ABlankResultWinsOverAnOverLongOneOnTheSameEntity()
        {
            await CreateAdminAndSignInAsync();

            // One stream, two addresses, both forbidden results at once: the first becomes
            // whitespace only, the second - already at the column's width - grows past it. The
            // answer must name the blank refusal once, not two refusals and not the length one.
            var prefix = "http://a.test/";
            var stretched = prefix + new string('z', 2000 - prefix.Length - 1) + "X";

            var refusedId = await SeedVideoStreamAsync("Both", ["X", stretched]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "X", replace = "   " },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);
            var refusals = root.GetProperty("refusals");

            Assert.Equal(1, refusals.GetArrayLength());
            Assert.Equal("Both", refusals[0].GetProperty("name").GetString());
            Assert.Equal(
                "source_replacement_blank",
                refusals[0].GetProperty("code").GetString());

            await using var connection = await Fixture.OpenConnectionAsync();
            var rows = await TestDataSeeder.ReadVideoSourceRowsAsync(
                connection,
                refusedId,
                TestContext.Current.CancellationToken);

            Assert.Equal("X", rows[0].Url);
            Assert.Equal(stretched, rows[1].Url);
        }

        [Fact]
        public async Task EveryAdmittedCandidateChangesAndTheSkipCountStaysZero()
        {
            await CreateAdminAndSignInAsync();

            // Two addresses of one stream contain the search text. The database filter is the
            // same ordinal comparison the rule applies, so a candidate it admits always
            // changes; the skip count exists for the case a future collation or predicate
            // widens the filter, and it stays zero here.
            var streamId = await SeedVideoStreamAsync(
                "Mixed",
                ["http://old.test/live", "http://other.test/old.test/x"]);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "old.test/" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var root = await ReadRootAsync(response);

            Assert.Equal(1, root.GetProperty("updated").GetInt32());
            Assert.Equal(0, root.GetProperty("skipped").GetInt32());

            await using var connection = await Fixture.OpenConnectionAsync();
            var rows = await TestDataSeeder.ReadVideoSourceRowsAsync(
                connection,
                streamId,
                TestContext.Current.CancellationToken);

            Assert.Equal("http://old.test//live", rows[0].Url);
            Assert.Equal("http://other.test/old.test//x", rows[1].Url);
        }

        [Fact]
        public async Task AReplacementPreservesIdentityPositionAndCreatedAt()
        {
            await CreateAdminAndSignInAsync();

            var streamId = await SeedVideoStreamAsync(
                "News",
                ["http://old.test/one", "http://untouched.test/two"]);

            await using var connection = await Fixture.OpenConnectionAsync();
            var token = TestContext.Current.CancellationToken;

            await TestDataSeeder.SetActiveSourceAsync(
                connection,
                streamId,
                "http://old.test/one",
                token);

            // The audit columns are stamped by the db context and the suite's clock is a fake
            // that stands still, so the row is placed in the past first: without it "the stamp
            // moved" and "the stamp stayed" would be the same instant.
            await TestDataSeeder.AgeVideoSourceRowsAsync(
                connection,
                streamId,
                DateTimeOffset.UtcNow.AddHours(-1),
                token);

            var before = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, streamId, token);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var after = await TestDataSeeder.ReadVideoSourceRowsAsync(connection, streamId, token);

            // The same two rows, at the same positions, born at the same instant: an in-place
            // replacement is not a delete-and-insert, or the relay's failover chain would be
            // reordered and the audit trail restarted.
            Assert.Equal(before.Select(row => row.Id), after.Select(row => row.Id));
            Assert.Equal(before.Select(row => row.Position), after.Select(row => row.Position));
            Assert.Equal(before.Select(row => row.CreatedAt), after.Select(row => row.CreatedAt));

            Assert.Equal("http://new.test/one", after[0].Url);
            Assert.Equal("http://untouched.test/two", after[1].Url);

            // Only the row that changed moves its updated_at; the untouched one was not even
            // loaded, so its stamp is exactly what it was.
            Assert.True(after[0].UpdatedAt > before[0].UpdatedAt);
            Assert.Equal(before[1].UpdatedAt, after[1].UpdatedAt);

            // The relaying process's report is not a playback address the operator edits: it
            // is deliberately outside the replacement's scope and stays stale until the relay
            // reports again.
            Assert.Equal(
                "http://old.test/one",
                await TestDataSeeder.ReadActiveSourceAsync(connection, streamId, token));
        }

        [Fact]
        public async Task AVodAndAnEpisodeKeepTheirIdentityAndCreatedAt()
        {
            await CreateAdminAndSignInAsync();

            var vodId = await SeedVodAsync("Film", "http://old.test/film.mp4");
            var (_, episodeId) = await SeedEpisodeAsync(
                "Show",
                "First",
                "http://old.test/show.mp4");

            await using var connection = await Fixture.OpenConnectionAsync();
            var token = TestContext.Current.CancellationToken;

            var past = DateTimeOffset.UtcNow.AddHours(-1);
            await TestDataSeeder.AgeVodRowAsync(connection, vodId, past, token);
            await TestDataSeeder.AgeEpisodeRowAsync(connection, episodeId, past, token);

            var vodBefore = await TestDataSeeder.ReadVodSourceAsync(connection, vodId, token);
            var episodeBefore = await TestDataSeeder.ReadEpisodeSourceAsync(connection, episodeId, token);

            var csrf = await GetCsrfTokenAsync();

            using var response = await PostJsonAsync(
                "/admin/source-replacements",
                new { find = "old.test", replace = "new.test" },
                csrf);

            await AssertCodeAsync(response, HttpStatusCode.OK, "source_replacement_completed");

            var vodAfter = await TestDataSeeder.ReadVodSourceAsync(connection, vodId, token);
            var episodeAfter = await TestDataSeeder.ReadEpisodeSourceAsync(connection, episodeId, token);

            Assert.Equal("http://new.test/film.mp4", vodAfter.Source);
            Assert.Equal(vodBefore.CreatedAt, vodAfter.CreatedAt);
            Assert.True(vodAfter.UpdatedAt > vodBefore.UpdatedAt);

            Assert.Equal("http://new.test/show.mp4", episodeAfter.Source);
            Assert.Equal(episodeBefore.CreatedAt, episodeAfter.CreatedAt);
            Assert.True(episodeAfter.UpdatedAt > episodeBefore.UpdatedAt);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("PUT")]
        [InlineData("PATCH")]
        [InlineData("DELETE")]
        public async Task EveryOtherVerbAnswersMethodNotAllowed(string method)
        {
            await CreateAdminAndSignInAsync();
            await SeedVideoStreamAsync("News", ["http://old.test/live"]);

            var csrf = await GetCsrfTokenAsync();

            using var request = new HttpRequestMessage(new HttpMethod(method), "/admin/source-replacements");

            if (method != "GET")
            {
                request.Headers.Add("X-CSRF-Token", csrf);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { find = "old.test", replace = "new.test" }),
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            using var response = await Client.SendAsync(
                request,
                TestContext.Current.CancellationToken);

            await AssertCodeAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
        }

        /// <summary>The four grants the endpoint demands, as the caller's group should hold them.</summary>
        private static UserPermission[] Grants(
            bool editStreams,
            bool editAudioStreams,
            bool editVideoOnDemand,
            bool editSeries)
        {
            var grants = new List<UserPermission>();

            if (editStreams)
            {
                grants.Add(UserPermission.EditStreams);
            }

            if (editAudioStreams)
            {
                grants.Add(UserPermission.EditAudioStreams);
            }

            if (editVideoOnDemand)
            {
                grants.Add(UserPermission.EditVideoOnDemand);
            }

            if (editSeries)
            {
                grants.Add(UserPermission.EditSeries);
            }

            return grants.ToArray();
        }

        private async Task AssertValidationAsync(HttpResponseMessage response, string field)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            Assert.Equal("validation_failed", CodeOf(body));

            Assert.True(
                body.RootElement.GetProperty("errors").TryGetProperty(field, out var reasons),
                body.RootElement.ToString());

            Assert.NotEmpty(reasons.EnumerateArray());
        }

        private async Task<JsonElement> ReadRootAsync(HttpResponseMessage response)
        {
            using var body = await ReadJsonAsync(response);

            // The document dies with this frame, so the root is cloned before it is returned.
            return body.RootElement.Clone();
        }

        private async Task<IReadOnlyList<(Guid Id, int Position, string Url, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)>> ReadVideoRowsAsync(
            Guid streamId)
        {
            await using var connection = await Fixture.OpenConnectionAsync();

            return await TestDataSeeder.ReadVideoSourceRowsAsync(
                connection,
                streamId,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> SeedVideoStreamAsync(string name, IReadOnlyList<string> sources)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVideoStreamAsync(
                scope.ServiceProvider,
                name,
                sources,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> SeedAudioStreamAsync(string name, IReadOnlyList<string> sources)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateAudioStreamAsync(
                scope.ServiceProvider,
                name,
                sources,
                TestContext.Current.CancellationToken);
        }

        private async Task<Guid> SeedVodAsync(string name, string source)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateVodItemAsync(
                scope.ServiceProvider,
                name,
                source,
                TestContext.Current.CancellationToken);
        }

        private async Task<(Guid SeriesId, Guid EpisodeId)> SeedEpisodeAsync(
            string name,
            string episodeName,
            string source)
        {
            await using var scope = Fixture.Factory.Services.CreateAsyncScope();

            return await TestDataSeeder.CreateSeriesWithEpisodeAsync(
                scope.ServiceProvider,
                name,
                episodeName,
                source,
                TestContext.Current.CancellationToken);
        }
    }
}
