using System.Net;
using System.Text.Json;
using Fluxy.Core.Users.Enums;
using Fluxy.IntegrationTests.Infrastructure;

namespace Fluxy.IntegrationTests.Subscriptions
{
    /// <summary>
    /// The tariff plan endpoints: the price list of the installation and the two relations that
    /// say who may buy a plan and what it delivers.
    /// </summary>
    /// <remarks>
    /// The rules these tests exist to pin are the ones a screen cannot show: that a name is unique
    /// in any spelling, that a trial cannot be longer than thirty days, that a plan which delivers
    /// nothing is refused, that the two relations are replaced by the set a request names and
    /// cleaned up by the database when their subject goes, and that the page really is filtered,
    /// counted and ordered on the server.
    /// </remarks>
    public sealed class TariffPlanTests : IntegrationTestBase
    {
        public TariffPlanTests(FluxyFixture fixture)
            : base(fixture)
        {
        }

        /// <summary>
        /// How many bouquets this test has had created for it by the helpers below.
        /// </summary>
        /// <remarks>
        /// A bouquet's name is unique for the installation, so two posts inside one test cannot
        /// both call theirs "Default bouquet" - the second create would be refused and the plan
        /// that followed would be refused for naming a bouquet that does not exist.
        /// </remarks>
        private int _defaultBouquetCount;

        [Fact]
        public async Task APlanRoundTripsWithEverySettingAndBothRelations()
        {
            await CreateAdminAndSignInAsync();

            var bouquet = await CreateBouquetAsync("Cinema");
            // The three base groups are the groups a plan may be offered to.
            var groups = await GroupIdsAsync();

            using var created = await PostPlanAsync(
                Plan("Standard", userGroupIds: groups, bouquetIds: [bouquet]));
            await AssertCodeAsync(created, HttpStatusCode.Created, "tariff_plan_created");

            var id = IdFromLocation(created);

            using var detail = await Client.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

            using var body = await ReadJsonAsync(detail);
            var plan = body.RootElement;

            Assert.Equal("Standard", plan.GetProperty("name").GetString());
            Assert.False(plan.GetProperty("isTrial").GetBoolean());
            Assert.Equal(120, plan.GetProperty("cost").GetInt32());
            Assert.Equal(1, plan.GetProperty("durationValue").GetInt32());
            Assert.Equal("months", plan.GetProperty("durationUnit").GetString());
            Assert.True(plan.GetProperty("supportsM3u").GetBoolean());
            Assert.True(plan.GetProperty("supportsMag").GetBoolean());
            Assert.False(plan.GetProperty("supportsEnigma2").GetBoolean());
            Assert.True(plan.GetProperty("outputMpegTs").GetBoolean());
            Assert.True(plan.GetProperty("outputHls").GetBoolean());
            Assert.False(plan.GetProperty("strictStbDeviceId").GetBoolean());
            Assert.False(plan.GetProperty("allowRestream").GetBoolean());
            Assert.Equal(2, plan.GetProperty("maxConnections").GetInt32());
            Assert.True(plan.GetProperty("allowMultipleCountries").GetBoolean());

            Assert.Equal(
                groups.OrderBy(value => value, StringComparer.Ordinal),
                Ids(plan.GetProperty("userGroupIds")).OrderBy(value => value, StringComparer.Ordinal));
            Assert.Equal([bouquet], Ids(plan.GetProperty("bouquetIds")));

            // The list carries the counts instead of the identifiers, which is what the table
            // draws beside each plan.
            using var list = await Client.GetAsync(
                "/admin/tariff-plans",
                TestContext.Current.CancellationToken);
            using var listBody = await ReadJsonAsync(list);
            var row = listBody.RootElement.GetProperty("items")[0];

            Assert.Equal(groups.Count, row.GetProperty("userGroupCount").GetInt32());
            Assert.Equal(1, row.GetProperty("bouquetCount").GetInt32());

            // A patch changes only what it names.
            using var patched = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { cost = 250, supportsEnigma2 = true },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(patched, HttpStatusCode.OK, "tariff_plan_updated");

            using var after = await Client.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            using var afterBody = await ReadJsonAsync(after);
            var afterPlan = afterBody.RootElement;

            Assert.Equal(250, afterPlan.GetProperty("cost").GetInt32());
            Assert.True(afterPlan.GetProperty("supportsEnigma2").GetBoolean());
            Assert.Equal("Standard", afterPlan.GetProperty("name").GetString());
            Assert.Equal(groups.Count, Ids(afterPlan.GetProperty("userGroupIds")).Length);

            // A delete answers 200 and a second one 404.
            using var deleted = await DeleteAsync(
                $"/admin/tariff-plans/{id}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(deleted, HttpStatusCode.OK, "tariff_plan_deleted");

            using var again = await DeleteAsync(
                $"/admin/tariff-plans/{id}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(again, HttpStatusCode.NotFound, "tariff_plan_not_found");
        }

        [Fact]
        public async Task ANameIsUniqueWithoutRegardToCase()
        {
            await CreateAdminAndSignInAsync();

            _ = await CreatePlanAsync("Standard");

            // The same word in another spelling is the same plan.
            using var lowerCase = await PostPlanAsync(Plan("standard"));
            await AssertCodeAsync(lowerCase, HttpStatusCode.Conflict, "tariff_plan_already_exists");
            using var lowerCaseBody = await ReadJsonAsync(lowerCase);
            Assert.True(lowerCaseBody.RootElement.GetProperty("errors").TryGetProperty("name", out _));

            using var upperCase = await PostPlanAsync(Plan("STANDARD"));
            await AssertCodeAsync(upperCase, HttpStatusCode.Conflict, "tariff_plan_already_exists");

            // Renaming a plan to its own spelling in another case is not a conflict with itself.
            var id = await CreatePlanAsync("Trial-ish");
            using var renamed = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { name = "TRIAL-ISH" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(renamed, HttpStatusCode.OK, "tariff_plan_updated");

            // Turning it into a trial is judged against the row as it would become: a month-long
            // plan cannot become a trial without its length changing too.
            using var monthTrial = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { isTrial = true },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(monthTrial, HttpStatusCode.BadRequest, "validation_failed");

            using var dayTrial = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { isTrial = true, durationValue = 7, durationUnit = "days" },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(dayTrial, HttpStatusCode.OK, "tariff_plan_updated");
        }

        [Fact]
        public async Task ATrialCannotBeLongerThanThirtyDays()
        {
            await CreateAdminAndSignInAsync();

            foreach (var broken in new object[]
            {
                // Thirty days stated in a unit the ceiling cannot hold.
                new { name = "Trial months", durationUnit = "months", durationValue = 1 },
                new { name = "Trial years", durationUnit = "years", durationValue = 1 },
                new { name = "Trial 31 days", durationUnit = "days", durationValue = 31 },
                new { name = "Trial 721 hours", durationUnit = "hours", durationValue = 721 }
            })
            {
                using var refused = await PostPlanAsync(Plan(name: "Trial", isTrial: true, overrides: broken));
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }

            // The two lengths the ceiling can state exactly are accepted.
            foreach (var accepted in new object[]
            {
                new { name = "Trial 30 days", durationUnit = "days", durationValue = 30 },
                new { name = "Trial 720 hours", durationUnit = "hours", durationValue = 720 }
            })
            {
                using var response = await PostPlanAsync(
                    Plan(name: "Trial", isTrial: true, overrides: accepted));
                await AssertCodeAsync(response, HttpStatusCode.Created, "tariff_plan_created");
            }
        }

        [Fact]
        public async Task APlanThatDeliversNothingIsRefused()
        {
            await CreateAdminAndSignInAsync();

            // No delivery method at all.
            using var noSupports = await PostPlanAsync(Plan(
                name: "Nothing",
                overrides: new
                {
                    supportsM3u = false,
                    supportsMag = false,
                    supportsEnigma2 = false
                }));
            await AssertCodeAsync(noSupports, HttpStatusCode.BadRequest, "validation_failed");
            using var noSupportsBody = await ReadJsonAsync(noSupports);
            Assert.True(noSupportsBody.RootElement.GetProperty("errors").TryGetProperty("supportsM3u", out _));

            // No output format.
            using var noOutput = await PostPlanAsync(Plan(
                name: "Silent",
                overrides: new { outputMpegTs = false, outputHls = false }));
            await AssertCodeAsync(noOutput, HttpStatusCode.BadRequest, "validation_failed");
            using var noOutputBody = await ReadJsonAsync(noOutput);
            Assert.True(noOutputBody.RootElement.GetProperty("errors").TryGetProperty("outputMpegTs", out _));

            // A strict STB check on a plan with no MAG support.
            using var strict = await PostPlanAsync(Plan(
                name: "Strict",
                overrides: new { supportsMag = false, strictStbDeviceId = true }));
            await AssertCodeAsync(strict, HttpStatusCode.BadRequest, "validation_failed");
            using var strictBody = await ReadJsonAsync(strict);
            Assert.True(strictBody.RootElement.GetProperty("errors").TryGetProperty("strictStbDeviceId", out _));

            // The bounds of a price, a length and a connection ceiling.
            foreach (var broken in new object[]
            {
                new { name = "Free-ish", cost = -1 },
                new { name = "Zero length", durationValue = 0 },
                new { name = "No connections", maxConnections = 0 },
                new { name = "Too many", maxConnections = 10001 },
                new { name = "Bad unit", durationUnit = "weeks" },
                new { name = " ", cost = 10 }
            })
            {
                using var refused = await PostPlanAsync(Plan(
                    name: "Broken",
                    overrides: broken));
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }
        }

        [Fact]
        public async Task APlanMustBeBuyableAndDeliverSomething()
        {
            await CreateAdminAndSignInAsync();

            var bouquet = await CreateBouquetAsync("Bouquet");

            // No group at all: the *absent* list is the default (every base group), so this asks
            // for the empty set explicitly - "nobody may buy it", which is refused.
            using var noGroups = await PostPlanAsync(Plan(
                name: "Nobody",
                userGroupIds: [],
                bouquetIds: [bouquet]));
            await AssertCodeAsync(noGroups, HttpStatusCode.BadRequest, "validation_failed");
            using var noGroupsBody = await ReadJsonAsync(noGroups);
            Assert.True(noGroupsBody.RootElement.GetProperty("errors").TryGetProperty("userGroupIds", out _));

            // No bouquet: there is no default for content, so the empty set is refused too.
            using var noBouquets = await PostPlanAsync(Plan(
                name: "Nothing",
                bouquetIds: []));
            await AssertCodeAsync(noBouquets, HttpStatusCode.BadRequest, "validation_failed");
            using var noBouquetsBody = await ReadJsonAsync(noBouquets);
            Assert.True(noBouquetsBody.RootElement.GetProperty("errors").TryGetProperty("bouquetIds", out _));

            // The body naming *no* group key at all is the default: every base group.
            var groups = await GroupIdsAsync();
            using var created = await PostPlanAsync(Plan(
                name: "Default audience",
                bouquetIds: [bouquet],
                overrides: new { userGroupIds = (string[]?)null }));
            await AssertCodeAsync(created, HttpStatusCode.Created, "tariff_plan_created");

            using var detail = await Client.GetAsync(
                $"/admin/tariff-plans/{IdFromLocation(created)}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);

            Assert.Equal(
                groups.OrderBy(value => value, StringComparer.Ordinal),
                Ids(detailBody.RootElement.GetProperty("userGroupIds"))
                    .OrderBy(value => value, StringComparer.Ordinal));
        }

        [Fact]
        public async Task TheRelationsAreReplacedByTheSetTheRequestNames()
        {
            await CreateAdminAndSignInAsync();

            var first = await CreateBouquetAsync("First");
            var second = await CreateBouquetAsync("Second");
            var groups = await GroupIdsAsync();
            var id = await CreatePlanAsync("Relations", bouquetIds: [first], userGroupIds: groups);

            // Clearing a relation is refused: a plan has to be buyable by someone and deliver
            // something, so an empty set is the same refusal as an empty create. The plan keeps
            // the links it had - a refused patch changes nothing.
            foreach (var cleared in new object[]
            {
                new { bouquetIds = Array.Empty<string>() },
                new { userGroupIds = Array.Empty<string>() }
            })
            {
                using var refused = await PatchJsonAsync(
                    $"/admin/tariff-plans/{id}",
                    cleared,
                    await GetCsrfTokenAsync());
                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }

            using var stillLinked = await Client.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            using var stillLinkedBody = await ReadJsonAsync(stillLinked);
            Assert.Equal([first], Ids(stillLinkedBody.RootElement.GetProperty("bouquetIds")));
            Assert.Equal(
                groups.OrderBy(value => value, StringComparer.Ordinal),
                Ids(stillLinkedBody.RootElement.GetProperty("userGroupIds"))
                    .OrderBy(value => value, StringComparer.Ordinal));

            // Naming one replaces the whole set - there is no "add" here.
            using var replaced = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { bouquetIds = new[] { second } },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(replaced, HttpStatusCode.OK, "tariff_plan_updated");

            using var replacedRead = await Client.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            using var replacedBody = await ReadJsonAsync(replacedRead);
            Assert.Equal([second], Ids(replacedBody.RootElement.GetProperty("bouquetIds")));

            // A body naming an identifier that exists nowhere is refused with the field named.
            foreach (var broken in new object[]
            {
                new { bouquetIds = new[] { Guid.NewGuid().ToString() } },
                new { userGroupIds = new[] { Guid.NewGuid().ToString() } },
                new { bouquetIds = new[] { second, second } }
            })
            {
                using var refused = await PatchJsonAsync(
                    $"/admin/tariff-plans/{id}",
                    broken,
                    await GetCsrfTokenAsync());

                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }
        }

        [Fact]
        public async Task DeletingASubjectTakesItsLinksWithIt()
        {
            await CreateAdminAndSignInAsync();

            var kept = await CreateBouquetAsync("Kept");
            var doomed = await CreateBouquetAsync("Doomed");
            var id = await CreatePlanAsync("Cascade", bouquetIds: [kept, doomed]);

            using var removed = await DeleteAsync(
                $"/admin/bouquets/{doomed}",
                await GetCsrfTokenAsync());
            await AssertCodeAsync(removed, HttpStatusCode.OK, "bouquet_deleted");

            // The foreign key cascades, so the link left with the bouquet and the plan no longer
            // counts it - a count that lied would make the table lie too.
            using var list = await Client.GetAsync(
                "/admin/tariff-plans",
                TestContext.Current.CancellationToken);
            using var body = await ReadJsonAsync(list);
            Assert.Equal(1, body.RootElement.GetProperty("items")[0].GetProperty("bouquetCount").GetInt32());

            using var detail = await Client.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            using var detailBody = await ReadJsonAsync(detail);
            Assert.Equal([kept], Ids(detailBody.RootElement.GetProperty("bouquetIds")));

            // The plan itself still exists: a bouquet is a member of the price list, not its owner.
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        }

        [Fact]
        public async Task TheTableIsPagedSearchedAndSortedOnTheServer()
        {
            await CreateAdminAndSignInAsync();

            _ = await CreatePlanAsync("Alpha", cost: 300);
            _ = await CreatePlanAsync("Beta", cost: 100);
            _ = await CreatePlanAsync("Gamma", cost: 200);

            using var firstPage = await Client.GetAsync(
                "/admin/tariff-plans?page=1&pageSize=2",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);

            using var firstBody = await ReadJsonAsync(firstPage);
            Assert.Equal(3, firstBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(2, firstBody.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(
                new[] { "Alpha", "Beta" },
                Names(firstBody.RootElement.GetProperty("items")));

            using var secondPage = await Client.GetAsync(
                "/admin/tariff-plans?page=2&pageSize=2",
                TestContext.Current.CancellationToken);
            using var secondBody = await ReadJsonAsync(secondPage);
            Assert.Equal(3, secondBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(new[] { "Gamma" }, Names(secondBody.RootElement.GetProperty("items")));

            // Search narrows the list before it is counted, which is the whole reason the filter
            // is not applied in the browser.
            using var searched = await Client.GetAsync(
                "/admin/tariff-plans?search=amm",
                TestContext.Current.CancellationToken);
            using var searchedBody = await ReadJsonAsync(searched);
            Assert.Equal(1, searchedBody.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(new[] { "Gamma" }, Names(searchedBody.RootElement.GetProperty("items")));

            // Cost sorts on the stored column, in either direction.
            using var cheapest = await Client.GetAsync(
                "/admin/tariff-plans?sortBy=cost&sortOrder=asc",
                TestContext.Current.CancellationToken);
            using var cheapestBody = await ReadJsonAsync(cheapest);
            Assert.Equal(
                new[] { "Beta", "Gamma", "Alpha" },
                Names(cheapestBody.RootElement.GetProperty("items")));

            using var dearest = await Client.GetAsync(
                "/admin/tariff-plans?sortBy=cost&sortOrder=desc",
                TestContext.Current.CancellationToken);
            using var dearestBody = await ReadJsonAsync(dearest);
            Assert.Equal(
                new[] { "Alpha", "Gamma", "Beta" },
                Names(dearestBody.RootElement.GetProperty("items")));

            // The bounds are refused rather than clamped, and a value that names nothing is
            // refused rather than treated as "no filter".
            foreach (var broken in new[]
            {
                "page=0",
                "pageSize=1001",
                $"search={new string('a', 201)}",
                "sortBy=duration",
                "sortOrder=upwards"
            })
            {
                using var refused = await Client.GetAsync(
                    $"/admin/tariff-plans?{broken}",
                    TestContext.Current.CancellationToken);

                await AssertCodeAsync(refused, HttpStatusCode.BadRequest, "validation_failed");
            }
        }

        [Fact]
        public async Task PlanWritesRequireAntiforgeryAndThePlanPermissions()
        {
            using var anonymous = Fixture.CreateClient();
            using var anonymousList = await anonymous.GetAsync(
                "/admin/tariff-plans",
                TestContext.Current.CancellationToken);
            await AssertCodeAsync(anonymousList, HttpStatusCode.Unauthorized, "auth_required");

            await CreateAdminAndSignInAsync();
            using var noCsrf = await PostJsonAsync("/admin/tariff-plans", Plan("No Token"));
            await AssertCodeAsync(noCsrf, HttpStatusCode.BadRequest, "csrf_invalid");

            var id = await CreatePlanAsync("Guarded");

            // The two plan grants are one pair per resource, like every other resource here: an
            // administrator whose group holds only the read grant may read the table and one plan
            // but may change nothing.
            var readerClient = Fixture.CreateClient();
            var readerGroup = await CreateGroupAsync(
                "plan-readers",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewTariffPlans);
            _ = await CreateUserAsync(UserRole.Admin, "plan-reader", groupId: readerGroup);
            await SignInAsync(UserRole.Admin, "plan-reader", "Password1", readerClient);

            using var readable = await readerClient.GetAsync(
                "/admin/tariff-plans",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

            using var readableDetail = await readerClient.GetAsync(
                $"/admin/tariff-plans/{id}",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, readableDetail.StatusCode);

            using var refusedWrite = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { cost = 1 },
                await GetCsrfTokenAsync(readerClient),
                readerClient);
            Assert.Equal(HttpStatusCode.Forbidden, refusedWrite.StatusCode);

            var editorClient = Fixture.CreateClient();
            var editorGroup = await CreateGroupAsync(
                "plan-editors",
                UserRole.Admin,
                UserStatus.Registered,
                UserPermission.ViewTariffPlans,
                UserPermission.EditTariffPlans);
            _ = await CreateUserAsync(UserRole.Admin, "plan-editor", groupId: editorGroup);
            await SignInAsync(UserRole.Admin, "plan-editor", "Password1", editorClient);

            using var allowed = await PatchJsonAsync(
                $"/admin/tariff-plans/{id}",
                new { cost = 1 },
                await GetCsrfTokenAsync(editorClient),
                editorClient);
            await AssertCodeAsync(allowed, HttpStatusCode.OK, "tariff_plan_updated");
        }

        /// <summary>
        /// A plan a test would create, with the values a test overrides.
        /// </summary>
        /// <remarks>
        /// An absent bouquet list is left as an empty array here and filled in by
        /// <see cref="PostPlanAsync"/>, because a plan must deliver at least one bouquet and the
        /// real identifier can only be created asynchronously. A refusal test that forgot to name
        /// one would otherwise assert the wrong refusal. <paramref name="userGroupIds"/> defaults
        /// to null, which the dictionary writes as JSON `null` - the server reads that as "not
        /// named" and answers with the base groups.
        /// </remarks>
        private static object Plan(
            string name,
            bool isTrial = false,
            IReadOnlyList<string>? userGroupIds = null,
            IReadOnlyList<string>? bouquetIds = null,
            object? overrides = null)
        {
            var body = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["isTrial"] = isTrial,
                ["cost"] = 120,
                ["durationValue"] = 1,
                ["durationUnit"] = "months",
                ["supportsM3u"] = true,
                ["supportsMag"] = true,
                ["supportsEnigma2"] = false,
                ["outputMpegTs"] = true,
                ["outputHls"] = true,
                ["strictStbDeviceId"] = false,
                ["allowRestream"] = false,
                ["maxConnections"] = 2,
                ["allowMultipleCountries"] = true,
                ["userGroupIds"] = userGroupIds,
                ["bouquetIds"] = bouquetIds
            };

            if (overrides is not null)
            {
                foreach (var property in overrides.GetType().GetProperties())
                {
                    body[property.Name] = property.GetValue(overrides);
                }
            }

            return body;
        }

        /// <summary>
        /// Creates a plan that the service accepts, creating a bouquet for it if the test names
        /// none.
        /// </summary>
        /// <remarks>
        /// A plan must deliver at least one bouquet now, so a helper that let a test say nothing
        /// would make every one of these tests a refusal test. The bouquet is created through the
        /// API rather than planted, which is the same path an operator takes.
        /// </remarks>
        private async Task<string> CreatePlanAsync(
            string name,
            int cost = 120,
            IReadOnlyList<string>? userGroupIds = null,
            IReadOnlyList<string>? bouquetIds = null)
        {
            var bouquets = bouquetIds ?? [await CreateBouquetAsync($"Bouquet of {name}")];

            using var response = await PostPlanAsync(Plan(
                name,
                userGroupIds: userGroupIds,
                bouquetIds: bouquets,
                overrides: new { cost }));
            await AssertCodeAsync(response, HttpStatusCode.Created, "tariff_plan_created");

            return IdFromLocation(response);
        }

        /// <summary>
        /// Posts a plan body, supplying a bouquet when the body names none.
        /// </summary>
        /// <remarks>
        /// The same courtesy <see cref="CreatePlanAsync"/> gives, for the tests that post directly
        /// because they assert a refusal about something *else* - a trial that is too long, a plan
        /// with no output format. Without it those bodies would fail the bouquet minimum first and
        /// every one of them would be testing that rule instead. A body that names an explicit
        /// empty array is left alone, because clearing the bouquets is itself under test.
        /// </remarks>
        private async Task<HttpResponseMessage> PostPlanAsync(object body)
        {
            if (body is IDictionary<string, object?> dictionary
                && dictionary.TryGetValue("bouquetIds", out var bouquets)
                && bouquets is null)
            {
                dictionary["bouquetIds"] = new[] { await CreateBouquetAsync($"Default bouquet {++_defaultBouquetCount}") };
            }

            return await PostJsonAsync("/admin/tariff-plans", body, await GetCsrfTokenAsync());
        }

        private async Task<string> CreateBouquetAsync(string name)
        {
            using var response = await PostJsonAsync(
                "/admin/bouquets",
                new { name },
                await GetCsrfTokenAsync());
            await AssertCodeAsync(response, HttpStatusCode.Created, "bouquet_created");

            return IdFromLocation(response);
        }

        /// <summary>The identifiers of the three base groups every installation has.</summary>
        private async Task<List<string>> GroupIdsAsync()
        {
            using var response = await Client.GetAsync(
                "/admin/user-groups",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var body = await ReadJsonAsync(response);
            return body.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetString()!)
                .ToList();
        }

        private static string?[] Names(JsonElement items)
            => items.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();

        private static string?[] Ids(JsonElement items)
            => items.EnumerateArray().Select(item => item.GetString()).ToArray();
    }
}
