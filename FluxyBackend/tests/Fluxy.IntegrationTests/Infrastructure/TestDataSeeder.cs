using Fluxy.Application.Users.Utils;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Requests.Commands;
using Fluxy.Core.Requests.Repositories;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Creates the rows a test starts from without going through the API.
    /// </summary>
    public static class TestDataSeeder
    {
        /// <summary>
        /// Recreates the three groups the installation cannot operate without, with the permissions
        /// the catalog assigns to their roles.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            await EnsureBaseGroupsAsync(groups, unitOfWork, cancellationToken);
        }

        /// <summary>
        /// Recreates the three base groups through repositories already resolved from a scope.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken = default)
        {
            foreach (var role in new[] { UserRole.Client, UserRole.Reseller, UserRole.Admin })
            {
                var id = BaseUserGroups.ForRole(role);
                var wanted = UserPermissionCatalog.ForRole(role).ToHashSet();
                var existing = await groups.GetSummaryAsync(id, cancellationToken);

                if (existing is null)
                {
                    await groups.AddWithPermissionsAsync(
                        new NewUserGroup
                        {
                            Id = id,
                            Name = BaseUserGroups.NameOf(role),
                            Role = role,
                            Status = UserStatus.Registered
                        },
                        wanted,
                        cancellationToken);

                    continue;
                }

                if (existing.Role != role)
                {
                    await groups.SetRoleAsync(id, role, cancellationToken);
                }

                if (existing.Status != UserStatus.Registered)
                {
                    await groups.SetStatusAsync(id, UserStatus.Registered, cancellationToken);
                }

                var current = (await groups.GetPermissionsAsync(id, cancellationToken)).ToHashSet();
                if (!current.SetEquals(wanted))
                {
                    await groups.ReplacePermissionsAsync(id, wanted, cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Creates an account in its role's base group, or in <paramref name="groupId"/> when one is
        /// named. The password is hashed with a low cost factor; only tests ever read it.
        /// </summary>
        public static async Task<Guid> CreateUserAsync(
            IServiceProvider services,
            string username,
            string password,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            Guid? groupId = null,
            string? email = null,
            string? timeZone = null,
            bool geoProtectionEnabled = false,
            bool bindSessionToIp = false,
            int credits = 0,
            CancellationToken cancellationToken = default)
        {
            var users = services.GetRequiredService<IUserRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var now = DateTimeOffset.UtcNow;

            var id = await users.AddAsync(
                new NewAccount
                {
                    Username = username,
                    Email = EmailNormalizer.Normalize(email ?? $"{username}@example.test"),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
                    GroupId = groupId ?? BaseUserGroups.ForRole(role),
                    Status = status,
                    RegisteredAt = status is UserStatus.Unregistered ? null : now,
                    TimeZone = timeZone,
                    GeoProtectionEnabled = geoProtectionEnabled,
                    BindSessionToIp = bindSessionToIp,
                    Credits = credits
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a custom group with the permissions the role is allowed to hold.
        /// </summary>
        public static async Task<Guid> CreateGroupAsync(
            IServiceProvider services,
            string name,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            CancellationToken cancellationToken = default,
            params UserPermission[] permissions)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var wanted = permissions
                .Where(permission => UserPermissionCatalog.IsValidForRole(permission, role))
                .ToHashSet();

            var id = await groups.AddWithPermissionsAsync(
                new NewUserGroup
                {
                    Name = name,
                    Role = role,
                    Status = status
                },
                wanted,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a subscription owned by <paramref name="ownerId"/> and issued by
        /// <paramref name="issuerId"/>, carrying a playlist and no bouquets.
        /// </summary>
        /// <remarks>
        /// Planted through the Core repository, like every other row this class writes. The
        /// service's minimum-one-bouquet rule is a rule of the service, not of the table, and a
        /// test that only needs an owner for a connection has no reason to walk the bouquet
        /// surface to get one.
        /// </remarks>
        public static async Task<Guid> CreateSubscriptionAsync(
            IServiceProvider services,
            Guid ownerId,
            Guid issuerId,
            CancellationToken cancellationToken = default)
        {
            var subscriptions = services.GetRequiredService<ISubscriptionRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var id = await subscriptions.AddAsync(
                new NewSubscription
                {
                    UserId = ownerId,
                    ResellerId = issuerId,
                    PlaylistId = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()),
                    BouquetIds = []
                },
                [],
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a video stream of the installation's own, under a category of the kind.
        /// </summary>
        /// <remarks>
        /// A connection names its stream by an opaque identifier with no foreign key, so the title
        /// a page draws is resolved from this row when it exists - which is exactly what these
        /// helpers exist to plant, and why the category has to exist first.
        /// </remarks>
        public static async Task<Guid> CreateVideoStreamAsync(
            IServiceProvider services,
            string name,
            CancellationToken cancellationToken = default)
        {
            var categories = services.GetRequiredService<IContentCategoryRepository>();
            var streams = services.GetRequiredService<IVideoStreamRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var categoryId = await categories.AddAsync(
                new NewContentCategory
                {
                    Name = $"Video {Guid.NewGuid():N}",
                    Type = ContentType.VideoStream
                },
                1,
                cancellationToken);

            var id = await streams.AddAsync(
                new VideoStreamWrite
                {
                    Name = name,
                    Type = VideoStreamType.Permanent,
                    Status = VideoStreamStatus.Running,
                    CategoryId = categoryId,
                    DelayMinutes = 0,
                    ArchiveDays = 0,
                    VodItems = [],
                    Sources = ["http://example.test/stream"],
                    Files = []
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>Creates an audio stream of the installation's own, under a category of the kind.</summary>
        public static async Task<Guid> CreateAudioStreamAsync(
            IServiceProvider services,
            string name,
            CancellationToken cancellationToken = default)
        {
            var categories = services.GetRequiredService<IContentCategoryRepository>();
            var streams = services.GetRequiredService<IAudioStreamRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var categoryId = await categories.AddAsync(
                new NewContentCategory
                {
                    Name = $"Audio {Guid.NewGuid():N}",
                    Type = ContentType.AudioStream
                },
                1,
                cancellationToken);

            var id = await streams.AddAsync(
                new AudioStreamWrite
                {
                    Name = name,
                    Type = AudioStreamType.OnDemand,
                    Status = AudioStreamStatus.Running,
                    CategoryId = categoryId,
                    Sources = ["http://example.test/audio"]
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Plants one connection log row directly, and returns its identifier.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="subscriptionId">Subscription the connection was made under.</param>
        /// <param name="streamKind">Which medium it delivered.</param>
        /// <param name="streamId">Opaque identifier of the stream.</param>
        /// <param name="startedAt">When the session opened.</param>
        /// <param name="endedAt">When it finished, or null while it is live.</param>
        /// <param name="ip">Address the session came from.</param>
        /// <param name="userAgent">User agent the session presented, or null.</param>
        /// <param name="countryCode">Stored upper-case country, or null.</param>
        /// <param name="isp">Stored provider, or null.</param>
        /// <param name="broadcastPid">Reported process identifier, or null.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>A deliberate SQL fixture.</b> A connection log row has no capture path in this pass -
        /// the process that relays a stream will own it - and the row itself carries nothing but
        /// stored values, so it is written as a plain statement naming exactly the columns the
        /// assertion reads.
        /// </para>
        /// <para>
        /// The insert intent that does exist, <see cref="IConnectionLogRepository.AddRangeAsync"/>,
        /// cannot serve this fixture: it answers <c>int</c> - how many rows it staged - and the row's
        /// identifier is generated inside the entity, so the caller never learns the id a test has
        /// to assert a filtered page against. Its remarks also place it with the Development trial
        /// seeder, and no production endpoint reaches it. The statement below is what gives the
        /// test the id it needs.
        /// </para>
        /// <para>
        /// It names the table and its columns, exactly as the expired-registration-code fixture
        /// names <c>users</c>, and never an entity, a context or any other persistence type - the
        /// rule the test project follows. Everything the row points at - the account, the
        /// subscription, the stream - is planted through its Core repository above.
        /// </para>
        /// </remarks>
        public static async Task<Guid> PlantConnectionLogAsync(
            NpgsqlConnection connection,
            Guid subscriptionId,
            ConnectionStreamKind streamKind,
            Guid streamId,
            DateTimeOffset startedAt,
            DateTimeOffset? endedAt = null,
            string ip = "203.0.113.9",
            string? userAgent = "Mozilla/5.0 (Test)",
            string? countryCode = "DE",
            string? isp = "Example ISP",
            int? broadcastPid = null,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();

            await using var command = new NpgsqlCommand(
                "insert into connection_logs " +
                "(id, subscription_id, stream_kind, stream_id, ip, user_agent, country_code, " +
                "isp, started_at, ended_at, broadcast_pid, created_at, updated_at) " +
                "values (@id, @subscription_id, @stream_kind, @stream_id, @ip, @user_agent, " +
                "@country_code, @isp, @started_at, @ended_at, @broadcast_pid, @now, @now)",
                connection);

            var now = DateTimeOffset.UtcNow;

            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("subscription_id", subscriptionId);
            command.Parameters.AddWithValue("stream_kind", (short)streamKind);
            command.Parameters.AddWithValue("stream_id", streamId);
            command.Parameters.AddWithValue("ip", ip);
            command.Parameters.AddWithValue("user_agent", (object?)userAgent ?? DBNull.Value);
            command.Parameters.AddWithValue("country_code", (object?)countryCode ?? DBNull.Value);
            command.Parameters.AddWithValue("isp", (object?)isp ?? DBNull.Value);
            command.Parameters.AddWithValue("started_at", startedAt);
            command.Parameters.AddWithValue("ended_at", (object?)endedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("broadcast_pid", (object?)broadcastPid ?? DBNull.Value);
            command.Parameters.AddWithValue("now", now);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return id;
        }

        /// <summary>Counts the connection log rows, so a test can prove a refusal wrote nothing.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task<int> CountConnectionLogsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                "select count(*) from connection_logs",
                connection);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        /// <summary>
        /// Plants one credit log row directly, and returns its identifier.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="resellerId">Reseller whose balance the row moved.</param>
        /// <param name="issuedById">Issuer, or null on a reseller-spent row.</param>
        /// <param name="amount">Signed change.</param>
        /// <param name="description">English sentence of the change.</param>
        /// <param name="createdAt">Creation instant, so an ordering or a range can be pinned.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>A deliberate SQL fixture.</b> The production write path is the account service, and
        /// it stamps <c>created_at</c> with the request's own clock; a test that has to pin two
        /// rows to the same instant, or to place one outside a chosen range, cannot reach that
        /// instant through the API. The statement names the table and its columns - never an
        /// entity or a context - which is the rule the connection log fixture follows too.
        /// </remarks>
        public static async Task<Guid> PlantCreditLogAsync(
            NpgsqlConnection connection,
            Guid resellerId,
            Guid? issuedById,
            int amount,
            string description,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();

            await using var command = new NpgsqlCommand(
                "insert into credit_logs " +
                "(id, reseller_id, issued_by_id, amount, description, created_at, updated_at) " +
                "values (@id, @reseller_id, @issued_by_id, @amount, @description, @created_at, @created_at)",
                connection);

            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("reseller_id", resellerId);
            command.Parameters.AddWithValue("issued_by_id", (object?)issuedById ?? DBNull.Value);
            command.Parameters.AddWithValue("amount", amount);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("created_at", createdAt);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return id;
        }

        /// <summary>Counts the credit log rows, so a test can prove a refusal wrote nothing.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task<int> CountCreditLogsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                "select count(*) from credit_logs",
                connection);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        /// <summary>
        /// Plants one user log row directly, and returns its identifier.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="userId">Account the row is about, or null for an unknown-account attempt.</param>
        /// <param name="recordType">Which kind of event it records.</param>
        /// <param name="isSuccess">Whether the attempt worked.</param>
        /// <param name="description">Stored English sentence.</param>
        /// <param name="createdAt">Creation instant, so an ordering or a range can be pinned.</param>
        /// <param name="ip">Address the event came from, or null.</param>
        /// <param name="isp">Provider, or null.</param>
        /// <param name="countryCode">Upper-case country, or null.</param>
        /// <param name="userAgent">User agent, or null.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>A deliberate SQL fixture.</b> The production write path stamps <c>created_at</c> with
        /// the request clock, so a test that has to pin rows to chosen instants - or to the same
        /// instant, to exercise the identifier tie-break - cannot reach them through the API. The
        /// statement names the table and its columns, never an entity or a context, which is the
        /// rule the connection and credit log fixtures follow too.
        /// </remarks>
        public static async Task<Guid> PlantUserLogAsync(
            NpgsqlConnection connection,
            Guid? userId,
            UserLogRecordType recordType,
            bool isSuccess,
            string description,
            DateTimeOffset createdAt,
            string? ip = "203.0.113.9",
            string? isp = "Example ISP",
            string? countryCode = "DE",
            string? userAgent = "Mozilla/5.0 (Test)",
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();

            await using var command = new NpgsqlCommand(
                "insert into user_logs " +
                "(id, user_id, record_type, is_success, description, ip, isp, country_code, " +
                "user_agent, created_at, updated_at) " +
                "values (@id, @user_id, @record_type, @is_success, @description, @ip, @isp, " +
                "@country_code, @user_agent, @created_at, @created_at)",
                connection);

            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("user_id", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("record_type", (short)recordType);
            command.Parameters.AddWithValue("is_success", isSuccess);
            command.Parameters.AddWithValue("description", description);
            command.Parameters.AddWithValue("ip", (object?)ip ?? DBNull.Value);
            command.Parameters.AddWithValue("isp", (object?)isp ?? DBNull.Value);
            command.Parameters.AddWithValue("country_code", (object?)countryCode ?? DBNull.Value);
            command.Parameters.AddWithValue("user_agent", (object?)userAgent ?? DBNull.Value);
            command.Parameters.AddWithValue("created_at", createdAt);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return id;
        }

        /// <summary>Counts the user log rows, so a test can prove a refusal wrote nothing.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task<int> CountUserLogsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                "select count(*) from user_logs",
                connection);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        /// <summary>
        /// Plants API request failure rows through the Core repository, in one save.
        /// </summary>
        /// <param name="services">Provider of one request scope.</param>
        /// <param name="failures">Rows to stage, in the order they are given.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>The repository is the only writer this capability has.</b> There is no production
        /// write path, so a test plants rows the same way the Development seeder does:
        /// <c>AddRangeAsync</c> stages them and one <see cref="IUnitOfWork.SaveChangesAsync"/>
        /// commits them. No persistence type is named here, which is the rule every helper in this
        /// class follows.
        /// </para>
        /// <para>
        /// <b>One save stamps one instant.</b> The audit columns come from the host's clock, so all
        /// rows in one call share <c>created_at</c> - which is exactly the case the identifier
        /// tie-break exists for. A test that needs two instants advances the fixture's
        /// <c>FakeTimeProvider</c> and plants again.
        /// </para>
        /// </remarks>
        public static async Task PlantApiRequestFailuresAsync(
            IServiceProvider services,
            IReadOnlyList<NewApiRequestFailure> failures,
            CancellationToken cancellationToken = default)
        {
            var repository = services.GetRequiredService<IApiRequestFailureRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            await repository.AddRangeAsync(failures, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Installs a trigger that refuses every user log insert, so a test can force the audit
        /// write to fail while the audited operation succeeds.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name of the trigger, so it can be dropped afterwards.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>The only way to prove the write path fails open from outside.</b> The log row is
        /// written by the flow that already committed its business change, so the interesting
        /// failure is one the database raises on the log insert alone. A trigger is that failure,
        /// and it is installed under a name the test controls so it can be dropped in a
        /// <c>finally</c>.
        /// </remarks>
        public static async Task InstallUserLogFailureTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"create or replace function {triggerName}_fn() returns trigger as $$ " +
                "begin " +
                "raise exception 'forced user log failure'; " +
                "end; $$ language plpgsql; " +
                $"drop trigger if exists {triggerName} on user_logs; " +
                $"create trigger {triggerName} before insert on user_logs " +
                $"for each row execute function {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>Removes a trigger installed by <see cref="InstallUserLogFailureTriggerAsync"/>.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name the trigger was installed under.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task DropUserLogFailureTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"drop trigger if exists {triggerName} on user_logs; " +
                $"drop function if exists {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Installs a trigger that makes every insert into <c>pending_changes</c> fail with the
        /// unique-index refusal the staging race produces, so a test can exercise the branch
        /// that answers "a code is already waiting" without needing a genuine simultaneous
        /// request.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name of the trigger, so it can be dropped afterwards.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>The race is otherwise not reachable from a test.</b> Two requests have to pass the
        /// repository's read before either one saves, and an HTTP test cannot hold that window
        /// open. The trigger raises the same SQLSTATE the unique index raises and names the same
        /// constraint, so the service takes the exact branch a real loser takes - and the
        /// response the test sees is the one production gives. The constraint name mirrors
        /// <c>PendingChangeService.PendingChangeIndexName</c>
        /// (<c>Fluxy.Application/Users/Services/PendingChangeService.cs</c>, a private const),
        /// spelled out here because the fixture is SQL.
        /// </remarks>
        public static async Task InstallPendingChangeRaceTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"create or replace function {triggerName}_fn() returns trigger as $$ " +
                "begin " +
                "raise exception using errcode = '23505', " +
                "constraint = 'IX_pending_changes_user_id', " +
                "message = 'forced staging race'; " +
                "end; $$ language plpgsql; " +
                $"drop trigger if exists {triggerName} on pending_changes; " +
                $"create trigger {triggerName} before insert on pending_changes " +
                $"for each row execute function {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>Removes a trigger installed by <see cref="InstallPendingChangeRaceTriggerAsync"/>.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name the trigger was installed under.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task DropPendingChangeRaceTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"drop trigger if exists {triggerName} on pending_changes; " +
                $"drop function if exists {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Installs a trigger that refuses every credit log insert, so a test can force the save to
        /// fail after a balance change was staged.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name of the trigger, so it can be dropped afterwards.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <b>The only way to prove atomicity from outside.</b> The balance change and its log row
        /// are staged in one save, so the interesting failure is one the database raises between
        /// staging and committing. A trigger is that failure, and it is installed under a name the
        /// test controls so it can be dropped in a <c>finally</c>.
        /// </remarks>
        public static async Task InstallCreditLogFailureTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"create or replace function {triggerName}_fn() returns trigger as $$ " +
                "begin " +
                "raise exception 'forced credit log failure'; " +
                "end; $$ language plpgsql; " +
                $"drop trigger if exists {triggerName} on credit_logs; " +
                $"create trigger {triggerName} before insert on credit_logs " +
                $"for each row execute function {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>Removes a trigger installed by <see cref="InstallCreditLogFailureTriggerAsync"/>.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="triggerName">Name the trigger was installed under.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task DropCreditLogFailureTriggerAsync(
            NpgsqlConnection connection,
            string triggerName,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                $"drop trigger if exists {triggerName} on credit_logs; " +
                $"drop function if exists {triggerName}_fn();",
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
