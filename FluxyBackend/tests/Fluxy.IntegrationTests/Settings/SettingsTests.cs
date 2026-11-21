using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fluxy.Core.Settings.Utils;
using Fluxy.Core.Subscriptions.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxy.IntegrationTests.Settings
{
    /// <summary>
    /// The two settings resources and the guest read: the wire shape of every field, the
    /// write-only private key, the validation keys, the two grants, the method fences, and the
    /// three settings that have consumers (reCAPTCHA absence, trials, the email service name).
    /// </summary>
    /// <remarks>
    /// Rows are written through the API and read back through it, because the contract is what
    /// this suite is about. The trials case creates a subscription through the real endpoint, so
    /// the service's own refusal is the one proved rather than a unit-level re-statement.
    /// </remarks>
    public sealed class SettingsTests : IntegrationTestBase
    {
        public SettingsTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task TheSettingsAreRefusedToAnAnonymousCallerOnEveryRoute()
        {
            using var application = await Client.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(application, HttpStatusCode.Unauthorized, "auth_required");

            using var mag = await Client.GetAsync(
                "/admin/mag-settings",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(mag, HttpStatusCode.Unauthorized, "auth_required");

            var csrf = await GetCsrfTokenAsync();
            using var write = await PatchJsonAsync("/admin/settings", Full(), csrf);
            await AssertCodeAsync(write, HttpStatusCode.Unauthorized, "auth_required");
        }

        [Fact]
        public async Task ThePublicSettingsAreAnonymousAndCarryNothingButTheSiteKey()
        {
            using var response = await Client.GetAsync(
                "/auth/public-settings",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore ?? false);

            using var body = await ReadJsonAsync(response);

            Assert.Equal(string.Empty, body.RootElement.GetProperty("recaptchaSiteKey").GetString());
            Assert.Equal(
                ["recaptchaSiteKey"],
                body.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        }

        [Fact]
        public async Task TheDefaultsAreAnsweredAndTheSecretIsNeverOneOfThem()
        {
            await CreateAdminAndSignInAsync();

            using var response = await Client.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            Assert.Equal(ApplicationSettingsDefaults.ServiceName, root.GetProperty("serviceName").GetString());
            Assert.Equal("Europe/Berlin", root.GetProperty("defaultTimeZone").GetString());
            Assert.Equal(ApplicationSettingsDefaults.Enigma2BouquetName, root.GetProperty("enigma2BouquetName").GetString());
            Assert.Equal(string.Empty, root.GetProperty("recaptchaSiteKey").GetString());
            Assert.False(root.GetProperty("recaptchaConfigured").GetBoolean());
            Assert.True(root.GetProperty("enableTrials").GetBoolean());
            Assert.Equal(20, root.GetProperty("publicApiFloodLimit").GetInt32());
            Assert.Empty(root.GetProperty("publicApiFloodWhitelist").EnumerateArray());
            Assert.False(root.GetProperty("requireUserAgent").GetBoolean());
            Assert.Equal(30, root.GetProperty("retentionUserLogs").GetInt32());
            Assert.Equal(30, root.GetProperty("retentionCreditLogs").GetInt32());
            Assert.Equal(30, root.GetProperty("retentionConnectionLogs").GetInt32());
            Assert.Equal(30, root.GetProperty("retentionApiRequestFailures").GetInt32());
            Assert.Equal(30, root.GetProperty("retentionStreamProblems").GetInt32());
            Assert.Equal(30, root.GetProperty("retentionMagEvents").GetInt32());
            Assert.False(root.GetProperty("backupEnabled").GetBoolean());
            Assert.Equal("daily", root.GetProperty("backupPeriod").GetString());
            Assert.Equal(10, root.GetProperty("backupRotationCount").GetInt32());
            Assert.Equal(string.Empty, root.GetProperty("fallbackStreamDownUrl").GetString());
            Assert.Equal(string.Empty, root.GetProperty("fallbackUserBannedUrl").GetString());
            Assert.Equal(string.Empty, root.GetProperty("fallbackUserExpiredUrl").GetString());
            Assert.Equal(string.Empty, root.GetProperty("fallbackMaxConnectionsUrl").GetString());

            Assert.False(root.TryGetProperty("recaptchaSecretKey", out _));
        }

        [Fact]
        public async Task AWriteIsReflectedInAReadAndInThePublicSettings()
        {
            await CreateAdminAndSignInAsync();

            var csrf = await GetCsrfTokenAsync();
            using var patched = await PatchJsonAsync(
                "/admin/settings",
                Full(
                    ("serviceName", "Panel of record"),
                    ("recaptchaSiteKey", "site-key"),
                    ("recaptchaSecretKey", "private-key"),
                    ("publicApiFloodLimit", 0),
                    ("publicApiFloodWhitelist", new[] { "203.0.113.7", "203.0.113.7" }),
                    ("enableTrials", false),
                    ("backupPeriod", "monthly")),
                csrf);

            await AssertCodeAsync(patched, HttpStatusCode.OK, "settings_updated");
            using var patchedBody = await ReadJsonAsync(patched);
            Assert.Equal(
                "The application settings have been updated.",
                patchedBody.RootElement.GetProperty("message").GetString());

            using var reread = await Client.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);
            var root = rereadBody.RootElement;

            Assert.Equal("Panel of record", root.GetProperty("serviceName").GetString());
            Assert.Equal("site-key", root.GetProperty("recaptchaSiteKey").GetString());
            Assert.True(root.GetProperty("recaptchaConfigured").GetBoolean());
            Assert.False(root.GetProperty("enableTrials").GetBoolean());
            Assert.Equal(0, root.GetProperty("publicApiFloodLimit").GetInt32());
            Assert.Equal(
                ["203.0.113.7"],
                root.GetProperty("publicApiFloodWhitelist")
                    .EnumerateArray()
                    .Select(element => element.GetString())
                    .ToArray());
            Assert.Equal("monthly", root.GetProperty("backupPeriod").GetString());
            Assert.False(root.TryGetProperty("recaptchaSecretKey", out _));

            using var guest = await Client.GetAsync(
                "/auth/public-settings",
                TestContext.Current.CancellationToken);
            using var guestBody = await ReadJsonAsync(guest);
            Assert.Equal("site-key", guestBody.RootElement.GetProperty("recaptchaSiteKey").GetString());
        }

        [Fact]
        public async Task TheSecretIsWriteOnlyAndAnAbsentOrEmptyOneKeepsIt()
        {
            await CreateAdminAndSignInAsync();

            using (var first = await PatchJsonAsync(
                "/admin/settings",
                Full(("recaptchaSecretKey", "stored-secret")),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(first, HttpStatusCode.OK, "settings_updated");
            }

            // Absent: the whole body is posted again without the field at all.
            var withoutSecret = Full();
            using (var absent = await PatchJsonAsync(
                "/admin/settings",
                withoutSecret,
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(absent, HttpStatusCode.OK, "settings_updated");
            }

            using (var afterAbsent = await Client.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken))
            using (var absentBody = await ReadJsonAsync(afterAbsent))
            {
                Assert.True(absentBody.RootElement.GetProperty("recaptchaConfigured").GetBoolean());
            }

            // Empty after trimming: the same "keep".
            using (var empty = await PatchJsonAsync(
                "/admin/settings",
                Full(("recaptchaSecretKey", "   ")),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(empty, HttpStatusCode.OK, "settings_updated");
            }

            using (var afterEmpty = await Client.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken))
            using (var emptyBody = await ReadJsonAsync(afterEmpty))
            {
                Assert.True(emptyBody.RootElement.GetProperty("recaptchaConfigured").GetBoolean());
            }
        }

        [Fact]
        public async Task AnInvalidWriteAnswersTheExactFieldNames()
        {
            await CreateAdminAndSignInAsync();

            var body = Full(
                ("serviceName", "   "),
                ("defaultTimeZone", "Mars/OlympusMons"),
                ("enigma2BouquetName", "   "),
                ("recaptchaSiteKey", new string('a', SettingsLimits.RecaptchaKeyMaxLength + 1)),
                ("recaptchaSecretKey", new string('b', SettingsLimits.RecaptchaKeyMaxLength + 1)),
                ("publicApiFloodLimit", -1),
                ("publicApiFloodWhitelist", new[] { "nonsense" }),
                ("retentionUserLogs", 0),
                ("retentionCreditLogs", 3651),
                ("retentionConnectionLogs", 0),
                ("retentionApiRequestFailures", 3651),
                ("retentionStreamProblems", 0),
                ("retentionMagEvents", 3651),
                ("backupPeriod", "yearly"),
                ("backupRotationCount", 1001),
                ("fallbackStreamDownUrl", "not-a-url"),
                ("fallbackUserBannedUrl", "ftp://example.test/banned"),
                ("fallbackUserExpiredUrl", "not-a-url"),
                ("fallbackMaxConnectionsUrl", "not-a-url"));

            using var response = await PatchJsonAsync(
                "/admin/settings",
                body,
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");

            using var errorBody = await ReadJsonAsync(response);
            var errors = errorBody.RootElement.GetProperty("errors");

            string[] expected =
            [
                "serviceName",
                "defaultTimeZone",
                "enigma2BouquetName",
                "recaptchaSiteKey",
                "recaptchaSecretKey",
                "publicApiFloodLimit",
                "publicApiFloodWhitelist",
                "retentionUserLogs",
                "retentionCreditLogs",
                "retentionConnectionLogs",
                "retentionApiRequestFailures",
                "retentionStreamProblems",
                "retentionMagEvents",
                "backupPeriod",
                "backupRotationCount",
                "fallbackStreamDownUrl",
                "fallbackUserBannedUrl",
                "fallbackUserExpiredUrl",
                "fallbackMaxConnectionsUrl"
            ];

            foreach (var field in expected)
            {
                Assert.True(
                    errors.TryGetProperty(field, out _),
                    $"The refusal did not name '{field}'.");
            }
        }

        [Fact]
        public async Task TheMagDefaultsAreAnswered()
        {
            await CreateAdminAndSignInAsync();

            using var response = await Client.GetAsync(
                "/admin/mag-settings",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            var root = body.RootElement;

            Assert.True(root.GetProperty("showAllCategories").GetBoolean());
            Assert.True(root.GetProperty("strictSecurity").GetBoolean());
            Assert.True(root.GetProperty("showChannelLogos").GetBoolean());
            Assert.True(root.GetProperty("showPreviewLogos").GetBoolean());
            Assert.Equal("mpeg-ts", root.GetProperty("defaultContainer").GetString());
            Assert.Equal("default", root.GetProperty("defaultTheme").GetString());
            Assert.Equal(180, root.GetProperty("recordingMaxLengthMinutes").GetInt32());
            Assert.Equal(10, root.GetProperty("maxLocalRecordings").GetInt32());
            Assert.Equal(3, root.GetProperty("playbackLimit").GetInt32());
            Assert.Equal(
                StbTypeCatalog.All.ToArray(),
                root.GetProperty("allowedStbTypes")
                    .EnumerateArray()
                    .Select(element => element.GetString()!)
                    .ToArray());
            Assert.Equal(
                ["MAG255", "MAG256", "MAG257"],
                root.GetProperty("allowedRecordingStbTypes")
                    .EnumerateArray()
                    .Select(element => element.GetString()!)
                    .ToArray());
        }

        [Fact]
        public async Task AMagWriteCanonicalizesTheContainerAndTheme()
        {
            await CreateAdminAndSignInAsync();

            using var patched = await PatchJsonAsync(
                "/admin/mag-settings",
                MagFull(
                    ("defaultContainer", "mpegts"),
                    ("defaultTheme", "OCEAN BLUE"),
                    ("allowedRecordingStbTypes", new[] { "mag255", "MAG255" }),
                    ("playbackLimit", 5000)),
                await GetCsrfTokenAsync());

            await AssertCodeAsync(patched, HttpStatusCode.OK, "mag_settings_updated");
            using var patchedBody = await ReadJsonAsync(patched);
            Assert.Equal(
                "The MAG settings have been updated.",
                patchedBody.RootElement.GetProperty("message").GetString());

            using var reread = await Client.GetAsync(
                "/admin/mag-settings",
                TestContext.Current.CancellationToken);
            using var rereadBody = await ReadJsonAsync(reread);
            var root = rereadBody.RootElement;

            Assert.Equal("mpeg-ts", root.GetProperty("defaultContainer").GetString());
            Assert.Equal("ocean blue", root.GetProperty("defaultTheme").GetString());
            Assert.Equal(
                ["MAG255"],
                root.GetProperty("allowedRecordingStbTypes")
                    .EnumerateArray()
                    .Select(element => element.GetString())
                    .ToArray());
            Assert.Equal(5000, root.GetProperty("playbackLimit").GetInt32());
        }

        [Fact]
        public async Task AnInvalidMagWriteNamesTheThemeAndTheLists()
        {
            await CreateAdminAndSignInAsync();

            using var response = await PatchJsonAsync(
                "/admin/mag-settings",
                MagFull(
                    ("defaultTheme", "cappuccino"),
                    ("allowedStbTypes", Array.Empty<string>()),
                    ("allowedRecordingStbTypes", new[] { "Commodore 64" }),
                    ("recordingMaxLengthMinutes", 0),
                    ("playbackLimit", SubscriptionLimits.ConnectionsMax + 1)),
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.BadRequest, "validation_failed");

            using var body = await ReadJsonAsync(response);
            var errors = body.RootElement.GetProperty("errors");

            Assert.True(errors.TryGetProperty("defaultTheme", out _));
            Assert.True(errors.TryGetProperty("allowedStbTypes", out _));
            Assert.True(errors.TryGetProperty("allowedRecordingStbTypes", out _));
            Assert.True(errors.TryGetProperty("recordingMaxLengthMinutes", out _));
            Assert.True(errors.TryGetProperty("playbackLimit", out _));
        }

        [Fact]
        public async Task TheSettingsRequireTheirOwnGrants()
        {
            await CreateAdminAndSignInAsync();

            // An administrator whose group may run the streams.
            var streamOperators = await CreateGroupAsync(
                "Stream operators",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewStreams,
                UserPermission.EditStreams);

            using var other = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "settings-guard", groupId: streamOperators);
            await SignInAsync(UserRole.Admin, "settings-guard", "Password1", other);

            using var forbiddenRead = await other.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenRead, HttpStatusCode.Forbidden, "permission_denied");

            using var forbiddenMagRead = await other.GetAsync(
                "/admin/mag-settings",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(forbiddenMagRead, HttpStatusCode.Forbidden, "permission_denied");

            var csrf = await GetCsrfTokenAsync(other);
            using var forbiddenWrite = await PatchJsonAsync("/admin/settings", Full(), csrf, other);
            await AssertCodeAsync(forbiddenWrite, HttpStatusCode.Forbidden, "permission_denied");

            // A view-only grant reads both screens and writes neither.
            var readers = await CreateGroupAsync(
                "Settings readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewSettings);

            using var reader = Fixture.CreateClient();
            await CreateUserAsync(UserRole.Admin, "settings-reader", groupId: readers);
            await SignInAsync(UserRole.Admin, "settings-reader", "Password1", reader);

            using var allowedRead = await reader.GetAsync(
                "/admin/settings",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);

            using var allowedMagRead = await reader.GetAsync(
                "/admin/mag-settings",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowedMagRead.StatusCode);

            csrf = await GetCsrfTokenAsync(reader);
            using var refusedWrite = await PatchJsonAsync(
                "/admin/settings",
                Full(),
                csrf,
                reader);
            await AssertCodeAsync(refusedWrite, HttpStatusCode.Forbidden, "permission_denied");

            csrf = await GetCsrfTokenAsync(reader);
            using var refusedMagWrite = await PatchJsonAsync(
                "/admin/mag-settings",
                MagFull(),
                csrf,
                reader);
            await AssertCodeAsync(refusedMagWrite, HttpStatusCode.Forbidden, "permission_denied");
        }

        [Fact]
        public async Task AWritesWithoutTheAntiforgeryPairIsRefusedBeforeTheBody()
        {
            await CreateAdminAndSignInAsync();

            using var application = await PatchJsonAsync("/admin/settings", Full());
            await AssertCodeAsync(application, HttpStatusCode.BadRequest, "csrf_invalid");

            using var mag = await PatchJsonAsync("/admin/mag-settings", MagFull());
            await AssertCodeAsync(mag, HttpStatusCode.BadRequest, "csrf_invalid");
        }

        [Fact]
        public async Task TheCollectionsAndARowPathAnswerMethodNotAllowed()
        {
            await CreateAdminAndSignInAsync();

            foreach (var route in new[] { "/admin/settings", "/admin/mag-settings" })
            {
                foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
                {
                    using var response = await SendAsync(method, route, Full());

                    await AssertCodeAsync(
                        response,
                        HttpStatusCode.MethodNotAllowed,
                        "method_not_allowed");
                }

                foreach (var method in new[]
                {
                    HttpMethod.Get,
                    HttpMethod.Post,
                    HttpMethod.Patch,
                    HttpMethod.Put,
                    HttpMethod.Delete
                })
                {
                    var id = Guid.NewGuid();
                    using var response = await SendAsync(method, $"{route}/{id}", Full());

                    await AssertCodeAsync(
                        response,
                        HttpStatusCode.MethodNotAllowed,
                        "method_not_allowed");
                }
            }
        }

        [Fact]
        public async Task WithTrialsSwitchedOffATrialIsRefusedOnBothPathsAndTheSwitchRestoresIt()
        {
            await CreateAdminAndSignInAsync();

            var owner = await CreateUserAsync(UserRole.Client, "trial-subscriber");
            var bouquet = await CreateBouquetAsync("Cinema");
            var groups = await GroupIdsAsync();
            var trialPlan = await CreatePlanAsync("Trial plan", groups, [bouquet], isTrial: true);

            using (var switchedOff = await PatchJsonAsync(
                "/admin/settings",
                Full(("enableTrials", false)),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(switchedOff, HttpStatusCode.OK, "settings_updated");
            }

            // The request's own flag.
            using (var refused = await PostJsonAsync(
                "/admin/subscriptions",
                Subscription(owner, isTrial: true, bouquetIds: [bouquet]),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");

                using var refusedBody = await ReadJsonAsync(refused);
                Assert.True(refusedBody.RootElement.GetProperty("errors").TryGetProperty("isTrial", out _));
            }

            // The trial tariff plan's flag: the check reads the effective row, so this path is
            // refused without a second rule.
            using (var refusedByPlan = await PostJsonAsync(
                "/admin/subscriptions",
                Subscription(owner, planId: trialPlan, playlistId: Playlist(), bouquetIds: [bouquet]),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(
                    refusedByPlan,
                    HttpStatusCode.BadRequest,
                    "validation_failed");

                using var refusedBody = await ReadJsonAsync(refusedByPlan);
                Assert.True(refusedBody.RootElement.GetProperty("errors").TryGetProperty("isTrial", out _));
            }

            using (var switchedOn = await PatchJsonAsync(
                "/admin/settings",
                Full(("enableTrials", true)),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(switchedOn, HttpStatusCode.OK, "settings_updated");
            }

            using var created = await PostJsonAsync(
                "/admin/subscriptions",
                Subscription(owner, isTrial: true, bouquetIds: [bouquet]),
                await GetCsrfTokenAsync());
            await AssertCodeAsync(created, HttpStatusCode.Created, "subscription_created");
        }

        [Fact]
        public async Task TheServiceNameTravelsIntoTheEmailSubjects()
        {
            await CreateAdminAndSignInAsync();

            using (var named = await PatchJsonAsync(
                "/admin/settings",
                Full(("serviceName", "Renamed panel")),
                await GetCsrfTokenAsync()))
            {
                await AssertCodeAsync(named, HttpStatusCode.OK, "settings_updated");
            }

            using var registered = await PostJsonAsync(
                "/auth/registrations",
                new
                {
                    username = "subject-check",
                    email = "subject@example.test",
                    password = "Password1"
                },
                await GetCsrfTokenAsync());

            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

            var message = Fixture.Email.LastFor("subject@example.test");

            Assert.NotNull(message);
            Assert.Equal("Your Renamed panel registration code", message!.Subject);
        }

        /// <summary>A valid application settings body, with the named values replaced.</summary>
        private static object Full(params (string Key, object? Value)[] overrides)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["serviceName"] = ApplicationSettingsDefaults.ServiceName,
                ["defaultTimeZone"] = "Europe/Berlin",
                ["enigma2BouquetName"] = ApplicationSettingsDefaults.Enigma2BouquetName,
                ["recaptchaSiteKey"] = string.Empty,
                ["enableTrials"] = true,
                ["publicApiFloodLimit"] = 20,
                ["publicApiFloodWhitelist"] = Array.Empty<string>(),
                ["requireUserAgent"] = false,
                ["retentionUserLogs"] = 30,
                ["retentionCreditLogs"] = 30,
                ["retentionConnectionLogs"] = 30,
                ["retentionApiRequestFailures"] = 30,
                ["retentionStreamProblems"] = 30,
                ["retentionMagEvents"] = 30,
                ["backupEnabled"] = false,
                ["backupPeriod"] = "daily",
                ["backupRotationCount"] = 10,
                ["fallbackStreamDownUrl"] = string.Empty,
                ["fallbackUserBannedUrl"] = string.Empty,
                ["fallbackUserExpiredUrl"] = string.Empty,
                ["fallbackMaxConnectionsUrl"] = string.Empty
            };

            foreach (var (key, value) in overrides)
            {
                body[key] = value;
            }

            return body;
        }

        /// <summary>A valid MAG settings body, with the named values replaced.</summary>
        private static object MagFull(params (string Key, object? Value)[] overrides)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["showAllCategories"] = true,
                ["strictSecurity"] = true,
                ["showChannelLogos"] = true,
                ["showPreviewLogos"] = true,
                ["defaultContainer"] = "mpeg-ts",
                ["defaultTheme"] = "default",
                ["recordingMaxLengthMinutes"] = 180,
                ["maxLocalRecordings"] = 10,
                ["playbackLimit"] = 3,
                ["allowedStbTypes"] = StbTypeCatalog.All,
                ["allowedRecordingStbTypes"] = new[] { "MAG255", "MAG256", "MAG257" }
            };

            foreach (var (key, value) in overrides)
            {
                body[key] = value;
            }

            return body;
        }

        /// <summary>A valid subscription body, with only the fields the tests vary.</summary>
        private static object Subscription(
            Guid owner,
            string? playlistId = "0123456789abcdef0123456789abcdef",
            string? planId = null,
            bool isTrial = false,
            IReadOnlyList<Guid>? bouquetIds = null)
            => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["userId"] = owner.ToString(),
                ["playlistId"] = playlistId,
                ["tariffPlanId"] = planId,
                ["maxConnections"] = 3,
                ["outputFormats"] = new[] { "mpegTs", "hls" },
                ["isTrial"] = isTrial,
                ["bouquetIds"] = bouquetIds?.Select(id => id.ToString()).ToArray()
            };

        private static string Playlist() => Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());

        private async Task<Guid> CreateBouquetAsync(string name)
        {
            using var response = await PostJsonAsync(
                "/admin/bouquets",
                new { name = $"{name} {Guid.NewGuid():N}" },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Created, "bouquet_created");

            return Guid.Parse(IdFromLocation(response));
        }

        private async Task<List<string>> GroupIdsAsync()
        {
            using var response = await Client.GetAsync(
                "/admin/user-groups",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);

            return body.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()!)
                .ToList();
        }

        private async Task<string> CreatePlanAsync(
            string name,
            IReadOnlyList<string> userGroupIds,
            IReadOnlyList<Guid> bouquetIds,
            bool isTrial = false)
        {
            using var response = await PostJsonAsync(
                "/admin/tariff-plans",
                new
                {
                    name = $"{name} {Guid.NewGuid():N}",
                    isTrial,
                    cost = 100,
                    durationValue = 1,
                    durationUnit = isTrial ? "days" : "months",
                    supportsM3u = true,
                    supportsMag = true,
                    supportsEnigma2 = true,
                    outputMpegTs = true,
                    outputHls = true,
                    strictStbDeviceId = false,
                    allowRestream = false,
                    maxConnections = 2,
                    allowMultipleCountries = true,
                    userGroupIds,
                    bouquetIds = bouquetIds.Select(id => id.ToString()).ToArray()
                },
                await GetCsrfTokenAsync());

            await AssertCodeAsync(response, HttpStatusCode.Created, "tariff_plan_created");

            return IdFromLocation(response);
        }

        private async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string path,
            object? body)
        {
            using var request = new HttpRequestMessage(method, path);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Add("X-CSRF-Token", await GetCsrfTokenAsync());

            return await Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
