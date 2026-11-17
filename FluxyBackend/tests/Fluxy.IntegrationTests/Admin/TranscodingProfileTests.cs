using System.Net;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Npgsql;

namespace Fluxy.IntegrationTests.Admin
{
    /// <summary>
    /// The transcoding profile API: the seeded Standard profile, the option set, the name rule,
    /// the four references that keep a profile alive, the picker endpoint and the two grants.
    /// </summary>
    /// <remarks>
    /// The rows are planted through the API rather than through the repository, because the
    /// controller is what this suite is about: a contract that refused the wrong field or
    /// answered a duplicate with the wrong status would be invisible to a repository test.
    /// </remarks>
    public sealed class TranscodingProfileTests : IntegrationTestBase
    {
        public TranscodingProfileTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task AProfileIsCreatedChangedAndDeleted()
        {
            await CreateAdminAndSignInAsync();

            using var created = await PostProfileAsync(new
            {
                name = " Standard 720p ",
                videoCodec = "libx264",
                audioCodec = "aac",
                preset = "veryfast",
                videoProfile = "high4.0",
                videoBitrateAvg = 2500,
                audioBitrateAvg = 128,
                minBitrateTolerance = 2000,
                maxBitrateTolerance = 3000,
                bufferSize = 5000,
                crf = 23,
                width = 1280,
                height = 720,
                aspectRatio = "16:9",
                framerate = 25.0m,
                audioSampleRate = 48000,
                audioChannels = 2,
                threads = 4,
                logoUrl = "https://example.test/logo.png",
                redactionX = 10,
                redactionY = 20,
                redactionWidth = 100,
                redactionHeight = 50
            });

            await AssertCodeAsync(created, HttpStatusCode.Created, "transcoding_profile_created");
            Assert.NotNull(created.Headers.Location);
            var id = IdFromLocation(created);
            Assert.Equal($"/admin/transcoding-profiles/{id}", created.Headers.Location?.ToString());

            using var detail = await Client.GetAsync(
                $"/admin/transcoding-profiles/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var body = await ReadJsonAsync(detail);
            var root = body.RootElement;

            Assert.Equal("Standard 720p", root.GetProperty("name").GetString());
            Assert.Equal("libx264", root.GetProperty("videoCodec").GetString());
            Assert.Equal("aac", root.GetProperty("audioCodec").GetString());
            Assert.Equal("veryfast", root.GetProperty("preset").GetString());
            Assert.Equal("high4.0", root.GetProperty("videoProfile").GetString());
            Assert.Equal(2500, root.GetProperty("videoBitrateAvg").GetInt32());
            Assert.Equal(23, root.GetProperty("crf").GetInt32());
            Assert.Equal(1280, root.GetProperty("width").GetInt32());
            Assert.Equal(25.0m, root.GetProperty("framerate").GetDecimal());
            Assert.Equal(10, root.GetProperty("redactionX").GetInt32());

            // The change carries the whole set: a field the body omits is cleared, not kept.
            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/transcoding-profiles/{id}",
                new
                {
                    name = "Renamed",
                    videoCodec = "copy",
                    audioCodec = "copy",
                    preset = "default"
                },
                csrf);
            await AssertCodeAsync(patched, HttpStatusCode.OK, "transcoding_profile_updated");

            using var afterPatch = await Client.GetAsync(
                $"/admin/transcoding-profiles/{id}",
                TestContext.Current.CancellationToken);
            using var afterPatchBody = await ReadJsonAsync(afterPatch);
            var patchedRoot = afterPatchBody.RootElement;

            Assert.Equal("Renamed", patchedRoot.GetProperty("name").GetString());
            Assert.Equal("copy", patchedRoot.GetProperty("videoCodec").GetString());
            Assert.Equal(
                System.Text.Json.JsonValueKind.Null,
                patchedRoot.GetProperty("crf").ValueKind);
            Assert.Equal(
                System.Text.Json.JsonValueKind.Null,
                patchedRoot.GetProperty("redactionX").ValueKind);

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/transcoding-profiles/{id}", csrf);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "transcoding_profile_deleted");

            using var missing = await Client.GetAsync(
                $"/admin/transcoding-profiles/{id}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(
                missing,
                HttpStatusCode.NotFound,
                "transcoding_profile_not_found");

            csrf = await GetCsrfTokenAsync();
            using var again = await DeleteAsync($"/admin/transcoding-profiles/{id}", csrf);
            await AssertCodeAsync(
                again,
                HttpStatusCode.NotFound,
                "transcoding_profile_not_found");
        }

        [Fact]
        public async Task TheSeedStatementInsertsTheProfileExactlyOnce()
        {
            await CreateAdminAndSignInAsync();

            // The rows are truncated between tests, so the migration's seed is not present by
            // the time a test runs. What is worth proving is the statement itself: running the
            // same guarded INSERT twice leaves one row, which is what makes the migration safe
            // on an installation the seeder has already touched.
            const string seed = """
                INSERT INTO transcoding_profiles (
                    id, name, video_codec, audio_codec, preset, video_profile,
                    video_bitrate_avg, audio_bitrate_avg, min_bitrate_tolerance,
                    max_bitrate_tolerance, buffer_size, crf, width, height, aspect_ratio,
                    framerate, audio_sample_rate, audio_channels, threads, logo_url,
                    redaction_x, redaction_y, redaction_width, redaction_height,
                    created_at, updated_at)
                SELECT
                    '00000000-0000-0000-0000-000000000301'::uuid, 'Standard H264 AAC', 1,
                    1, 0, NULL,
                    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
                    NULL, NULL, NULL, NULL, NULL,
                    NULL, NULL, NULL, NULL,
                    now(), now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM transcoding_profiles WHERE name = 'Standard H264 AAC'
                );
                """;

            await using (var connection = await Fixture.OpenConnectionAsync())
            {
                for (var run = 0; run < 2; run++)
                {
                    await using var command = new NpgsqlCommand(seed, connection);
                    await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
                }
            }

            using var page = await Client.GetAsync(
                "/admin/transcoding-profiles?search=Standard%20H264%20AAC",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            using var body = await ReadJsonAsync(page);
            var root = body.RootElement;

            Assert.Equal(1, root.GetProperty("total").GetInt32());
            var item = root.GetProperty("items")[0];
            Assert.Equal("Standard H264 AAC", item.GetProperty("name").GetString());
            Assert.Equal("libx264", item.GetProperty("videoCodec").GetString());
            Assert.Equal("aac", item.GetProperty("audioCodec").GetString());
            Assert.Equal("default", item.GetProperty("preset").GetString());
            Assert.Equal(
                System.Text.Json.JsonValueKind.Null,
                item.GetProperty("videoProfile").ValueKind);

            // The seeded row is an ordinary profile: it may be renamed.
            var csrf = await GetCsrfTokenAsync();
            using var renamed = await PatchJsonAsync(
                $"/admin/transcoding-profiles/{item.GetProperty("id").GetString()}",
                new
                {
                    name = "Standard renamed",
                    videoCodec = "libx264",
                    audioCodec = "aac",
                    preset = "default"
                },
                csrf);
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "transcoding_profile_updated");
        }

        [Fact]
        public async Task ANameIsUniqueForTheInstallation()
        {
            await CreateAdminAndSignInAsync();

            using var first = await PostProfileAsync(new { name = "Duplicate" });
            await AssertCodeAsync(first, HttpStatusCode.Created, "transcoding_profile_created");

            using var duplicate = await PostProfileAsync(new { name = "Duplicate" });
            await AssertCodeAsync(
                duplicate,
                HttpStatusCode.Conflict,
                "transcoding_profile_already_exists");
            using var duplicateBody = await ReadJsonAsync(duplicate);
            Assert.True(
                duplicateBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            // Compared case sensitively: PostgreSQL's default collation compares the bytes.
            using var otherCase = await PostProfileAsync(new { name = "duplicate" });
            Assert.Equal(HttpStatusCode.Created, otherCase.StatusCode);

            // A change may not take a name another profile holds.
            var csrf = await GetCsrfTokenAsync();
            using var collision = await PatchJsonAsync(
                $"/admin/transcoding-profiles/{IdFromLocation(otherCase)}",
                new
                {
                    name = "Duplicate",
                    videoCodec = "copy",
                    audioCodec = "copy",
                    preset = "default"
                },
                csrf);
            await AssertCodeAsync(
                collision,
                HttpStatusCode.Conflict,
                "transcoding_profile_already_exists");
        }

        [Fact]
        public async Task EachFieldIsRefusedUnderItsOwnName()
        {
            await CreateAdminAndSignInAsync();

            using var blankName = await PostProfileAsync(new { name = "   " });
            await AssertCodeAsync(blankName, HttpStatusCode.BadRequest, "validation_failed");
            using var blankNameBody = await ReadJsonAsync(blankName);
            Assert.True(
                blankNameBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var longName = await PostProfileAsync(new { name = new string('a', 101) });
            await AssertCodeAsync(longName, HttpStatusCode.BadRequest, "validation_failed");
            using var longNameBody = await ReadJsonAsync(longName);
            Assert.True(
                longNameBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var badCrf = await PostProfileAsync(new { name = "Crf", crf = 52 });
            await AssertCodeAsync(badCrf, HttpStatusCode.BadRequest, "validation_failed");
            using var badCrfBody = await ReadJsonAsync(badCrf);
            Assert.True(
                badCrfBody.RootElement.GetProperty("errors").TryGetProperty("crf", out _));

            using var badWidth = await PostProfileAsync(new { name = "Width", width = -2 });
            await AssertCodeAsync(badWidth, HttpStatusCode.BadRequest, "validation_failed");
            using var badWidthBody = await ReadJsonAsync(badWidth);
            Assert.True(
                badWidthBody.RootElement.GetProperty("errors").TryGetProperty("width", out _));

            using var badChannels = await PostProfileAsync(new { name = "Channels", audioChannels = 9 });
            await AssertCodeAsync(badChannels, HttpStatusCode.BadRequest, "validation_failed");
            using var badChannelsBody = await ReadJsonAsync(badChannels);
            Assert.True(
                badChannelsBody.RootElement.GetProperty("errors")
                    .TryGetProperty("audioChannels", out _));

            using var partialRedaction = await PostProfileAsync(new
            {
                name = "Redaction",
                redactionX = 5,
                redactionY = 5
            });
            await AssertCodeAsync(
                partialRedaction,
                HttpStatusCode.BadRequest,
                "validation_failed");
            using var partialRedactionBody = await ReadJsonAsync(partialRedaction);
            Assert.True(
                partialRedactionBody.RootElement.GetProperty("errors")
                    .TryGetProperty("redactionX", out _));

            // A codec that names no member is refused by the parser, under its own field.
            using var badCodec = await PostProfileAsync(new { name = "Codec", videoCodec = "h264" });
            await AssertCodeAsync(badCodec, HttpStatusCode.BadRequest, "validation_failed");
            using var badCodecBody = await ReadJsonAsync(badCodec);
            Assert.True(
                badCodecBody.RootElement.GetProperty("errors").TryGetProperty("videoCodec", out _));

            // An absent codec is not a refusal: the create defaults apply.
            using var defaults = await PostProfileAsync(new { name = "Defaults" });
            await AssertCodeAsync(defaults, HttpStatusCode.Created, "transcoding_profile_created");

            using var defaultsDetail = await Client.GetAsync(
                $"/admin/transcoding-profiles/{IdFromLocation(defaults)}",
                TestContext.Current.CancellationToken);
            using var defaultsBody = await ReadJsonAsync(defaultsDetail);
            var defaultsRoot = defaultsBody.RootElement;

            Assert.Equal("copy", defaultsRoot.GetProperty("videoCodec").GetString());
            Assert.Equal("copy", defaultsRoot.GetProperty("audioCodec").GetString());
            Assert.Equal("default", defaultsRoot.GetProperty("preset").GetString());
            Assert.Equal(
                System.Text.Json.JsonValueKind.Null,
                defaultsRoot.GetProperty("videoProfile").ValueKind);
        }

        [Fact]
        public async Task TheListSearchesSortsAndPagesOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            // The seeded profile is present, so the names below are chosen not to collide with it.
            await CreateProfileAsync("Beta profile");
            await CreateProfileAsync("Alpha profile");
            await CreateProfileAsync("Gamma profile");

            using var searched = await Client.GetAsync(
                "/admin/transcoding-profiles?search=ALPHA",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());

            using var sorted = await Client.GetAsync(
                "/admin/transcoding-profiles?search=profile&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var sortedBody = await ReadJsonAsync(sorted);
            Assert.Equal(
                new[] { "Alpha profile", "Beta profile", "Gamma profile" },
                sortedBody.RootElement.GetProperty("items").EnumerateArray()
                    .Select(item => item.GetProperty("name").GetString())
                    .ToArray());

            using var paged = await Client.GetAsync(
                "/admin/transcoding-profiles?page=1&pageSize=2&sortBy=name&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var pagedBody = await ReadJsonAsync(paged);
            Assert.Equal(2, pagedBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(3, pagedBody.RootElement.GetProperty("total").GetInt32());

            using var unknownSort = await Client.GetAsync(
                "/admin/transcoding-profiles?sortBy=crf",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownSort, HttpStatusCode.BadRequest, "validation_failed");
            using var unknownSortBody = await ReadJsonAsync(unknownSort);
            Assert.True(
                unknownSortBody.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));

            using var unknownOrder = await Client.GetAsync(
                "/admin/transcoding-profiles?sortOrder=upwards",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(unknownOrder, HttpStatusCode.BadRequest, "validation_failed");

            using var badPage = await Client.GetAsync(
                "/admin/transcoding-profiles?page=0",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badPage, HttpStatusCode.BadRequest, "validation_failed");

            using var badSize = await Client.GetAsync(
                "/admin/transcoding-profiles?pageSize=1001",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(badSize, HttpStatusCode.BadRequest, "validation_failed");
        }

        [Fact]
        public async Task AMissingProfileAnswersNotFoundUnderEveryWrite()
        {
            await CreateAdminAndSignInAsync();
            var unknown = Guid.NewGuid();

            using var read = await Client.GetAsync(
                $"/admin/transcoding-profiles/{unknown}",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(
                read,
                HttpStatusCode.NotFound,
                "transcoding_profile_not_found");

            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                $"/admin/transcoding-profiles/{unknown}",
                new { name = "Nobody" },
                csrf);
            await AssertCodeAsync(
                patched,
                HttpStatusCode.NotFound,
                "transcoding_profile_not_found");

            csrf = await GetCsrfTokenAsync();
            using var deleted = await DeleteAsync($"/admin/transcoding-profiles/{unknown}", csrf);
            await AssertCodeAsync(
                deleted,
                HttpStatusCode.NotFound,
                "transcoding_profile_not_found");
        }

        [Fact]
        public async Task WritesRequireCsrfAndThePermission()
        {
            await CreateAdminAndSignInAsync();

            using var noCsrf = await PostJsonAsync(
                "/admin/transcoding-profiles",
                new { name = "No csrf" });
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            // An administrator whose group may run the streams but not read the profiles.
            var streamOperators = await CreateGroupAsync(
                "Stream operators",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewStreams,
                Fluxy.Core.Users.Enums.UserPermission.EditStreams);

            using var operatorClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "profileguard", groupId: streamOperators);
            await SignInAsync(UserRole.Admin, "profileguard", "Password1", operatorClient);

            using var forbiddenRead = await operatorClient.GetAsync(
                "/admin/transcoding-profiles",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenRead, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(operatorClient);
            using var forbiddenWrite = await PostJsonAsync(
                "/admin/transcoding-profiles",
                new { name = "Forbidden" },
                csrf,
                operatorClient);
            await AssertCodeAsync(forbiddenWrite, HttpStatusCode.Forbidden, "permission_denied");

            // A view-only grant reads and is refused the writes.
            var readers = await CreateGroupAsync(
                "Profile readers",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewTranscodingProfiles);

            using var readerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "profilereader", groupId: readers);
            await SignInAsync(UserRole.Admin, "profilereader", "Password1", readerClient);

            using var allowedRead = await readerClient.GetAsync(
                "/admin/transcoding-profiles",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            csrf = await GetCsrfTokenAsync(readerClient);
            using var refusedWrite = await PostJsonAsync(
                "/admin/transcoding-profiles",
                new { name = "Refused" },
                csrf,
                readerClient);
            await AssertCodeAsync(refusedWrite, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task TheOptionsEndpointIsGatedByAnyContentViewGrant()
        {
            await CreateAdminAndSignInAsync();
            await CreateProfileAsync("Picker profile");

            using var allowed = await Client.GetAsync(
                "/admin/transcoding-profiles/options",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            using var allowedBody = await ReadJsonAsync(allowed);
            Assert.Contains(
                allowedBody.RootElement.EnumerateArray(),
                option => option.GetProperty("name").GetString() == "Picker profile");

            // A single content-view grant is enough - the picker is a field of a content form.
            var streamReaders = await CreateGroupAsync(
                "Stream readers",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewStreams);

            using var streamClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "streamreader", groupId: streamReaders);
            await SignInAsync(UserRole.Admin, "streamreader", "Password1", streamClient);

            using var allowedByStream = await streamClient.GetAsync(
                "/admin/transcoding-profiles/options",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedByStream.StatusCode);

            // The full list is still the profile pair, which this group does not hold.
            using var refusedList = await streamClient.GetAsync(
                "/admin/transcoding-profiles",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(refusedList, HttpStatusCode.Forbidden, "permission_denied");

            // Neither grant refuses the picker.
            var strangers = await CreateGroupAsync(
                "No content",
                UserRole.Admin,
                UserStatus.Registered,
                Fluxy.Core.Users.Enums.UserPermission.ViewUserGroups);

            using var strangerClient = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "nocontent", groupId: strangers);
            await SignInAsync(UserRole.Admin, "nocontent", "Password1", strangerClient);

            using var refusedOptions = await strangerClient.GetAsync(
                "/admin/transcoding-profiles/options",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(refusedOptions, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task AReferencedProfileCanNotBeDeletedUntilEveryReferenceIsCleared()
        {
            await CreateAdminAndSignInAsync();

            var profileId = await CreateProfileAsync("Keep me");

            // A video stream points at it.
            var videoCategory = await CreateCategoryAsync("Live", "VideoStream");
            using var stream = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Uses the profile",
                    type = "permanent",
                    categoryId = videoCategory,
                    sources = new[] { "https://example.test/source" },
                    transcodingProfileId = profileId
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(stream, HttpStatusCode.Created, "video_stream_created");
            var streamId = IdFromLocation(stream);

            using var refused = await DeleteProfileAsync(profileId);
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "transcoding_profile_in_use");

            // Clearing the reference is a PATCH with an empty string.
            var csrf = await GetCsrfTokenAsync();
            using var cleared = await PatchJsonAsync(
                $"/admin/video-streams/{streamId}",
                new { transcodingProfileId = "" },
                csrf);
            await AssertCodeAsync(cleared, HttpStatusCode.OK, "video_stream_updated");

            using var deleted = await DeleteProfileAsync(profileId);
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "transcoding_profile_deleted");
        }

        [Fact]
        public async Task EveryReferencingTableKeepsAProfileAlive()
        {
            await CreateAdminAndSignInAsync();

            var audioProfile = await CreateProfileAsync("Audio reference");
            var audioCategory = await CreateCategoryAsync("Radio", "AudioStream");
            using var audio = await PostJsonAsync(
                "/admin/audio-streams",
                new
                {
                    name = "Uses the profile",
                    type = "ondemand",
                    categoryId = audioCategory,
                    sources = new[] { "https://example.test/radio" },
                    transcodingProfileId = audioProfile
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(audio, HttpStatusCode.Created, "audio_stream_created");
            using var refusedAudio = await DeleteProfileAsync(audioProfile);
            await AssertCodeAsync(
                refusedAudio,
                HttpStatusCode.Conflict,
                "transcoding_profile_in_use");

            var vodProfile = await CreateProfileAsync("Video reference");
            var vodCategory = await CreateCategoryAsync("Films", "VideoOnDemand");
            using var vod = await PostJsonAsync(
                "/admin/video-on-demand",
                new
                {
                    name = "Uses the profile",
                    type = "relay",
                    categoryId = vodCategory,
                    source = "https://example.test/film.mp4",
                    transcodingProfileId = vodProfile
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(vod, HttpStatusCode.Created, "video_on_demand_created");
            using var refusedVod = await DeleteProfileAsync(vodProfile);
            await AssertCodeAsync(refusedVod, HttpStatusCode.Conflict, "transcoding_profile_in_use");

            var seriesProfile = await CreateProfileAsync("Series reference");
            var seriesCategory = await CreateCategoryAsync("Shows", "Series");
            using var series = await PostJsonAsync(
                "/admin/series",
                new
                {
                    name = "Uses the profile",
                    categoryId = seriesCategory,
                    transcodingProfileId = seriesProfile
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(series, HttpStatusCode.Created, "series_created");
            using var refusedSeries = await DeleteProfileAsync(seriesProfile);
            await AssertCodeAsync(
                refusedSeries,
                HttpStatusCode.Conflict,
                "transcoding_profile_in_use");

            // A reference to a profile that does not exist is refused by field, not by the key.
            using var unknownProfile = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Names nobody",
                    type = "permanent",
                    categoryId = await CreateCategoryAsync("Other", "VideoStream"),
                    sources = new[] { "https://example.test/source" },
                    transcodingProfileId = Guid.NewGuid().ToString()
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(
                unknownProfile,
                HttpStatusCode.BadRequest,
                "validation_failed");
            using var unknownProfileBody = await ReadJsonAsync(unknownProfile);
            Assert.True(
                unknownProfileBody.RootElement.GetProperty("errors")
                    .TryGetProperty("transcodingProfileId", out _));
        }

        [Fact]
        public async Task AReferenceIsSetReplacedAndClearedFromEveryContentEndpoint()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateProfileAsync("First profile");
            var second = await CreateProfileAsync("Second profile");

            // Video stream: set on create, replaced, cleared.
            var videoCategory = await CreateCategoryAsync("Live", "VideoStream");
            using var video = await PostJsonAsync(
                "/admin/video-streams",
                new
                {
                    name = "Video",
                    type = "permanent",
                    categoryId = videoCategory,
                    sources = new[] { "https://example.test/source" },
                    transcodingProfileId = first
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(video, HttpStatusCode.Created, "video_stream_created");
            var videoId = IdFromLocation(video);
            await AssertDetailProfileAsync(
                $"/admin/video-streams/{videoId}",
                first);

            var csrf = await GetCsrfTokenAsync();
            using var videoReplaced = await PatchJsonAsync(
                $"/admin/video-streams/{videoId}",
                new { transcodingProfileId = second },
                csrf);
            await AssertCodeAsync(videoReplaced, HttpStatusCode.OK, "video_stream_updated");
            await AssertDetailProfileAsync($"/admin/video-streams/{videoId}", second);

            csrf = await GetCsrfTokenAsync();
            using var videoCleared = await PatchJsonAsync(
                $"/admin/video-streams/{videoId}",
                new { transcodingProfileId = "" },
                csrf);
            await AssertCodeAsync(videoCleared, HttpStatusCode.OK, "video_stream_updated");
            await AssertDetailProfileAsync($"/admin/video-streams/{videoId}", null);

            // Audio stream: set on create, replaced, cleared.
            var audioCategory = await CreateCategoryAsync("Radio", "AudioStream");
            using var audio = await PostJsonAsync(
                "/admin/audio-streams",
                new
                {
                    name = "Audio",
                    type = "ondemand",
                    categoryId = audioCategory,
                    sources = new[] { "https://example.test/radio" },
                    transcodingProfileId = first
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(audio, HttpStatusCode.Created, "audio_stream_created");
            var audioId = IdFromLocation(audio);
            await AssertDetailProfileAsync($"/admin/audio-streams/{audioId}", first);

            csrf = await GetCsrfTokenAsync();
            using var audioReplaced = await PatchJsonAsync(
                $"/admin/audio-streams/{audioId}",
                new { transcodingProfileId = second },
                csrf);
            await AssertCodeAsync(audioReplaced, HttpStatusCode.OK, "audio_stream_updated");
            await AssertDetailProfileAsync($"/admin/audio-streams/{audioId}", second);

            csrf = await GetCsrfTokenAsync();
            using var audioCleared = await PatchJsonAsync(
                $"/admin/audio-streams/{audioId}",
                new { transcodingProfileId = "" },
                csrf);
            await AssertCodeAsync(audioCleared, HttpStatusCode.OK, "audio_stream_updated");
            await AssertDetailProfileAsync($"/admin/audio-streams/{audioId}", null);

            // On-demand video: set on create, replaced, cleared.
            var vodCategory = await CreateCategoryAsync("Films", "VideoOnDemand");
            using var vod = await PostJsonAsync(
                "/admin/video-on-demand",
                new
                {
                    name = "Video on demand",
                    type = "relay",
                    categoryId = vodCategory,
                    source = "https://example.test/film.mp4",
                    transcodingProfileId = first
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(vod, HttpStatusCode.Created, "video_on_demand_created");
            var vodId = IdFromLocation(vod);
            await AssertDetailProfileAsync($"/admin/video-on-demand/{vodId}", first);

            csrf = await GetCsrfTokenAsync();
            using var vodReplaced = await PatchJsonAsync(
                $"/admin/video-on-demand/{vodId}",
                new { transcodingProfileId = second },
                csrf);
            await AssertCodeAsync(vodReplaced, HttpStatusCode.OK, "video_on_demand_updated");
            await AssertDetailProfileAsync($"/admin/video-on-demand/{vodId}", second);

            csrf = await GetCsrfTokenAsync();
            using var vodCleared = await PatchJsonAsync(
                $"/admin/video-on-demand/{vodId}",
                new { transcodingProfileId = "" },
                csrf);
            await AssertCodeAsync(vodCleared, HttpStatusCode.OK, "video_on_demand_updated");
            await AssertDetailProfileAsync($"/admin/video-on-demand/{vodId}", null);

            // Series: set on create, replaced, cleared.
            var seriesCategory = await CreateCategoryAsync("Shows", "Series");
            using var series = await PostJsonAsync(
                "/admin/series",
                new
                {
                    name = "Series",
                    categoryId = seriesCategory,
                    transcodingProfileId = first
                },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(series, HttpStatusCode.Created, "series_created");
            var seriesId = IdFromLocation(series);
            await AssertDetailProfileAsync($"/admin/series/{seriesId}", first);

            csrf = await GetCsrfTokenAsync();
            using var seriesReplaced = await PatchJsonAsync(
                $"/admin/series/{seriesId}",
                new { transcodingProfileId = second },
                csrf);
            await AssertCodeAsync(seriesReplaced, HttpStatusCode.OK, "series_updated");
            await AssertDetailProfileAsync($"/admin/series/{seriesId}", second);

            csrf = await GetCsrfTokenAsync();
            using var seriesCleared = await PatchJsonAsync(
                $"/admin/series/{seriesId}",
                new { transcodingProfileId = "" },
                csrf);
            await AssertCodeAsync(seriesCleared, HttpStatusCode.OK, "series_updated");
            await AssertDetailProfileAsync($"/admin/series/{seriesId}", null);
        }

        private async Task<HttpResponseMessage> PostProfileAsync(object body)
            => await PostJsonAsync(
                "/admin/transcoding-profiles",
                body,
                await GetCsrfTokenAsync());

        private async Task<string> CreateProfileAsync(string name)
        {
            using var response = await PostProfileAsync(new { name });
            await AssertCodeAsync(response, HttpStatusCode.Created, "transcoding_profile_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> DeleteProfileAsync(string id)
            => await DeleteAsync(
                $"/admin/transcoding-profiles/{id}",
                await GetCsrfTokenAsync());

        private async Task<string> CreateCategoryAsync(string name, string type)
        {
            var csrf = await GetCsrfTokenAsync();
            using var response = await PostJsonAsync(
                "/admin/content-categories",
                new { name, type },
                csrf);
            await AssertCodeAsync(response, HttpStatusCode.Created, "content_category_created");

            return IdFromLocation(response);
        }

        /// <summary>
        /// Asserts the transcoding profile a content detail answers with, or that it answers
        /// none. The list rows deliberately do not carry the field, so the detail is the only
        /// place it can be read.
        /// </summary>
        private async Task AssertDetailProfileAsync(string path, string? expected)
        {
            using var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await ReadJsonAsync(response);

            var property = body.RootElement.GetProperty("transcodingProfileId");

            Assert.Equal(
                expected,
                property.ValueKind == System.Text.Json.JsonValueKind.Null
                    ? null
                    : property.GetString());
        }
    }
}
